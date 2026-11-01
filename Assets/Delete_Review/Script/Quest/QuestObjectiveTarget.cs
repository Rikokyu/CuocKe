using UnityEngine;

/// <summary>
/// One-shot interaction point for a quest objective: a chore spot (sweep the yard) or a delivery
/// recipient (hand over the parcel). It is the shared placeholder for objectives that are not scrap pickups.
///
/// Rules:
///  - It only reacts while its quest is Active, so nothing counts (and no prompt shows) before the
///    NPC offered the quest or after it was finished.
///  - Each target counts ONCE per quest cycle. The 'used' flag flips BEFORE progress is reported, so
///    pressing E twice can never count twice. A repeatable quest starts a new cycle and re-arms it.
///  - It never pays money. Only the quest-giver NPC pays, through QuestManager.TurnIn.
///
/// Put it on a placeholder object (chore spot) or next to a VillagerFlavorNPC (delivery recipient).
/// VillagerFlavorNPC on the same object yields the E key while this target is waiting for the player.
/// Legacy Input and the shared prompt/banner follow the rest of the project.
/// </summary>
public class QuestObjectiveTarget : MonoBehaviour
{
    [Header("Quest")]
    [SerializeField] QuestDefinition quest;
    [Tooltip("Optional. Empty = use the quest's own objectiveKey.")]
    [SerializeField] string objectiveKeyOverride;
    [SerializeField, Min(1)] int amount = 1;

    [Header("Interaction")]
    [Tooltip("Keep this >= the interaction distance of a VillagerFlavorNPC on the same object.")]
    [SerializeField, Min(0.5f)] float interactionDistance = 3.2f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string promptText = "Nhấn E";
    [SerializeField, TextArea(1, 3)] string doneMessage = "Xong!";
    [Tooltip("Optional. If empty, the GameObject tagged 'Player' is used.")]
    [SerializeField] Transform player;

    [Header("Required held item (optional)")]
    [Tooltip("If set, this target only counts while the item is carried (e.g. the watering can for the garden plants). Empty = no requirement.")]
    [SerializeField] QuestCarryItem requiredItem;
    [Tooltip("Hint shown instead of the E prompt while the required item is not in hand.")]
    [SerializeField] string missingItemPromptText = "Cần có bình tưới để tưới";

    [Header("Story order (optional)")]
    [Tooltip("The target only works once the quest has at least this much progress (steps in a fixed order). 0 = always.")]
    [SerializeField, Min(0)] int minProgress = 0;
    [Tooltip("Hint shown instead of the E prompt while the earlier steps are not done. Empty = show nothing.")]
    [SerializeField] string lockedHintText = "";
    [Tooltip("Hidden when this target is used (e.g. a harvested plant). Not restored for repeatable quests.")]
    [SerializeField] GameObject hideOnUse;

    [Header("Item reward (optional)")]
    [Tooltip("Given to the existing InventorySystem when this target is used (e.g. the harvested vegetables). If the bag cannot take it, the target is NOT used and nothing counts.")]
    [SerializeField] ItemDefinition grantItem;
    [SerializeField, Min(1)] int grantCount = 1;
    [SerializeField] string bagFullMessage = "Túi đầy, không nhận thêm được.";

    [Header("Scene references")]
    [SerializeField] QuestManager questManager;
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] MessageBannerUI banner;
    [SerializeField] DialogueUI dialogue;

    int usedInCycle = -1;
    int interactedFrame = -1;
    bool promptShown;
    string shownPromptText;

    /// <summary>True while the quest is Active and this target was not used yet in the current cycle.</summary>
    public bool IsAvailable
    {
        get
        {
            if (quest == null || questManager == null) return false;
            QuestStatus status = questManager.GetStatus(quest);
            return status.State == QuestState.Active && status.Cycle != usedInCycle;
        }
    }

    /// <summary>True once this target was used in the quest's current cycle (read-only, for visual feedback such as a 'watered' plot).</summary>
    public bool IsUsed
    {
        get
        {
            if (quest == null || questManager == null) return false;
            return questManager.GetStatus(quest).Cycle == usedInCycle;
        }
    }

    /// <summary>
    /// True while this target wants the E key (waiting and in range) or used it this very frame.
    /// A VillagerFlavorNPC on the same object checks this so one key press never does both.
    /// </summary>
    public bool ClaimsInteraction
    {
        get { return interactedFrame == Time.frameCount || (IsAvailable && IsPlayerInRange()); }
    }

    void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (player == null) Debug.LogError("[QuestObjectiveTarget] No player assigned and no GameObject tagged 'Player' found.", this);
        if (quest == null || questManager == null || prompt == null)
            Debug.LogError("[QuestObjectiveTarget] Quest / QuestManager / InteractionPromptUI reference missing.", this);
    }

    void Update()
    {
        if (player == null || !IsAvailable)
        {
            SetPromptVisible(false);
            return;
        }

        bool inRange = IsPlayerInRange() && (dialogue == null || !dialogue.IsOpen) && !IsInventoryOpen();
        bool hasItem = HasRequiredItem;
        bool orderOk = OrderOk;
        // Earlier steps missing / required item missing: only a hint is shown (or nothing) and E does nothing.
        string text = !orderOk ? lockedHintText : (hasItem ? promptText : missingItemPromptText);
        SetPromptVisible(inRange && !string.IsNullOrEmpty(text), text);

        if (inRange && hasItem && orderOk && Input.GetKeyDown(interactKey))
            Interact();
    }

    /// <summary>Counts this target for the quest. Returns false (and reports nothing) if it is not available.</summary>
    public bool Interact()
    {
        if (!IsAvailable || !HasRequiredItem || !OrderOk) return false; // e.g. no watering can, or an earlier step is missing

        if (grantItem != null)
        {
            InventorySystem bag = InventorySystem.Instance;
            if (bag == null || !bag.CanAdd(grantItem, grantCount))
            {
                if (banner != null && !string.IsNullOrEmpty(bagFullMessage)) banner.Show(bagFullMessage);
                return false; // nothing used, nothing counted: try again after making room
            }
        }

        QuestStatus status = questManager.GetStatus(quest);
        usedInCycle = status.Cycle;          // flip first: a second press in the same frame cannot count again
        interactedFrame = Time.frameCount;
        SetPromptVisible(false);

        if (banner != null && !string.IsNullOrEmpty(doneMessage)) banner.Show(doneMessage);
        if (requiredItem != null) requiredItem.PlayUseFeedback();
        if (requiredItem != null && requiredItem.IsHeld) GameVfx.Water(transform.position + Vector3.up * 0.3f); // still held after the use = watering can (the consumed protein bag is not held any more)
        if (hideOnUse != null) hideOnUse.SetActive(false);
        if (grantItem != null && InventorySystem.Instance != null) InventorySystem.Instance.Add(grantItem, grantCount);

        string key = string.IsNullOrEmpty(objectiveKeyOverride) ? quest.objectiveKey : objectiveKeyOverride;
        questManager.ReportProgress(key, amount); // last, so the manager's own banner (objective done) is not overwritten
        return true;
    }

    bool IsPlayerInRange()
    {
        if (player == null) return false;
        Vector3 offset = player.position - transform.position;
        offset.y = 0f; // ignore height: the player's pivot is at the CharacterController centre
        return offset.sqrMagnitude <= interactionDistance * interactionDistance;
    }

    static bool IsInventoryOpen()
    {
        return InventorySystem.Instance != null && InventorySystem.Instance.isOpen;
    }

    /// <summary>True when no item is required, or the required item is in the player's hands.</summary>
    bool HasRequiredItem
    {
        get { return requiredItem == null || requiredItem.IsHeld; }
    }

    /// <summary>True when no step order is set, or the quest already has enough progress.</summary>
    bool OrderOk
    {
        get { return minProgress <= 0 || (questManager != null && quest != null && questManager.GetStatus(quest).Progress >= minProgress); }
    }

    void SetPromptVisible(bool visible)
    {
        SetPromptVisible(visible, promptText);
    }

    void SetPromptVisible(bool visible, string text)
    {
        if (prompt == null) return;
        if (!visible)
        {
            if (!promptShown) return;
            promptShown = false;
            shownPromptText = null;
            prompt.Hide(this);
            return;
        }

        if (promptShown && shownPromptText == text) return;
        promptShown = true;
        shownPromptText = text;
        prompt.Show(this, text);
    }

    void OnDisable()
    {
        SetPromptVisible(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
