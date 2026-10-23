using System.Collections;
using UnityEngine;

/// <summary>
/// The watering can of the grandparents' chore quest: a pickup that lies beside the house and is then CARRIED
/// by the player. Deliberately NOT an inventory item - it is one quest tool whose only state is "is it in
/// the player's hands" (IsHeld).
///
/// Rules:
///  - It can be picked up only while its quest is Active, with E (legacy Input like the rest of the project),
///    in range, and never while a dialogue or the inventory screen is open.
///  - IsHeld flips BEFORE anything else happens, so one press can never pick it up twice.
///  - While held, a copy of the model (heldModel, a child of an anchor under the first-person camera) is
///    visible. The player controller is not touched.
///  - A carry belongs to ONE quest run: the can stays in hand while the quest is Active or ReadyToTurnIn
///    (the player walks back to the grandmother with it) and is put away when the quest is completed or a
///    repeatable quest starts over. The world model is only shown while the can is not carried and the quest
///    has not been handed in.
///  - QuestObjectiveTarget reads IsHeld (its "requiredItem") to decide whether watering counts. This script
///    never counts progress and never pays - the quest, wallet and NPC systems do that.
/// </summary>
public class QuestCarryItem : MonoBehaviour
{
    [Header("Quest")]
    [SerializeField] QuestDefinition quest;
    [SerializeField] QuestManager questManager;

    [Header("Models")]
    [Tooltip("The can lying in the world (a child of this object). Hidden while carried and after the quest.")]
    [SerializeField] GameObject worldModel;
    [Tooltip("The carried copy under the first-person camera. Starts inactive.")]
    [SerializeField] GameObject heldModel;
    [Tooltip("Optional slow spin of the world model. 0 = off (default): the model keeps the position and rotation it has in the scene.")]
    [SerializeField] float worldSpinDegreesPerSecond = 0f;

    [Header("Carry rules")]
    [Tooltip("ON: the item leaves the player's hands as soon as the quest is ReadyToTurnIn (a delivered parcel is gone). OFF (default): it stays in hand until the quest is completed (the watering can).")]
    [SerializeField] bool releaseWhenReadyToTurnIn;

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] float interactionDistance = 1.8f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string promptText = "Nhấn E để nhặt bình tưới";
    [SerializeField, TextArea(1, 3)] string pickedUpMessage = "Đã cầm bình tưới. Mang ra sân tưới 3 cây cho ông bà!";
    [Tooltip("Optional. If empty, the GameObject tagged 'Player' is used.")]
    [SerializeField] Transform player;

    [Header("Scene references")]
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] MessageBannerUI banner;
    [SerializeField] DialogueUI dialogue;

    [Header("Pour feedback (carried can tilts while watering)")]
    [SerializeField] float pourTiltDegrees = 35f;
    [SerializeField, Min(0.1f)] float pourSeconds = 0.7f;

    [Header("Use")]
    [Tooltip("ON: the first successful use (QuestObjectiveTarget.requiredItem) uses the item up - it leaves the hand and does not come back to the world in this quest run (e.g. the sack of powder).")]
    [SerializeField] bool consumeOnUse;

    bool promptShown;
    int heldCycle = -1;
    int consumedCycle = -1;
    Quaternion heldBaseRotation = Quaternion.identity;
    Coroutine pourRoutine;

    /// <summary>True while the can is in the player's hands (read by QuestObjectiveTarget).</summary>
    public bool IsHeld { get; private set; }

    void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (player == null) Debug.LogError("[QuestCarryItem] No player assigned and no GameObject tagged 'Player' found.", this);
        if (quest == null || questManager == null || prompt == null)
            Debug.LogError("[QuestCarryItem] Quest / QuestManager / InteractionPromptUI reference missing.", this);
        if (worldModel == null || heldModel == null)
            Debug.LogError("[QuestCarryItem] worldModel / heldModel is not assigned.", this);

        if (heldModel != null)
        {
            heldBaseRotation = heldModel.transform.localRotation;
            heldModel.SetActive(false);
        }
    }

    void Update()
    {
        if (quest == null || questManager == null) return;
        QuestStatus status = questManager.GetStatus(quest);

        if (IsHeld && !CarryStillValid(status)) SetHeld(false);

        bool worldVisible = !IsHeld && consumedCycle != status.Cycle && (status.State == QuestState.NotStarted || status.State == QuestState.Active);
        if (worldModel != null)
        {
            if (worldModel.activeSelf != worldVisible) worldModel.SetActive(worldVisible);
            if (worldVisible && worldSpinDegreesPerSecond != 0f)
                worldModel.transform.Rotate(0f, worldSpinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }

        bool canPickUp = player != null && CanPickUp(status) && IsPlayerInRange() && !IsBlockedByUI();
        SetPromptVisible(canPickUp);

        if (canPickUp && Input.GetKeyDown(interactKey))
            TryPickUp();
    }

    /// <summary>
    /// Picks the can up if the quest is Active and it is not carried yet. Range and the E key are checked in
    /// Update; this is the single gate that makes picking up happen at most once per quest run.
    /// </summary>
    public bool TryPickUp()
    {
        if (quest == null || questManager == null || IsHeld) return false;

        QuestStatus status = questManager.GetStatus(quest);
        if (status.State != QuestState.Active) return false;

        heldCycle = status.Cycle;
        SetHeld(true); // flip first: a second press in the same frame cannot pick it up again
        SetPromptVisible(false);

        if (banner != null && !string.IsNullOrEmpty(pickedUpMessage)) banner.Show(pickedUpMessage);
        Debug.Log("[Quest] Watering can picked up");
        return true;
    }

    /// <summary>Small tilt of the carried can, called by QuestObjectiveTarget when a plant was watered.</summary>
    public void PlayUseFeedback()
    {
        if (IsHeld && consumeOnUse)
        {
            consumedCycle = heldCycle; // used up for this quest run
            SetHeld(false);
            return;
        }
        if (!IsHeld || heldModel == null || !isActiveAndEnabled) return;
        if (pourRoutine != null) StopCoroutine(pourRoutine);
        pourRoutine = StartCoroutine(Pour());
    }

    IEnumerator Pour()
    {
        Transform t = heldModel.transform;
        float elapsed = 0f;
        while (elapsed < pourSeconds)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(elapsed / pourSeconds) * Mathf.PI);
            t.localRotation = heldBaseRotation * Quaternion.Euler(pourTiltDegrees * k, 0f, 0f);
            yield return null;
        }
        t.localRotation = heldBaseRotation;
        pourRoutine = null;
    }

    bool CanPickUp(QuestStatus status)
    {
        return status.State == QuestState.Active && !IsHeld && consumedCycle != status.Cycle;
    }

    bool CarryStillValid(QuestStatus status)
    {
        bool running = status.State == QuestState.Active
                       || (status.State == QuestState.ReadyToTurnIn && !releaseWhenReadyToTurnIn);
        return running && status.Cycle == heldCycle;
    }

    void SetHeld(bool held)
    {
        IsHeld = held;
        if (!held && pourRoutine != null)
        {
            StopCoroutine(pourRoutine);
            pourRoutine = null;
        }

        if (heldModel != null)
        {
            if (held) heldModel.transform.localRotation = heldBaseRotation;
            heldModel.SetActive(held);
        }
    }

    bool IsPlayerInRange()
    {
        Vector3 offset = player.position - transform.position;
        offset.y = 0f; // ignore height: the player's pivot is at the CharacterController centre
        return offset.sqrMagnitude <= interactionDistance * interactionDistance;
    }

    bool IsBlockedByUI()
    {
        if (dialogue != null && dialogue.IsOpen) return true;
        return InventorySystem.Instance != null && InventorySystem.Instance.isOpen;
    }

    void SetPromptVisible(bool visible)
    {
        if (prompt == null || visible == promptShown) return;
        promptShown = visible;
        if (visible) prompt.Show(this, promptText);
        else prompt.Hide(this);
    }

    void OnDisable()
    {
        SetPromptVisible(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
