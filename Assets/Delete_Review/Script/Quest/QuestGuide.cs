using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Simple 2D guide for the quest that the HUD is tracking: a small arrow + the name and distance of the next thing
/// to do. It creates no quest logic: it only READS QuestManager (state, progress, TrackedQuest), QuestCarryItem.IsHeld,
/// QuestObjectiveTarget.IsAvailable and ScrapPickup.IsCollected, and shows ONE target at a time.
///
/// Rules are evaluated in order; the first one that matches the tracked quest wins (so the target changes by itself
/// when progress changes, an item is picked up, a quest is completed or another quest becomes the tracked one).
/// When the tracked quest has no matching rule, "offer" rules (quest NotStarted, prerequisite Completed) point to the
/// NPC that gives the next story quest. Nothing is shown when nothing matches.
/// The guide is hidden while a dialogue / shop / reward / inventory is open, during fights, while sleeping, in the
/// ending, and when the player is already within 'hideWithin' metres of the target (the E prompt takes over).
/// It sits at the top centre under the message banner, away from the crosshair area, the dialogue and the hotbar.
/// </summary>
public class QuestGuide : MonoBehaviour
{
    public enum HeldMode { Ignore, MustBeHeld, MustNotBeHeld }

    [Serializable]
    public class Rule
    {
        public string label = "Mục tiêu";
        public QuestDefinition quest;
        [Tooltip("Active / ReadyToTurnIn = the tracked quest is in that state. NotStarted = an 'offer' rule (points to the quest giver).")]
        public QuestState state = QuestState.Active;
        [Tooltip("Offer rules only: this quest must be Completed first.")]
        public QuestDefinition prerequisite;
        public int minProgress;
        [Tooltip("-1 = no upper limit.")]
        public int maxProgress = -1;
        public QuestCarryItem heldItem;
        public HeldMode heldMode = HeldMode.Ignore;
        [Tooltip("Only targets that still have a QuestObjectiveTarget waiting for the player count (e.g. plots not used yet).")]
        public bool requireAvailable;
        [Tooltip("The nearest valid one is shown.")]
        public Transform[] targets;
    }

    [Header("Scene references")]
    [SerializeField] QuestManager questManager;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] Transform player;

    [Header("UI (built in the scene)")]
    [SerializeField] GameObject panel;
    [SerializeField] RectTransform arrow;
    [SerializeField] TMP_Text label;

    [Header("Behaviour")]
    [SerializeField, Min(0f)] float hideWithin = 3f;
    [SerializeField, Min(0.05f)] float refreshSeconds = 0.2f;

    [Header("Rules (first match wins)")]
    [SerializeField] Rule[] rules;

    ShopUI shop;
    RewardUI reward;
    StoryBed bed;
    Camera cam;
    float nextRefresh;

    /// <summary>Current target (null when nothing is shown). Read by tests.</summary>
    public Transform CurrentTarget { get; private set; }
    public string CurrentLabel { get; private set; }
    public bool IsShown { get { return panel != null && panel.activeSelf; } }

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (questManager == null) questManager = QuestManager.Instance;
        shop = FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include);
        reward = FindFirstObjectByType<RewardUI>(FindObjectsInactive.Include);
        bed = FindFirstObjectByType<StoryBed>(FindObjectsInactive.Include);
        if (panel != null) panel.SetActive(false);
        if (player == null || questManager == null) Debug.LogError("[QuestGuide] Player / QuestManager missing.", this);
    }

    void Update()
    {
        if (player == null || questManager == null || panel == null) return;

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + refreshSeconds;
            Evaluate();
        }

        bool show = CurrentTarget != null && !Suppressed();
        float distance = 0f;
        if (show)
        {
            Vector3 toTarget = CurrentTarget.position - player.position;
            toTarget.y = 0f;
            distance = toTarget.magnitude;
            if (distance <= hideWithin) show = false;
            else UpdateArrow(toTarget, distance);
        }

        if (panel.activeSelf != show) panel.SetActive(show);
    }

    bool Suppressed()
    {
        if (dialogue != null && dialogue.IsOpen) return true;
        if (InventorySystem.Instance != null && InventorySystem.Instance.isOpen) return true;
        if (shop != null && shop.IsOpen) return true;
        if (reward != null && reward.IsOpen) return true;
        if (bed != null && bed.IsSleeping) return true;
        if (EndingSequence.Started) return true;
        BattleManager bm = BattleManager.Instance;
        if (bm != null && bm.IsBattleActive) return true;
        return false;
    }

    void UpdateArrow(Vector3 toTarget, float distance)
    {
        if (cam == null) cam = Camera.main;
        if (arrow != null && cam != null)
        {
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f && toTarget.sqrMagnitude > 0.0001f)
            {
                float angle = Vector3.SignedAngle(fwd, toTarget, Vector3.up); // + = target to the right
                arrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
        }
        if (label != null) label.text = CurrentLabel + "  -  " + Mathf.RoundToInt(distance) + " m";
    }

    // ------------------------------------------------------------ choosing the target
    void Evaluate()
    {
        CurrentTarget = null;
        CurrentLabel = null;
        if (rules == null) return;

        QuestStatus tracked = questManager.TrackedQuest;

        // pass 1: rules of the quest the HUD shows
        if (tracked != null)
        {
            for (int i = 0; i < rules.Length; i++)
            {
                Rule r = rules[i];
                if (r.quest == null || r.state == QuestState.NotStarted) continue;
                if (r.quest.questId != tracked.Definition.questId || tracked.State != r.state) continue;
                if (tracked.Progress < r.minProgress || (r.maxProgress >= 0 && tracked.Progress > r.maxProgress)) continue;
                if (!HeldOk(r)) continue;
                Transform t = Nearest(r);
                if (t != null) { Pick(r, t); return; }
            }
        }

        // pass 2: the NPC that gives the next story quest
        for (int i = 0; i < rules.Length; i++)
        {
            Rule r = rules[i];
            if (r.quest == null || r.state != QuestState.NotStarted) continue;
            if (questManager.GetState(r.quest.questId) != QuestState.NotStarted) continue;
            if (r.prerequisite != null && questManager.GetState(r.prerequisite.questId) != QuestState.Completed) continue;
            Transform t = Nearest(r);
            if (t != null) { Pick(r, t); return; }
        }
    }

    void Pick(Rule r, Transform t)
    {
        CurrentTarget = t;
        CurrentLabel = r.label;
    }

    static bool HeldOk(Rule r)
    {
        if (r.heldItem == null || r.heldMode == HeldMode.Ignore) return true;
        return r.heldMode == HeldMode.MustBeHeld ? r.heldItem.IsHeld : !r.heldItem.IsHeld;
    }

    Transform Nearest(Rule r)
    {
        if (r.targets == null) return null;
        Transform best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < r.targets.Length; i++)
        {
            Transform t = r.targets[i];
            if (!Valid(t, r.requireAvailable)) continue;
            Vector3 d = t.position - player.position;
            d.y = 0f;
            float s = d.sqrMagnitude;
            if (s < bestSqr) { bestSqr = s; best = t; }
        }
        return best;
    }

    static bool Valid(Transform t, bool requireAvailable)
    {
        if (t == null || !t.gameObject.activeInHierarchy) return false;
        QuestObjectiveTarget[] enabledCheck = t.GetComponents<QuestObjectiveTarget>();
        if (enabledCheck.Length > 0)
        {
            bool anyEnabled = false;
            for (int i = 0; i < enabledCheck.Length; i++) if (enabledCheck[i].enabled) anyEnabled = true;
            if (!anyEnabled) return false; // e.g. the friend's delivery point while he is away
        }
        ScrapPickup scrap = t.GetComponent<ScrapPickup>();
        if (scrap != null && scrap.IsCollected) return false;
        if (requireAvailable)
        {
            QuestObjectiveTarget[] objectives = t.GetComponents<QuestObjectiveTarget>();
            bool any = false;
            for (int i = 0; i < objectives.Length; i++) if (objectives[i].IsAvailable) any = true;
            if (!any) return false;
        }
        return true;
    }
}
