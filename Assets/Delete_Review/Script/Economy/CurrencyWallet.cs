using System;
using UnityEngine;

/// <summary>
/// Minimal player money balance: Add() earns money, TrySpend() spends it (used by the shop). Anything that shows or changes money talks to this component through
/// a serialized scene reference, so there is no singleton.
/// </summary>
[DisallowMultipleComponent]
public class CurrencyWallet : MonoBehaviour
{
    [SerializeField, Min(0)] int startingBalance = 0;

    public int Balance { get; private set; }

    /// <summary>Raised with the new balance every time it changes.</summary>
    public event Action<int> BalanceChanged;

    void Awake()
    {
        Balance = Mathf.Max(0, startingBalance);
    }

    /// <summary>Adds money. Returns false (and changes nothing) for a zero or negative amount.</summary>
    public bool Add(int amount)
    {
        if (amount <= 0) return false;

        long total = (long)Balance + amount;
        Balance = (int)Math.Min(total, int.MaxValue);
        BalanceChanged?.Invoke(Balance);
        return true;
    }

    /// <summary>True if the balance covers 'amount' (and amount is positive).</summary>
    public bool CanAfford(int amount)
    {
        return amount > 0 && Balance >= amount;
    }

    /// <summary>Spends money once. Returns false (and changes nothing) for a zero/negative amount or too little money.</summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || amount > Balance) return false;

        Balance -= amount;
        BalanceChanged?.Invoke(Balance);
        return true;
    }

}
