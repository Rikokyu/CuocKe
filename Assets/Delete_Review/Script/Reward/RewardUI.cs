using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// M10 - the medal exchange window of the Reward NPC. Same look and flow as ShopUI (it is a clone of that panel in the
/// scene and reuses ShopItemRow), but the price is MEDALS from TournamentManager and the goods come from a MedalRewardCatalog.
/// Money goes to the existing CurrencyWallet, items to the existing InventorySystem. No new currency, no new inventory.
///
/// Redeeming (TryRedeem) is all-or-nothing and runs in this order, so nothing is lost or paid twice:
///   1. window closed / data missing  -> nothing happens
///   2. this reward was already claimed -> message, nothing changes (also blocks double clicks)
///   3. not enough medals              -> message, nothing changes
///   4. no room in the bag for the item -> message, nothing changes (medals are NOT spent)
///   5. spend the medals, flip the 'claimed' flag, THEN grant item + money; if the item add unexpectedly fails the
///      medals are refunded and the flag is cleared.
/// Every reward can be claimed once. Medals are spent for good (TournamentManager.MedalsSpent); Medals (total earned) is untouched.
/// State is per play session (no save/load in the project yet).
/// </summary>
public class RewardUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] MedalRewardCatalog catalog;
    [SerializeField] CurrencyWallet wallet;
    [Tooltip("Optional. If empty, TournamentManager.Instance is used.")]
    [SerializeField] TournamentManager tournament;
    [Tooltip("Optional. If empty, InventorySystem.Instance is used.")]
    [SerializeField] InventorySystem inventory;

    [Header("UI")]
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text titleLabel;
    [SerializeField] TMP_Text moneyLabel;      // shows medals + money
    [SerializeField] TMP_Text statusLabel;
    [SerializeField] Transform rowContainer;
    [Tooltip("Inactive template row; one clone is created per catalog entry.")]
    [SerializeField] ShopItemRow rowTemplate;
    [SerializeField] Button closeButton;
    [SerializeField] TMP_Text closeButtonLabel;

    [Header("Input / texts")]
    [SerializeField] KeyCode closeKey = KeyCode.Escape;
    [SerializeField] string title = "Đổi huy chương";
    [SerializeField] string balanceFormat = "Huy chương: {0}    Tiền: {1}";   // {0} = medals left, {1} = money
    [SerializeField] string closeText = "Đóng (Esc)";
    [SerializeField] string costFormat = "Cần {0} huy chương";
    [SerializeField] string redeemText = "Đổi";
    [SerializeField] string claimedText = "Đã nhận";

    [Header("Messages ({0} = reward, {1} = cost, {2} = medals you have)")]
    [SerializeField] string redeemedFormat = "Đã đổi {0} (-{1} huy chương).";
    [SerializeField] string notEnoughFormat = "Không đủ huy chương đổi {0}: cần {1}, bạn có {2}.";
    [SerializeField] string fullFormat = "Túi đầy, không đổi được {0}. Huy chương không bị trừ.";
    [SerializeField] string alreadyFormat = "Bạn đã đổi {0} rồi.";
    [SerializeField] string notReadyText = "Quầy đổi thưởng chưa sẵn sàng.";

    [Header("Status colours")]
    [SerializeField] Color neutralColor = Color.white;
    [SerializeField] Color successColor = new Color(0.55f, 1f, 0.55f, 1f);
    [SerializeField] Color failColor = new Color(1f, 0.5f, 0.45f, 1f);

    [Header("Frozen while open (e.g. PlayerMovement, MouseMovement, HotbarUI, InventorySystem)")]
    [SerializeField] Behaviour[] disableWhileOpen;

    readonly List<ShopItemRow> rows = new List<ShopItemRow>();
    readonly List<MedalRewardEntry> shown = new List<MedalRewardEntry>();
    readonly List<bool> claimed = new List<bool>();
    bool[] wasEnabled;
    CursorLockMode previousLockState;
    bool previousCursorVisible;
    int openedOnFrame = -1;
    TournamentManager subscribedTo;

    public bool IsOpen { get; private set; }

    /// <summary>Number of rewards listed.</summary>
    public int RowCount => shown.Count;

    public MedalRewardEntry GetEntry(int index) => index >= 0 && index < shown.Count ? shown[index] : null;

    public bool IsClaimed(int index) => index >= 0 && index < claimed.Count && claimed[index];

    /// <summary>The Redeem button of row 'index', or null. (Lets tests click the real button.)</summary>
    public Button GetRedeemButton(int index) => index >= 0 && index < rows.Count ? rows[index].BuyButton : null;

    TournamentManager Tournament => tournament != null ? tournament : TournamentManager.Instance;
    InventorySystem Inventory => inventory != null ? inventory : InventorySystem.Instance;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (closeButtonLabel != null) closeButtonLabel.text = closeText;
        if (titleLabel != null) titleLabel.text = title;

        BuildRows();
    }

    void OnEnable()
    {
        if (wallet != null) wallet.BalanceChanged += OnBalanceChanged;
    }

    void OnDisable()
    {
        if (wallet != null) wallet.BalanceChanged -= OnBalanceChanged;
        Unsubscribe();
        Close(); // never leave the player frozen (e.g. when leaving Play Mode)
    }

    void BuildRows()
    {
        if (catalog == null || catalog.entries == null || rowTemplate == null || rowContainer == null)
        {
            Debug.LogError("[RewardUI] Catalog / row template / row container missing.", this);
            return;
        }

        for (int i = 0; i < catalog.entries.Length; i++)
        {
            MedalRewardEntry entry = catalog.entries[i];
            if (entry == null || (entry.item == null && entry.rewardMoney <= 0)) continue;

            int rowIndex = shown.Count;
            ShopItemRow row = Instantiate(rowTemplate, rowContainer);
            row.name = "Row_" + entry.rewardId;
            row.gameObject.SetActive(true);
            string cost = string.Format(costFormat, entry.medalCost);
            if (entry.item != null) row.Bind(entry.item, entry.description, cost, redeemText);
            else row.BindText(entry.displayName, entry.description, cost, redeemText);
            if (row.BuyButton != null) row.BuyButton.onClick.AddListener(() => TryRedeem(rowIndex));

            shown.Add(entry);
            rows.Add(row);
            claimed.Add(false);
        }
    }

    /// <summary>Opens the window. 'greeting' is shown in the status line. Returns false if it was already open.</summary>
    public bool Open(string greeting = null)
    {
        if (IsOpen) return false;

        IsOpen = true;
        openedOnFrame = Time.frameCount;
        FreezePlayer();

        TournamentManager t = Tournament;
        if (t != null && subscribedTo != t)
        {
            Unsubscribe();
            subscribedTo = t;
            subscribedTo.Changed += Refresh;
        }

        if (panel != null) panel.SetActive(true);
        SetStatus(greeting, neutralColor);
        Refresh();
        return true;
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        if (panel != null) panel.SetActive(false);
        RestorePlayer();
    }

    void Update()
    {
        if (!IsOpen || Time.frameCount == openedOnFrame) return;

        if (Input.GetKeyDown(closeKey)) Close();
    }

    /// <summary>
    /// Exchanges medals for reward 'rowIndex' ONCE. Returns true only if medals were spent and the reward was granted.
    /// Does nothing while the window is closed (a stale click can never redeem).
    /// </summary>
    public bool TryRedeem(int rowIndex)
    {
        if (!IsOpen) return false;

        MedalRewardEntry entry = GetEntry(rowIndex);
        TournamentManager t = Tournament;
        InventorySystem bag = Inventory;
        if (entry == null || t == null || (entry.rewardMoney > 0 && wallet == null) || (entry.item != null && bag == null))
        {
            SetStatus(notReadyText, failColor);
            return false;
        }

        string rewardName = entry.item != null ? entry.item.displayName : entry.displayName;

        if (claimed[rowIndex])
        {
            SetStatus(string.Format(alreadyFormat, rewardName), failColor);
            return false;
        }

        if (t.AvailableMedals < entry.medalCost)
        {
            SetStatus(string.Format(notEnoughFormat, rewardName, entry.medalCost, t.AvailableMedals), failColor);
            return false;
        }

        if (entry.item != null && !bag.CanAdd(entry.item, entry.itemCount))
        {
            SetStatus(string.Format(fullFormat, rewardName), failColor);
            return false;
        }

        if (!t.TrySpendMedals(entry.medalCost))
        {
            SetStatus(string.Format(notEnoughFormat, rewardName, entry.medalCost, t.AvailableMedals), failColor);
            return false;
        }
        claimed[rowIndex] = true; // flipped BEFORE granting: a second click can never pay twice

        if (entry.item != null && !bag.Add(entry.item, entry.itemCount))
        {
            // defensive: CanAdd said yes, so this should never happen
            claimed[rowIndex] = false;
            t.RefundMedals(entry.medalCost);
            SetStatus(string.Format(fullFormat, rewardName), failColor);
            return false;
        }
        if (entry.rewardMoney > 0) wallet.Add(entry.rewardMoney);

        SetStatus(string.Format(redeemedFormat, rewardName, entry.medalCost), successColor);
        Refresh();
        return true;
    }

    void OnBalanceChanged(int newBalance)
    {
        Refresh();
    }

    void Refresh()
    {
        TournamentManager t = Tournament;
        int medals = t != null ? t.AvailableMedals : 0;
        int money = wallet != null ? wallet.Balance : 0;
        if (moneyLabel != null) moneyLabel.text = string.Format(balanceFormat, medals, money);

        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].SetAffordable(medals >= shown[i].medalCost);
            if (claimed[i]) rows[i].SetButton(claimedText, false);
        }
    }

    void Unsubscribe()
    {
        if (subscribedTo != null) subscribedTo.Changed -= Refresh;
        subscribedTo = null;
    }

    void SetStatus(string text, Color color)
    {
        if (statusLabel == null) return;
        statusLabel.text = text ?? "";
        statusLabel.color = color;
    }

    void FreezePlayer()
    {
        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        int count = disableWhileOpen != null ? disableWhileOpen.Length : 0;
        wasEnabled = new bool[count];
        for (int i = 0; i < count; i++)
        {
            Behaviour b = disableWhileOpen[i];
            if (b == null) continue;
            wasEnabled[i] = b.enabled;
            b.enabled = false;
        }
    }

    void RestorePlayer()
    {
        int count = disableWhileOpen != null ? disableWhileOpen.Length : 0;
        for (int i = 0; i < count && wasEnabled != null && i < wasEnabled.Length; i++)
        {
            Behaviour b = disableWhileOpen[i];
            if (b != null && wasEnabled[i]) b.enabled = true;
        }

        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
    }
}
