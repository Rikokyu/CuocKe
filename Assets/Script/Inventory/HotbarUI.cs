using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// On-screen hotbar (5 slots) that shows what InventorySystem holds: icon, name, count per slot.
/// Keys 1-5 pick a slot, the mouse wheel steps through slots, the selected slot is highlighted.
/// It only DISPLAYS the inventory and tracks the selection - it never adds or removes items.
/// Other systems can read SelectedItem / SelectedStack (e.g. 'is the watering can selected?').
/// Input is ignored while a dialogue or the inventory screen is open, and the hotbar hides during dialogue.
/// </summary>
public class HotbarUI : MonoBehaviour
{
    [Serializable]
    public class SlotView
    {
        public Image background;
        public Image icon;
        public TMP_Text keyLabel;
        public TMP_Text nameLabel;
        public TMP_Text countLabel;
    }

    [SerializeField] SlotView[] slots;
    [Tooltip("Optional. If empty, InventorySystem.Instance is used.")]
    [SerializeField] InventorySystem inventory;
    [Tooltip("Optional. Used only to hide the hotbar and ignore keys while a dialogue is open.")]
    [SerializeField] DialogueUI dialogue;
    [SerializeField] CanvasGroup group;

    [Header("Look")]
    [SerializeField] Color normalColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] Color selectedColor = new Color(1f, 0.78f, 0.2f, 0.95f);
    [SerializeField, Min(1f)] float selectedScale = 1.12f;

    [Header("Input")]
    [SerializeField] bool wrapScroll = true;

    public int SelectedIndex { get; private set; }

    /// <summary>The selected slot's stack (may be empty), or null if there is no inventory.</summary>
    public ItemStack SelectedStack => inventory != null ? inventory.GetSlot(SelectedIndex) : null;

    /// <summary>The selected item, or null if the selected slot is empty.</summary>
    public ItemDefinition SelectedItem
    {
        get
        {
            ItemStack s = SelectedStack;
            return s != null && !s.IsEmpty ? s.item : null;
        }
    }

    public event Action<int> SelectionChanged;

    void Start()
    {
        if (inventory == null) inventory = InventorySystem.Instance;
        if (inventory != null) inventory.Changed += Refresh;
        else Debug.LogError("[HotbarUI] No InventorySystem found.", this);

        Refresh();
    }

    void OnDestroy()
    {
        if (inventory != null) inventory.Changed -= Refresh;
    }

    void Update()
    {
        bool dialogueOpen = dialogue != null && dialogue.IsOpen;
        if (group != null) group.alpha = dialogueOpen ? 0f : 1f;
        if (dialogueOpen || (inventory != null && inventory.isOpen)) return;

        int count = slots != null ? Mathf.Min(slots.Length, 9) : 0;
        for (int i = 0; i < count; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Select(i);

        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.01f) Step(-1);       // wheel up   -> previous slot
        else if (scroll < -0.01f) Step(1);  // wheel down -> next slot
    }

    /// <summary>Selects slot 'index' (0-based). Ignored if out of range.</summary>
    public void Select(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length || index == SelectedIndex) return;

        SelectedIndex = index;
        Refresh();
        if (SelectionChanged != null) SelectionChanged(index);
    }

    /// <summary>Moves the selection by +1 / -1 (wraps around if wrapScroll is on).</summary>
    public void Step(int direction)
    {
        if (slots == null || slots.Length == 0) return;

        int next = SelectedIndex + direction;
        if (wrapScroll) next = (next % slots.Length + slots.Length) % slots.Length;
        else next = Mathf.Clamp(next, 0, slots.Length - 1);
        Select(next);
    }

    /// <summary>Redraws every slot from the inventory. Called automatically when the inventory changes.</summary>
    public void Refresh()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            SlotView view = slots[i];
            if (view == null) continue;

            ItemStack stack = inventory != null ? inventory.GetSlot(i) : null;
            bool filled = stack != null && !stack.IsEmpty;
            bool selected = i == SelectedIndex;

            if (view.icon != null)
            {
                view.icon.enabled = filled;
                if (filled)
                {
                    ItemDefinition item = stack.item;
                    view.icon.sprite = item.icon;
                    view.icon.color = item.icon != null ? Color.white : item.placeholderColor;
                }
            }
            if (view.nameLabel != null) view.nameLabel.text = filled ? stack.item.displayName : "";
            if (view.countLabel != null) view.countLabel.text = filled ? "x" + stack.count : "";
            if (view.keyLabel != null) view.keyLabel.text = (i + 1).ToString();

            if (view.background != null)
            {
                view.background.color = selected ? selectedColor : normalColor;
                view.background.transform.localScale = Vector3.one * (selected ? selectedScale : 1f);
            }
        }
    }
}
