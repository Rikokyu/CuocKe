using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Village resident who offers, tracks and pays out the quests configured on it (Quest + Additional Quests).
///
/// All collaborators are serialized scene references (no hierarchy paths). The only thing resolved at
/// runtime is the player Transform, once in Start, by the 'Player' tag (like the rest of the project).
///
/// Flow: player within interactionDistance -> prompt -> interactKey (E, legacy Input like the rest of the
/// project) -> dialogue. The quest is accepted / paid ONLY from the dialogue's action button, and
/// QuestManager rejects repeated Accept/TurnIn calls, so double input can never accept or pay twice.
/// Dialogue lines are string.Format templates: {0} = progress, {1} = required amount, {2} = reward money.
/// </summary>
public class QuestNPC : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] string npcName = "Dân làng";
    [SerializeField] QuestDefinition quest;
    [Tooltip("Further quests this NPC offers, in priority order after 'Quest'. The NPC offers ONLY the quests configured here.")]
    [SerializeField] QuestDefinition[] additionalQuests;


    [Header("Interaction")]
    [SerializeField, Min(0.5f)] float interactionDistance = 4f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string promptText = "Nhấn E để nói chuyện";
    [Tooltip("Only offer the quest after the first battle was lost (an already started quest stays available).")]
    [SerializeField] bool requireFirstBattleLoss = true;
    [Tooltip("Optional. When set, this NPC offers nothing until that quest is Completed (an already started quest stays available). Used to keep the money / training quests closed until the rematch with the friend is won.")]
    [SerializeField] QuestDefinition requiredCompletedQuest;
    [Tooltip("Optional. Quests this NPC reminds about and pays, but NEVER offers (they are started by the story, e.g. the delivery that starts when the chicken is taken back). While such a quest is NotStarted the NPC ignores it.")]
    [SerializeField] QuestDefinition[] startedByStory;
    [Tooltip("Optional. Quests in 'Started By Story' are paid only after THIS quest is Completed (e.g. Bà pays the delivery only after the friend's fight is over). Before that she says 'Pay Later Lines' and pays nothing.")]
    [SerializeField] QuestDefinition storyPayAfterCompleted;
    [SerializeField, TextArea(2, 4)] string[] payLaterLines =
    {
        "Cháu giao hàng xong rồi à? Giỏi lắm! Nhưng lo xong chuyện với thằng bạn đã, rồi quay lại đây bà trả công nhé."
    };
    [Tooltip("Optional. This NPC also offers nothing until ALL of these quests are Completed (in addition to 'Required Completed Quest'). An already started quest stays available.")]
    [SerializeField] QuestDefinition[] alsoRequiredCompletedQuests;
    [Tooltip("Optional. If empty, the GameObject tagged 'Player' is used.")]
    [SerializeField] Transform player;
    [Tooltip("Optional. Rotated toward the player when a conversation starts.")]
    [SerializeField] Transform modelRoot;

    [Header("Scene references")]
    [SerializeField] QuestManager questManager;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] InteractionPromptUI prompt;

    [Header("Dialogue")]
    [SerializeField, TextArea(2, 4)] string[] offerLines =
    {
        "Gà của cậu còn yếu lắm. Muốn huấn luyện nó thì phải có tiền trước đã.",
        "Thử đi nhặt ve chai quanh làng đi. Đủ {1} món thì quay lại đây, tôi sẽ trả công cho cậu."
    };
    [SerializeField, TextArea(2, 4)] string[] activeLines =
    {
        "Cậu mới nhặt được {0}/{1} món thôi. Đi nhặt thêm ve chai quanh làng đi!"
    };
    [SerializeField, TextArea(2, 4)] string[] readyLines =
    {
        "Đủ {1} món rồi à? Giỏi lắm! Tiền công của cậu là {2} VNĐ."
    };
    [SerializeField, TextArea(2, 4)] string[] completedLines =
    {
        "Cảm ơn cậu nhé! Cầm tiền đó mà lo cho con gà đi."
    };
    [Tooltip("Optional small talk while none of this NPC's quests is available yet (e.g. before the first battle was lost). No quest, no money.")]
    [SerializeField, TextArea(2, 4)] string[] idleLines;
    
[SerializeField] string acceptLabel = "Nhận nhiệm vụ";
    [SerializeField] string claimLabel = "Nhận tiền công (+{2} VNĐ)";

    bool promptShown;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (player == null) Debug.LogError("[QuestNPC] No player assigned and no GameObject tagged 'Player' found.", this);
        if (questManager == null || dialogue == null || prompt == null)
            Debug.LogError("[QuestNPC] Quest / QuestManager / DialogueUI / InteractionPromptUI reference missing.", this);
    }

void Update()
    {
        if (player == null || questManager == null || dialogue == null) return;

        bool canInteract = IsPlayerInRange() && CanTalk() && !dialogue.IsOpen && !IsInventoryOpen() && InteractObject.IsAimed(this);
        SetPromptVisible(canInteract);

        if (canInteract && Input.GetKeyDown(interactKey))
            Interact();
    }

    /// <summary>Opens the dialogue that matches the quest state. Returns false if it cannot be opened right now.</summary>
public bool Interact() { return Interact(false); }

    /// <summary>
    /// Same as Interact(). skipLottery = true is used by the lottery dialogue's "Nói chuyện khác" choice, so the player can still
    /// reach the normal quest talk of MrBeast while holding lottery tickets.
    /// </summary>
    public bool Interact(bool skipLottery)
    {
        if (questManager == null || dialogue == null) return false;
        if (dialogue.IsOpen) return false;

        // MrBeast with lottery tickets in the bag: redeem offer first (LotteryRedeem). False for every other NPC / moment.
        if (!skipLottery && LotteryRedeem.Instance != null && LotteryRedeem.Instance.TryHandle(this))
        {
            SetPromptVisible(false);
            FacePlayer();
            return true;
        }

        // Morning after the garden quest: Bà answers 'where is my chicken' (MissingChickenStory). False for every other NPC / moment.
        // After the watering: Bà only points to Ông (GardenFlowStory). False for every other NPC / moment.
        if (GardenFlowStory.Instance != null && GardenFlowStory.Instance.TryHandleGrandparent(transform))
        {
            SetPromptVisible(false);
            FacePlayer();
            return true;
        }

        if (MissingChickenStory.Instance != null && MissingChickenStory.Instance.TryHandleGrandparent(transform))
        {
            SetPromptVisible(false);
            FacePlayer();
            return true;
        }

        // After the tournament: Bà tells the player to rest, and answers 'where is my chicken' the second time (ZoroReunionStory). False otherwise.
        if (ZoroReunionStory.Instance != null && ZoroReunionStory.Instance.TryHandleGrandparent(transform))
        {
            SetPromptVisible(false);
            FacePlayer();
            return true;
        }

        // Bà hands out the delivery right after Ông kept the chicken (before the friend). False for every other NPC / moment.
        if (GrandfatherChickenRescue.Instance != null && GrandfatherChickenRescue.Instance.TryHandleGrandmother(transform))
        {
            SetPromptVisible(false);
            FacePlayer();
            return true;
        }

        if (!IsOfferAvailable())
        {
            if (!HasIdleLines()) return false;
            dialogue.Open(npcName, idleLines, null, null); // plain small talk: no quest, no money
            SetPromptVisible(false);
            FacePlayer();
            return true;
        }

        QuestDefinition offered = GetOfferedQuest();
        if (offered == null)
        {
            if (!HasIdleLines()) return false;
            dialogue.Open(npcName, idleLines, null, null);
            SetPromptVisible(false);
            FacePlayer();
            return true;
        }

        QuestStatus status = questManager.GetStatus(offered);
        object[] args = { status.Progress, offered.requiredAmount, offered.rewardMoney };

        // Accept / claim only happen from the dialogue's action button; QuestManager refuses repeats.
        switch (status.State)
        {
            case QuestState.NotStarted:
                dialogue.Open(npcName, Format(Pick(offered.offerLines, offerLines), args), acceptLabel, () => questManager.AcceptQuest(offered));
                break;
            case QuestState.Active:
                dialogue.Open(npcName, Format(Pick(offered.activeLines, activeLines), args), null, null);
                break;
            case QuestState.ReadyToTurnIn:
                if (PayIsWaiting(offered)) dialogue.Open(npcName, payLaterLines, null, null); // a story quest that is paid only later: no button, no money
                else dialogue.Open(npcName, Format(Pick(offered.readyLines, readyLines), args), string.Format(claimLabel, args), () => questManager.TurnIn(offered));
                break;
            default: // Completed
                dialogue.Open(npcName, Format(Pick(offered.completedLines, completedLines), args), null, null);
                break;
        }

        SetPromptVisible(false);
        FacePlayer();
        return true;
    }





bool IsOfferAvailable()
    {
        if (!RequiredQuestsCompleted())
        {
            foreach (QuestDefinition started in ConfiguredQuests())
                if (questManager.GetState(started.questId) != QuestState.NotStarted) return true; // an already started quest stays available
            return false;
        }
        if (!requireFirstBattleLoss) return true;
        // after the loss the story first plays the friend's taunt; quests open only once that dialogue is finished
        if (ChildhoodFriendBattleTaunt.Instance != null) { if (ChildhoodFriendBattleTaunt.AfterLossStoryDone) return true; }
        else if (BattleManager.firstBattleCompleted) return true;

        // an already started quest stays available
        foreach (QuestDefinition q in ConfiguredQuests())
            if (questManager.GetState(q.questId) != QuestState.NotStarted) return true;
        return false;
    }

    bool RequiredQuestsCompleted()
    {
        if (requiredCompletedQuest != null && questManager.GetState(requiredCompletedQuest.questId) != QuestState.Completed) return false;
        if (alsoRequiredCompletedQuests != null)
            for (int i = 0; i < alsoRequiredCompletedQuests.Length; i++)
                if (alsoRequiredCompletedQuests[i] != null && questManager.GetState(alsoRequiredCompletedQuests[i].questId) != QuestState.Completed) return false;
        return true;
    }

    /// <summary>A 'started by story' quest that is ready, but whose payment must wait for 'Story Pay After Completed'.</summary>
    bool PayIsWaiting(QuestDefinition q)
    {
        return storyPayAfterCompleted != null && IsStartedByStory(q) && questManager.GetState(storyPayAfterCompleted.questId) != QuestState.Completed;
    }


    bool IsStartedByStory(QuestDefinition q)
    {
        if (startedByStory == null) return false;
        for (int i = 0; i < startedByStory.Length; i++) if (startedByStory[i] == q) return true;
        return false;
    }

bool CanTalk()
    {
        if (GrandfatherChickenRescue.Instance != null && GrandfatherChickenRescue.Instance.HasGrandmotherTask(transform)) return true;
        if (LotteryRedeem.Instance != null && LotteryRedeem.Instance.CanRedeemWith(this)) return true;
        return IsOfferAvailable() || HasIdleLines();
    }

    bool HasIdleLines()
    {
        return idleLines != null && idleLines.Length > 0;
    }

    /// <summary>The quests this NPC may offer: 'Quest' first, then 'Additional Quests'. Nothing else.</summary>
    IEnumerable<QuestDefinition> ConfiguredQuests()
    {
        if (quest != null) yield return quest;
        if (additionalQuests == null) yield break;
        for (int i = 0; i < additionalQuests.Length; i++)
        {
            QuestDefinition extra = additionalQuests[i];
            if (extra != null && extra != quest) yield return extra;
        }
    }

    /// <summary>The quest this NPC talks about right now: money owed first, then running, then a new one, then finished.</summary>
    QuestDefinition GetOfferedQuest()
    {
        QuestDefinition best = null;
        int bestRank = -1;
        foreach (QuestDefinition q in ConfiguredQuests())
        {
            QuestState qs = questManager.GetState(q.questId);
            if (qs == QuestState.NotStarted && IsStartedByStory(q)) continue; // never offered here
            int rank = Rank(qs);
            if (rank > bestRank)
            {
                best = q;
                bestRank = rank;
            }
        }
        return best;
    }

    static int Rank(QuestState state)
    {
        switch (state)
        {
            case QuestState.ReadyToTurnIn: return 3;
            case QuestState.Active: return 2;
            case QuestState.NotStarted: return 1;
            default: return 0; // Completed
        }
    }

    /// <summary>Per-quest dialogue from the QuestDefinition if it has any, otherwise this NPC's own default lines.</summary>
    static string[] Pick(string[] fromQuest, string[] fallback)
    {
        return fromQuest != null && fromQuest.Length > 0 ? fromQuest : fallback;
    }


    bool IsPlayerInRange()
    {
        Vector3 offset = player.position - transform.position;
        offset.y = 0f; // ignore height: the player's pivot is at the CharacterController centre
        return offset.sqrMagnitude <= interactionDistance * interactionDistance;
    }

    static bool IsInventoryOpen()
    {
        return InventorySystem.Instance != null && InventorySystem.Instance.isOpen;
    }

    void SetPromptVisible(bool visible)
    {
        if (prompt == null || visible == promptShown) return;
        promptShown = visible;
        if (visible) prompt.Show(this, promptText);
        else prompt.Hide(this);
    }

    void FacePlayer()
    {
        Transform t = modelRoot != null ? modelRoot : transform;
        Vector3 toPlayer = player.position - t.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(toPlayer);
    }

    static string[] Format(string[] templates, object[] args)
    {
        string[] result = new string[templates.Length];
        for (int i = 0; i < templates.Length; i++)
        {
            try { result[i] = string.Format(templates[i], args); }
            catch (System.FormatException) { result[i] = templates[i]; } // a stray '{' in the Inspector text must not break the dialogue
        }
        return result;
    }

    void OnDisable()
    {
        SetPromptVisible(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
