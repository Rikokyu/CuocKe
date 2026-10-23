using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tool shop window: lists ShopCatalog entries (icon, name, description, price), shows the player's
/// money, and sells one item per Buy click using the EXISTING CurrencyWallet and InventorySystem/Hotbar.
///
/// Buying (TryBuy) is all-or-nothing and runs in this order, so money is never lost:
///   1. not enough money  -> message, nothing changes
///   2. no room in the bag -> message, nothing changes
///   3. wallet.TrySpend(price) once, then inventory.Add(item); if Add unexpectedly fails the price is refunded.
///
/// Closing: close button or closeKey (Esc). Both are ignored on the frame the shop opened. While open it frees the
/// cursor and disables the configured behaviours (player movement, mouse look, hotbar keys/wheel, the 'I' inventory key),
/// then restores exactly the previous state on close - the same approach DialogueUI uses.
/// This component must live on an object that stays active; only the child 'panel' is toggled.
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] ShopCatalog catalog;
    [SerializeField] CurrencyWallet wallet;
    [Tooltip("Optional. If empty, InventorySystem.Instance is used.")]
    [SerializeField] InventorySystem inventory;

    [Header("UI")]
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text titleLabel;
    [SerializeField] TMP_Text moneyLabel;
    [SerializeField] TMP_Text statusLabel;
    [SerializeField] Transform rowContainer;
    [Tooltip("Inactive template row; one clone is created per catalog entry.")]
    [SerializeField] ShopItemRow rowTemplate;
    [SerializeField] Button closeButton;
    [SerializeField] TMP_Text closeButtonLabel;

    [Header("Input / texts")]
    [SerializeField] KeyCode closeKey = KeyCode.Escape;
    [SerializeField] string title = "Cửa hàng dụng cụ";
    [SerializeField] string moneyFormat = "Tiền: {0}";
    [SerializeField] string closeText = "Đóng (Esc)";
    [SerializeField] string priceFormat = "Giá: {0}";
    [SerializeField] string buyText = "Mua";

    [Header("Messages ({0} = item, {1} = price, {2} = your money)")]
    [SerializeField] string boughtFormat = "Đã mua {0} (-{1} tiền).";
    [SerializeField] string notEnoughFormat = "Không đủ tiền mua {0}: cần {1}, bạn có {2}.";
    [SerializeField] string fullFormat = "Túi đầy, không mua được {0}. Tiền không bị trừ.";
    [SerializeField] string notReadyText = "Cửa hàng chưa sẵn sàng.";

    [Header("Status colours")]
    [SerializeField] Color neutralColor = Color.white;
    [SerializeField] Color successColor = new Color(0.55f, 1f, 0.55f, 1f);
    [SerializeField] Color failColor = new Color(1f, 0.5f, 0.45f, 1f);

    [Header("Frozen while the shop is open (e.g. PlayerMovement, MouseMovement, HotbarUI, InventorySystem)")]
    [SerializeField] Behaviour[] disableWhileOpen;

    readonly List<ShopItemRow> rows = new List<ShopItemRow>();
    readonly List<ShopEntry> shown = new List<ShopEntry>();
    bool[] wasEnabled;
    CursorLockMode previousLockState;
    bool previousCursorVisible;
    int openedOnFrame = -1;

    public bool IsOpen { get; private set; }

    /// <summary>Number of goods listed.</summary>
    public int RowCount => shown.Count;

    /// <summary>The catalog entry shown in row 'index', or null.</summary>
    public ShopEntry GetEntry(int index) => index >= 0 && index < shown.Count ? shown[index] : null;

    /// <summary>The Buy button of row 'index', or null. (Lets tests click the real button.)</summary>
    public Button GetBuyButton(int index) => index >= 0 && index < rows.Count ? rows[index].BuyButton : null;

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
        Close(); // never leave the player frozen (e.g. when leaving Play Mode)
    }

    void BuildRows()
    {
        if (catalog == null || catalog.entries == null || rowTemplate == null || rowContainer == null)
        {
            Debug.LogError("[ShopUI] Catalog / row template / row container missing.", this);
            return;
        }

        for (int i = 0; i < catalog.entries.Length; i++)
        {
            ShopEntry entry = catalog.entries[i];
            if (entry == null || entry.item == null) continue;

            int rowIndex = shown.Count;
            ShopItemRow row = Instantiate(rowTemplate, rowContainer);
            row.name = "Row_" + entry.item.itemId;
            row.gameObject.SetActive(true);
            row.Bind(entry.item, entry.description, string.Format(priceFormat, entry.price), buyText);
            if (row.BuyButton != null) row.BuyButton.onClick.AddListener(() => TryBuy(rowIndex));

            shown.Add(entry);
            rows.Add(row);
        }
    }

    /// <summary>Opens the shop. 'greeting' is shown in the status line. Returns false if it was already open.</summary>
    public bool Open(string greeting = null)
    {
        if (IsOpen) return false;

        IsOpen = true;
        openedOnFrame = Time.frameCount;
        FreezePlayer();

        if (panel != null) panel.SetActive(true);
        SetStatus(greeting, neutralColor);
        RefreshMoney();
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
    /// Buys ONE of the item in row 'rowIndex'. Returns true only if money was spent and the item was added.
    /// Does nothing while the shop is closed (a stale click can never buy).
    /// </summary>
    public bool TryBuy(int rowIndex)
    {
        if (!IsOpen) return false;

        ShopEntry entry = GetEntry(rowIndex);
        InventorySystem bag = Inventory;
        if (entry == null || entry.item == null || wallet == null || bag == null)
        {
            SetStatus(notReadyText, failColor);
            return false;
        }

        string itemName = entry.item.displayName;

        if (wallet.Balance < entry.price)
        {
            SetStatus(string.Format(notEnoughFormat, itemName, entry.price, wallet.Balance), failColor);
            return false;
        }

        if (!bag.CanAdd(entry.item, 1))
        {
            SetStatus(string.Format(fullFormat, itemName), failColor);
            return false;
        }

        if (!wallet.TrySpend(entry.price))
        {
            SetStatus(string.Format(notEnoughFormat, itemName, entry.price, wallet.Balance), failColor);
            return false;
        }

        if (!bag.Add(entry.item, 1))
        {
            wallet.Add(entry.price); // defensive: CanAdd said yes, so this should never happen
            SetStatus(string.Format(fullFormat, itemName), failColor);
            return false;
        }

        SetStatus(string.Format(boughtFormat, itemName, entry.price), successColor);
        return true;
    }

    void OnBalanceChanged(int newBalance)
    {
        RefreshMoney();
    }

    void RefreshMoney()
    {
        int balance = wallet != null ? wallet.Balance : 0;
        if (moneyLabel != null) moneyLabel.text = string.Format(moneyFormat, balance);

        for (int i = 0; i < rows.Count; i++)
            rows[i].SetAffordable(balance >= shown[i].price);
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
