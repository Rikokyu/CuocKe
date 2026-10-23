using UnityEngine;

/// <summary>
/// M10 - the Reward NPC: shows the shared "[E] ..." prompt when the player is close and opens the RewardUI on E.
/// Mirrors ShopNPC (same shared prompt + dialogue, legacy-Input E key, flat range check). All rules (medal cost,
/// once-only, bag full) live in RewardUI / TournamentManager.
/// </summary>
public class RewardNPC : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField, TextArea(2, 3)] string greeting = "Có huy chương thì đổi quà ở đây. Mỗi món chỉ đổi một lần thôi nhé!";
    [SerializeField, TextArea(2, 3)] string noMedalGreeting = "Chưa có huy chương nào à? Thắng giải làng rồi quay lại đây.";

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] float interactionDistance = 1.8f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string promptText = "Nhấn E để đổi huy chương";
    [Tooltip("Optional. If empty, the GameObject tagged 'Player' is used.")]
    [SerializeField] Transform player;
    [Tooltip("Optional. Rotated toward the player when the window opens.")]
    [SerializeField] Transform modelRoot;

    [Header("Scene references")]
    [SerializeField] RewardUI rewardUI;
    [SerializeField] ShopUI shop;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] InteractionPromptUI prompt;

    bool promptShown;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (player == null) Debug.LogError("[RewardNPC] No player assigned and no GameObject tagged 'Player' found.", this);
        if (rewardUI == null || dialogue == null || prompt == null)
            Debug.LogError("[RewardNPC] RewardUI / DialogueUI / InteractionPromptUI reference missing.", this);
    }

    void Update()
    {
        if (player == null || rewardUI == null || dialogue == null) return;

        bool canInteract = IsPlayerInRange() && !rewardUI.IsOpen && !dialogue.IsOpen && !IsShopOpen() && !IsInventoryOpen();
        SetPromptVisible(canInteract);

        if (canInteract && Input.GetKeyDown(interactKey))
            Interact();
    }

    /// <summary>E key action. Opens the exchange window. Returns false if it cannot be opened right now.</summary>
    public bool Interact()
    {
        if (rewardUI == null || dialogue == null) return false;
        if (rewardUI.IsOpen || dialogue.IsOpen || IsShopOpen() || IsInventoryOpen()) return false;

        TournamentManager t = TournamentManager.Instance;
        string hello = (t == null || t.AvailableMedals <= 0) ? noMedalGreeting : greeting;
        if (!rewardUI.Open(hello)) return false;

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

    bool IsShopOpen()
    {
        return shop != null && shop.IsOpen;
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
        if (player == null) return;
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
