using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A world item the player can pick up with E and put into InventorySystem (and so onto the hotbar).
/// Put this on any object, assign an ItemDefinition and an amount. No per-item code is needed.
///
/// Same pattern as ScrapCollector: the instances share ONE static decision per frame - the NEAREST
/// pickup in range gets the single shared InteractionPromptUI, and one key press picks up only that one.
/// Duplicate pickup is prevented by a 'collected' flag that is set BEFORE the item is added, and the
/// object is deactivated afterwards. If the inventory has no room the item stays in the world.
///
/// Input is ignored while a dialogue or the inventory screen is open (like ScrapCollector).
/// Keep pickups at least ~4 m away from NPCs / quest targets so one E press cannot also trigger those.
/// </summary>
[DisallowMultipleComponent]
public class ItemPickup : MonoBehaviour
{
    [SerializeField] ItemDefinition item;
    [SerializeField, Min(1)] int amount = 1;
    [SerializeField, Min(0.5f)] float interactionDistance = 2f;
    [SerializeField] KeyCode interactKey = KeyCode.E;

    [Header("Texts ({0} = item name, {1} = amount)")]
    [SerializeField] string promptFormat = "Nhấn E để nhặt {0}";
    [SerializeField] string pickedFormat = "Nhặt được {0} x{1}";
    [SerializeField] string fullFormat = "Túi đầy, không nhặt được {0}";

    public bool IsCollected { get; private set; }

    // ---- shared (static) state: one decision per frame for all pickups ----
    static readonly List<ItemPickup> active = new List<ItemPickup>();
    static readonly object promptOwner = new object();
    static int evaluatedFrame = -1;
    static ItemPickup promptTarget;
    static Transform player;
    static InteractionPromptUI prompt;
    static MessageBannerUI banner;
    static DialogueUI dialogue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        active.Clear();
        evaluatedFrame = -1;
        promptTarget = null;
        player = null;
        prompt = null;
        banner = null;
        dialogue = null;
    }

    void OnEnable() { active.Add(this); }

    void OnDisable()
    {
        active.Remove(this);
        if (promptTarget == this)
        {
            promptTarget = null;
            if (prompt != null) prompt.Hide(promptOwner);
        }
    }

    void Update()
    {
        if (Time.frameCount == evaluatedFrame) return; // another pickup already decided this frame
        evaluatedFrame = Time.frameCount;
        Evaluate();
    }

    static void Evaluate()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (prompt == null) prompt = FindFirstObjectByType<InteractionPromptUI>();
        if (banner == null) banner = FindFirstObjectByType<MessageBannerUI>();
        if (dialogue == null) dialogue = FindFirstObjectByType<DialogueUI>();
        if (player == null) return;

        ItemPickup nearest = null;
        bool blocked = (dialogue != null && dialogue.IsOpen)
            || (InventorySystem.Instance != null && InventorySystem.Instance.isOpen);

        if (!blocked)
        {
            float bestSqr = float.MaxValue;
            Vector3 playerPos = player.position;
            for (int i = 0; i < active.Count; i++)
            {
                ItemPickup p = active[i];
                if (p == null || p.IsCollected || p.item == null) continue;

                Vector3 offset = p.transform.position - playerPos;
                offset.y = 0f; // the player's pivot is at the CharacterController centre
                float sqr = offset.sqrMagnitude;
                if (sqr <= p.interactionDistance * p.interactionDistance && sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = p;
                }
            }
        }

        SetPromptTarget(nearest);

        if (nearest != null && Input.GetKeyDown(nearest.interactKey))
        {
            nearest.TryPickUp();
            SetPromptTarget(null); // the next frame decides what to show next
        }
    }

    static void SetPromptTarget(ItemPickup target)
    {
        if (target == promptTarget) return;

        promptTarget = target;
        if (prompt == null) return;

        if (target == null) prompt.Hide(promptOwner);
        else prompt.Show(promptOwner, string.Format(target.promptFormat, target.item.displayName));
    }

    /// <summary>Adds the item to the inventory and removes this object from the world. False if it was not picked up.</summary>
    public bool TryPickUp()
    {
        if (IsCollected || item == null) return false;

        InventorySystem inventory = InventorySystem.Instance;
        if (inventory == null) return false;

        if (!inventory.CanAdd(item, amount))
        {
            if (banner != null) banner.Show(string.Format(fullFormat, item.displayName));
            return false;
        }

        IsCollected = true; // once-only gate: set before anything is awarded
        if (!inventory.Add(item, amount))
        {
            IsCollected = false;
            return false;
        }

        if (banner != null) banner.Show(string.Format(pickedFormat, item.displayName, amount));
        gameObject.SetActive(false);
        return true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
