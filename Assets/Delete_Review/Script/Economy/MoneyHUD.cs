using TMPro;
using UnityEngine;

/// <summary>Money HUD: always shows the wallet balance ("Tiền: 0") and refreshes whenever it changes.</summary>
public class MoneyHUD : MonoBehaviour
{
    [SerializeField] CurrencyWallet wallet;
    [SerializeField] TMP_Text label;
    [SerializeField] string format = "Tiền: {0}";

    void OnEnable()
    {
        if (wallet != null) wallet.BalanceChanged += OnBalanceChanged;
        Refresh();
    }

    void Start()
    {
        Refresh(); // wallet.Awake may have run after our OnEnable
    }

    void OnDisable()
    {
        if (wallet != null) wallet.BalanceChanged -= OnBalanceChanged;
    }

    void OnBalanceChanged(int newBalance)
    {
        Refresh();
    }

    void Refresh()
    {
        if (label != null && wallet != null)
            label.text = string.Format(format, wallet.Balance);
    }
}
