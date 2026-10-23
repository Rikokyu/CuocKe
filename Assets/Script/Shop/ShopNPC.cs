using UnityEngine;

/// <summary>
/// Shopkeeper: shows the shared "[E] ..." prompt when the player is close and opens the ShopUI on E.
/// Mirrors QuestNPC / VillagerFlavorNPC (same shared prompt + dialogue, legacy-Input E key, flat range check).
///
/// E is only handled here when the shop and any dialogue / the old inventory screen are closed, and the shop
/// ignores the key on the frame it opens, so one press can never open and act twice. Esc / the close button live in ShopUI.
/// Keep interactionDistance small: Scrap_Can_11 is only ~4.1 m away and ScrapCollector's range is 2.2 m, so the two
/// ranges must not overlap (1.8 + 2.2 < 4.1), otherwise one E press could also pick up the can.
/// </summary>
public class ShopNPC : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField, TextArea(2, 3)] string greeting = "Cháu cần dụng cụ gì nào? Có tiền là có hàng!";

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] float interactionDistance = 1.8f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string promptText = "Nhấn E để mua dụng cụ";
    [Tooltip("Optional. If empty, the GameObject tagged 'Player' is used.")]
    [SerializeField] Transform player;
    [Tooltip("Optional. Rotated toward the player when the shop opens.")]
    [SerializeField] Transform modelRoot;

    [Header("Scene references")]
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

        if (player == null) Debug.LogError("[ShopNPC] No player assigned and no GameObject tagged 'Player' found.", this);
        if (shop == null || dialogue == null || prompt == null)
            Debug.LogError("[ShopNPC] ShopUI / DialogueUI / InteractionPromptUI reference missing.", this);
    }

    void Update()
    {
        if (player == null || shop == null || dialogue == null) return;

        bool canInteract = IsPlayerInRange() && !shop.IsOpen && !dialogue.IsOpen && !IsInventoryOpen();
        SetPromptVisible(canInteract);

        if (canInteract && Input.GetKeyDown(interactKey))
            Interact();
    }

    /// <summary>Opens the shop. Returns false if it cannot be opened right now.</summary>
    public bool Interact()
    {
        if (shop == null || dialogue == null) return false;
        if (shop.IsOpen || dialogue.IsOpen || IsInventoryOpen()) return false;
        if (!shop.Open(greeting)) return false;

        SetPromptVisible(false);
        FacePlayer();
        return true;
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
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
