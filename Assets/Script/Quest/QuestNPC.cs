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
    [SerializeField, Min(0.5f)] float interactionDistance = 3f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string promptText = "Nhấn E để nói chuyện";
    [Tooltip("Only offer the quest after the first battle was lost (an already started quest stays available).")]
    [SerializeField] bool requireFirstBattleLoss = true;
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
        "Đủ {1} món rồi à? Giỏi lắm! Tiền công của cậu là {2} tiền."
    };
    [SerializeField, TextArea(2, 4)] string[] completedLines =
    {
        "Cảm ơn cậu nhé! Cầm tiền đó mà lo cho con gà đi."
    };
    [Tooltip("Optional small talk while none of this NPC's quests is available yet (e.g. before the first battle was lost). No quest, no money.")]
    [SerializeField, TextArea(2, 4)] string[] idleLines;
    
[SerializeField] string acceptLabel = "Nhận nhiệm vụ";
    [SerializeField] string claimLabel = "Nhận tiền công (+{2})";

    bool promptShown;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (player == null) Debug.LogError("[QuestNPC] No player assigned and no GameObject tagged 'Player' found.", this);
        if (quest == null || questManager == null || dialogue == null || prompt == null)
            Debug.LogError("[QuestNPC] Quest / QuestManager / DialogueUI / InteractionPromptUI reference missing.", this);
    }

void Update()
    {
        if (player == null || quest == null || questManager == null || dialogue == null) return;

        bool canInteract = IsPlayerInRange() && CanTalk() && !dialogue.IsOpen && !IsInventoryOpen();
        SetPromptVisible(canInteract);

        if (canInteract && Input.GetKeyDown(interactKey))
            Interact();
    }

    /// <summary>Opens the dialogue that matches the quest state. Returns false if it cannot be opened right now.</summary>
public bool Interact()
    {
        if (quest == null || questManager == null || dialogue == null) return false;
        if (dialogue.IsOpen) return false;

        if (!IsOfferAvailable())
        {
            if (!HasIdleLines()) return false;
            dialogue.Open(npcName, idleLines, null, null); // plain small talk: no quest, no money
            SetPromptVisible(false);
            FacePlayer();
            return true;
        }

        QuestDefinition offered = GetOfferedQuest();
        if (offered == null) return false;

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
                dialogue.Open(npcName, Format(Pick(offered.readyLines, readyLines), args), string.Format(claimLabel, args), () => questManager.TurnIn(offered));
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
        if (!requireFirstBattleLoss) return true;
        // after the loss the story first plays the friend's taunt; quests open only once that dialogue is finished
        if (ChildhoodFriendBattleTaunt.Instance != null) { if (ChildhoodFriendBattleTaunt.AfterLossStoryDone) return true; }
        else if (BattleManager.firstBattleCompleted) return true;

        // an already started quest stays available
        foreach (QuestDefinition q in ConfiguredQuests())
            if (questManager.GetState(q.questId) != QuestState.NotStarted) return true;
        return false;
    }

bool CanTalk()
    {
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
            int rank = Rank(questManager.GetState(q.questId));
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
