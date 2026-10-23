using System;
using System.Collections.Generic;
using UnityEngine;

public enum QuestState { NotStarted, Active, ReadyToTurnIn, Completed }

/// <summary>Runtime progress of one quest. Created lazily by QuestManager; read-only for everyone else.</summary>
public class QuestStatus
{
    public QuestDefinition Definition { get; private set; }
    public QuestState State { get; internal set; }
    public int Progress { get; internal set; }
    /// <summary>Increments on every accept. One-shot interaction points (chores, deliveries) use it to reset when a repeatable quest starts again.</summary>
    public int Cycle { get; internal set; }
    internal int AcceptOrder { get; set; }


    public QuestStatus(QuestDefinition definition)
    {
        Definition = definition;
        State = QuestState.NotStarted;
    }
}

/// <summary>
/// Minimal quest state machine: NotStarted -> Active -> ReadyToTurnIn -> Completed.
///
/// FOR THE SCRAP MILESTONE: when the player collects an item call
///     QuestManager.Instance.ReportProgress(QuestManager.ScrapObjective);      // +1
/// (or pass an amount). It advances every Active quest whose objectiveKey matches, ignores quests that are
/// not Active (so nothing counts before the NPC offered the quest) and clamps at the required amount.
/// At the required amount the quest becomes ReadyToTurnIn and ObjectiveCompleted fires.
///
/// Accept/TurnIn each succeed at most once per quest: the state is flipped BEFORE anything is paid out, so a
/// repeated call (double click, E + click in the same frame) returns false and can never pay twice.
/// No persistence in this milestone (same as BattleManager.firstBattleCompleted).
/// </summary>
[DisallowMultipleComponent]
public class QuestManager : MonoBehaviour
{
    /// <summary>Objective key for scrap collection (matches QuestDefinition.objectiveKey of the money quest).</summary>
    public const string ScrapObjective = "scrap";
    /// <summary>Objective key for simple chores (grandparents_helper).</summary>
    public const string ChoreObjective = "chore";
    /// <summary>Objective key for deliveries to designated NPCs (village_delivery).</summary>
    public const string DeliveryObjective = "delivery";


    /// <summary>Convenience accessor for scene objects that cannot hold a serialized reference (e.g. spawned scrap pickups).</summary>
    public static QuestManager Instance { get; private set; }

    [SerializeField] CurrencyWallet wallet;

    readonly Dictionary<string, QuestStatus> statuses = new Dictionary<string, QuestStatus>();
    int acceptCounter;


    /// <summary>Fires after ANY change to a quest (accepted, progress, ready, completed).</summary>
    public event Action<QuestStatus> QuestChanged;
    public event Action<QuestStatus> QuestAccepted;
    /// <summary>Fires once, when progress reaches the required amount (state is then ReadyToTurnIn).</summary>
    public event Action<QuestStatus> ObjectiveCompleted;
    /// <summary>Fires once, after the reward was paid.</summary>
    public event Action<QuestStatus> QuestCompleted;

    /// <summary>The quest the HUD should show: the most recently accepted one that is not completed yet.</summary>
    public QuestStatus TrackedQuest { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[QuestManager] A second QuestManager was found - this one is ignored.", this);
            enabled = false;
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Returns the runtime status of a quest (creating a NotStarted one the first time).</summary>
    public QuestStatus GetStatus(QuestDefinition quest)
    {
        if (quest == null) return null;

        QuestStatus status;
        if (!statuses.TryGetValue(quest.questId, out status))
        {
            status = new QuestStatus(quest);
            statuses[quest.questId] = status;
        }
        return status;
    }

    public QuestState GetState(string questId)
    {
        QuestStatus status;
        return statuses.TryGetValue(questId, out status) ? status.State : QuestState.NotStarted;
    }

    /// <summary>Starts the quest. Only works from NotStarted; returns false otherwise.</summary>
public bool AcceptQuest(QuestDefinition quest)
    {
        QuestStatus status = GetStatus(quest);
        if (status == null || status.State != QuestState.NotStarted) return false;

        status.State = QuestState.Active;
        status.Progress = 0;
        status.Cycle++;
        status.AcceptOrder = ++acceptCounter;
        TrackedQuest = status;
        Debug.Log("[Quest] Accepted: " + quest.questId);

        QuestChanged?.Invoke(status);
        QuestAccepted?.Invoke(status);
        return true;
    }

    /// <summary>
    /// Reports collected/achieved items for an objective key. Returns true if at least one quest advanced.
    /// </summary>
    public bool ReportProgress(string objectiveKey, int amount = 1)
    {
        if (string.IsNullOrEmpty(objectiveKey) || amount <= 0) return false;

        bool anyAdvanced = false;
        // copy: handlers are allowed to touch the quest list (e.g. accept a follow-up quest)
        foreach (QuestStatus status in new List<QuestStatus>(statuses.Values))
        {
            if (status.State != QuestState.Active) continue;
            if (status.Definition.objectiveKey != objectiveKey) continue;

            int before = status.Progress;
            status.Progress = Mathf.Min(status.Definition.requiredAmount, status.Progress + amount);
            if (status.Progress == before) continue;

            anyAdvanced = true;
            bool objectiveDone = status.Progress >= status.Definition.requiredAmount;
            if (objectiveDone) status.State = QuestState.ReadyToTurnIn;

            Debug.Log("[Quest] " + status.Definition.questId + " progress " + status.Progress + "/" + status.Definition.requiredAmount);
            QuestChanged?.Invoke(status);
            if (objectiveDone) ObjectiveCompleted?.Invoke(status);
        }
        return anyAdvanced;
    }

    /// <summary>Pays the reward and completes the quest. Only works from ReadyToTurnIn, and only once.</summary>
public bool TurnIn(QuestDefinition quest)
    {
        QuestStatus status = GetStatus(quest);
        if (status == null || status.State != QuestState.ReadyToTurnIn) return false;

        status.State = QuestState.Completed; // flip first: any repeated call from here on is rejected
        if (TrackedQuest == status) TrackedQuest = FindNextTracked(status);

        if (wallet != null) wallet.Add(quest.rewardMoney);
        else Debug.LogWarning("[QuestManager] No CurrencyWallet assigned - reward was NOT paid.", this);

        Debug.Log("[Quest] Completed: " + quest.questId + " (reward " + quest.rewardMoney + ")");
        QuestChanged?.Invoke(status);
        QuestCompleted?.Invoke(status);

        // Repeatable quests return to NotStarted only AFTER the reward was paid and announced,
        // so a turn-in can never pay twice. One-time quests simply stay Completed.
        if (quest.repeatable)
        {
            status.State = QuestState.NotStarted;
            status.Progress = 0;
            QuestChanged?.Invoke(status);
        }
        return true;
    }

/// <summary>The most recently accepted quest that is still running, so the HUD falls back to it when the tracked one is done.</summary>
    QuestStatus FindNextTracked(QuestStatus except)
    {
        QuestStatus best = null;
        foreach (QuestStatus s in statuses.Values)
        {
            if (s == except) continue;
            if (s.State != QuestState.Active && s.State != QuestState.ReadyToTurnIn) continue;
            if (best == null || s.AcceptOrder > best.AcceptOrder) best = s;
        }
        return best;
    }

}
