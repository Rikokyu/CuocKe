using System;
using UnityEngine;

/// <summary>
/// Static data for one kind of carried item (ve chai, binh tuoi cay, thung hang, ...).
/// Create more with: Assets > Create > Chicken Training > Inventory Item. No code change is needed per item.
/// </summary>
[CreateAssetMenu(fileName = "Item_New", menuName = "Chicken Training/Inventory Item")]
public class ItemDefinition : ScriptableObject
{
    [Tooltip("Stable ID, e.g. watering_can.")]
    public string itemId = "new_item";

    [Tooltip("Name shown in the hotbar slot and the pickup prompt.")]
    public string displayName = "Vật phẩm";

    [Tooltip("Optional. If empty, the hotbar shows a plain coloured square (placeholder).")]
    public Sprite icon;

    [Tooltip("Colour of the placeholder square used while 'icon' is empty.")]
    public Color placeholderColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    [Tooltip("Maximum count of this item in ONE hotbar slot.")]
    [Min(1)] public int maxStack = 20;
}

/// <summary>One hotbar/inventory slot: an item and how many of it. An empty slot has item == null.</summary>
[Serializable]
public class ItemStack
{
    public ItemDefinition item;
    public int count;

    public bool IsEmpty => item == null || count <= 0;
}
