using System;
using UnityEngine;

/// <summary>One thing the shop sells: an existing inventory item, its price and a short description.</summary>
[Serializable]
public class ShopEntry
{
    public ItemDefinition item;
    [Min(1)] public int price = 10;
    [TextArea(1, 3)] public string description;
}

/// <summary>
/// The list of goods one shop sells. Create with: Assets > Create > Chicken Training > Shop Catalog.
/// Selling a new item = add one entry here (item asset, price, description). No code change is needed.
/// Item icon, name and stack size stay on the ItemDefinition, so the shop never duplicates item data.
/// </summary>
[CreateAssetMenu(fileName = "ShopCatalog_New", menuName = "Chicken Training/Shop Catalog")]
public class ShopCatalog : ScriptableObject
{
    public ShopEntry[] entries;
}
