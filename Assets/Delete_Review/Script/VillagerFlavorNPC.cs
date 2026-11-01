using UnityEngine;

/// <summary>
/// Flavor NPC: shows the shared "[E] ..." prompt and opens the shared DialogueUI with a short, fixed
/// set of lines. No quest, no money, no state machine — for ambient village characters (ông, bà, bạn
/// thời thơ ấu, dân làng) that just need a line or two of dialogue.
///
/// Deliberately mirrors QuestNPC's interaction loop (same prompt/dialogue singletons, same legacy-Input
/// E key, same range check) so it behaves consistently with the quest NPC already in the scene, without
/// touching QuestManager or any quest data.
/// </summary>
public class VillagerFlavorNPC : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] string npcName = "Dân làng";

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] float interactionDistance = 3f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string promptText = "Nhấn E để nói chuyện";
    [Tooltip("Optional. If empty, the GameObject tagged 'Player' is used.")]
    [SerializeField] Transform player;
    [Tooltip("Optional. Rotated toward the player when a conversation starts.")]
    [SerializeField] Transform modelRoot;

    [Header("Scene references (shared singletons)")]
    [SerializeField] DialogueUI dialogue;
    [SerializeField] InteractionPromptUI prompt;

    [Header("Dialogue (repeats every time; simple flavor text)")]
    [SerializeField, TextArea(2, 4)] string[] lines =
    {
        "..."
    };

    bool promptShown;
    QuestObjectiveTarget questTarget; // optional: a quest objective (e.g. delivery) on the same NPC


    void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (player == null) Debug.LogError("[VillagerFlavorNPC] No player assigned and no GameObject tagged 'Player' found.", this);
        if (dialogue == null || prompt == null)
            Debug.LogError("[VillagerFlavorNPC] DialogueUI / InteractionPromptUI reference missing.", this);

        questTarget = GetComponent<QuestObjectiveTarget>();

    }

    void Update()
    {
        if (player == null || dialogue == null) return;

        bool canInteract = IsPlayerInRange() && !dialogue.IsOpen && !YieldsToQuest();
        SetPromptVisible(canInteract);

        if (canInteract && Input.GetKeyDown(interactKey))
            Interact();
    }

    public bool Interact()
    {
        if (dialogue == null || dialogue.IsOpen || lines == null || lines.Length == 0 || YieldsToQuest()) return false;

        dialogue.Open(npcName, lines, null, null); // plain conversation: no action button
        SetPromptVisible(false);
        FacePlayer();
        return true;
    }

    bool IsPlayerInRange()
    {
        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= interactionDistance * interactionDistance;
    }

/// <summary>A quest objective on this NPC (e.g. a delivery) owns the E key while it waits for the player.</summary>
    bool YieldsToQuest()
    {
        return questTarget != null && questTarget.ClaimsInteraction;
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
