using TMPro;
using UnityEngine;

/// <summary>HUD line that shows how many of ONE item the bag holds (e.g. "Vé số: 3"). Refreshes whenever the inventory changes. Display only.</summary>
public class ItemCountHUD : MonoBehaviour
{
    [SerializeField] ItemDefinition item;
    [SerializeField] TMP_Text label;
    [SerializeField] string format = "Vé số: {0}";

    InventorySystem inventory;

    void OnEnable()
    {
        Bind();
        Refresh();
    }

    void Start()
    {
        Bind(); // InventorySystem.Instance may be set after our OnEnable
        Refresh();
    }

    void OnDisable()
    {
        if (inventory != null) inventory.Changed -= Refresh;
        inventory = null;
    }

    void Bind()
    {
        InventorySystem current = InventorySystem.Instance;
        if (current == inventory || current == null) return;
        if (inventory != null) inventory.Changed -= Refresh;
        inventory = current;
        inventory.Changed += Refresh;
    }

    void Refresh()
    {
        if (label == null) return;
        int count = inventory != null && item != null ? inventory.GetCount(item) : 0;
        label.text = string.Format(format, count);
    }
}
