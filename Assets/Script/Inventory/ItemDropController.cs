using UnityEngine;

/// <summary>
/// Q = drop the item of the SELECTED hotbar slot on the ground in front of the player.
///
///  - A carried quest item (watering can, delivery box: QuestCarryItem with a hotbarItem) is put down where the player stands:
///    its world model appears there and E picks it up again. Quest progress and money are never touched.
///  - Any other hotbar item (ve chai, ve so, mat, ...) drops ONE piece: it is taken out of InventorySystem and a small 2D sprite of
///    its icon lies on the ground (ItemPickup, E picks it up again).
///
/// Ignored while a dialogue, the shop, the trainer window, the reward window or the inventory screen is open.
/// </summary>
public class ItemDropController : MonoBehaviour
{
    [SerializeField] KeyCode dropKey = KeyCode.Q;
    [SerializeField, Min(0.3f)] float dropDistance = 1.3f;
    [SerializeField, Min(0.1f)] float droppedSpriteSize = 0.5f;
    [SerializeField] string droppedFormat = "Đã vứt {0}. Nhấn E để nhặt lại.";

    [Header("Scene references (empty = found automatically)")]
    [SerializeField] HotbarUI hotbar;
    [SerializeField] MessageBannerUI banner;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] ShopUI shop;
    [SerializeField] TrainerNPC trainer;
    [SerializeField] Transform player;

    QuestCarryItem[] carryItems;

    void Start()
    {
        if (hotbar == null) hotbar = FindAnyObjectByType<HotbarUI>();
        if (banner == null) banner = FindAnyObjectByType<MessageBannerUI>();
        if (dialogue == null) dialogue = FindAnyObjectByType<DialogueUI>();
        if (shop == null) shop = FindAnyObjectByType<ShopUI>();
        if (trainer == null) trainer = FindAnyObjectByType<TrainerNPC>();
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        carryItems = FindObjectsByType<QuestCarryItem>(FindObjectsInactive.Include);
        if (hotbar == null || player == null) Debug.LogError("[ItemDropController] HotbarUI / Player not found.", this);
    }

    void Update()
    {
        if (hotbar == null || player == null) return;
        if (!Input.GetKeyDown(dropKey)) return;
        if (IsBlocked()) return;
        TryDropSelected();
    }

    /// <summary>Q action. True if something was dropped.</summary>
    public bool TryDropSelected()
    {
        ItemDefinition selected = hotbar != null ? hotbar.SelectedItem : null;
        InventorySystem inv = InventorySystem.Instance;
        if (selected == null || inv == null || player == null) return false;

        Vector3 ground = GroundPointInFront();

        // 1. a carried quest item
        if (carryItems != null)
        {
            for (int i = 0; i < carryItems.Length; i++)
            {
                QuestCarryItem c = carryItems[i];
                if (c == null || c.HotbarItem != selected || !c.IsHeld) continue;
                if (!c.Drop(ground)) continue;
                Say(string.Format(droppedFormat, selected.displayName));
                return true;
            }
        }

        // 2. an ordinary item: one piece goes to the ground as a small 2D sprite
        if (!inv.Remove(selected, 1)) return false; // taken out first, then it exists in the world (never both)
        SpawnDropped(selected, ground);
        Say(string.Format(droppedFormat, selected.displayName));
        return true;
    }

    void SpawnDropped(ItemDefinition item, Vector3 ground)
    {
        GameObject go = new GameObject("Dropped_" + item.itemId);
        go.transform.position = ground + Vector3.up * (droppedSpriteSize * 0.6f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        Sprite sprite = item.icon != null ? item.icon : PlaceholderSprite();
        sr.sprite = sprite;
        sr.color = item.icon != null ? Color.white : item.placeholderColor;

        float width = Mathf.Max(0.01f, sprite.bounds.size.x);
        go.transform.localScale = Vector3.one * (droppedSpriteSize / width);

        go.AddComponent<DroppedItemSprite>();
        go.AddComponent<ItemPickup>().Setup(item, 1);
    }

    static Sprite placeholder;
    static Sprite PlaceholderSprite()
    {
        if (placeholder != null) return placeholder;
        Texture2D t = new Texture2D(4, 4);
        Color[] px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        t.SetPixels(px);
        t.Apply();
        placeholder = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return placeholder;
    }

    Vector3 GroundPointInFront()
    {
        Vector3 flatForward = player.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.01f) flatForward = Vector3.forward;
        flatForward.Normalize();

        Vector3 top = player.position + flatForward * dropDistance + Vector3.up * 2f;
        RaycastHit[] hits = Physics.RaycastAll(top, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        Vector3 best = Vector3.zero;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].transform.IsChildOf(player) || hits[i].transform == player) continue; // not the player's own collider
            if (hits[i].distance < bestDist) { bestDist = hits[i].distance; best = hits[i].point; found = true; }
        }
        if (found) return best;

        Vector3 fallback = player.position + flatForward * dropDistance;
        fallback.y = player.position.y - 0.9f; // the pivot is at the CharacterController centre
        return fallback;
    }

    bool IsBlocked()
    {
        if (dialogue != null && dialogue.IsOpen) return true;
        if (shop != null && shop.IsOpen) return true;
        if (trainer != null && trainer.IsOpen) return true;
        return InventorySystem.Instance != null && InventorySystem.Instance.isOpen;
    }

    void Say(string message)
    {
        if (banner != null) banner.Show(message);
    }
}

/// <summary>A dropped item's 2D sprite: always faces the camera (turns around the vertical axis only) and bobs slightly.</summary>
public class DroppedItemSprite : MonoBehaviour
{
    float baseY;

    void Start() { baseY = transform.position.y; }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 away = transform.position - cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(away);
        }
        Vector3 p = transform.position;
        p.y = baseY + Mathf.Sin(Time.time * 2f) * 0.04f;
        transform.position = p;
    }
}
