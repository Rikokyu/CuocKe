using System;
using UnityEngine;

/// <summary>One thing the Reward NPC gives for medals: an existing inventory item and/or money. Redeemable once.</summary>
[Serializable]
public class MedalRewardEntry
{
    [Tooltip("Stable ID, e.g. feed_pack.")]
    public string rewardId = "reward";
    public string displayName = "Phần thưởng";
    [TextArea(1, 3)] public string description;
    [Min(1)] public int medalCost = 1;

    [Header("What the player gets (item and/or money)")]
    public ItemDefinition item;
    [Min(1)] public int itemCount = 1;
    [Min(0)] public int rewardMoney = 0;
}

/// <summary>
/// The list of rewards the Reward NPC (M10) exchanges medals for. Create with: Assets > Create > CuocKe > Medal Reward Catalog.
/// Adding a reward = adding one entry here (no code change). Items stay on ItemDefinition, money goes to the existing
/// CurrencyWallet, so nothing is duplicated. Every entry can be redeemed ONCE.
/// </summary>
[CreateAssetMenu(fileName = "MedalRewardCatalog_New", menuName = "CuocKe/Medal Reward Catalog")]
public class MedalRewardCatalog : ScriptableObject
{
    public MedalRewardEntry[] entries;
}
