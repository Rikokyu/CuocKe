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
    [Tooltip("Optional. If empty, PetEquipment.Instance is used. Gear goods (nón lá, tam kiếm) are worn by the chicken instead of going into the bag.")]
    [SerializeField] PetEquipment petEquipment;

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

    [Header("Quantity (buy one or several at once)")]
    [SerializeField] Button quantityMinusButton;
    [SerializeField] Button quantityPlusButton;
    [SerializeField] TMP_Text quantityLabel;
    [SerializeField, Min(1)] int maxQuantity = 20;
    [SerializeField] string quantityFormat = "{0}";

    [Header("Input / texts")]
    [SerializeField] KeyCode closeKey = KeyCode.Escape;
    [SerializeField] string title = "Cửa hàng dụng cụ";
    [SerializeField] string moneyFormat = "Tiền: {0} VNĐ";
    [SerializeField] string closeText = "Đóng (Esc)";
    [SerializeField] string priceFormat = "Giá: {0} VNĐ";
    [SerializeField] string buyText = "Mua";

    [Header("Messages ({0} = item, {1} = price, {2} = your money)")]
    [SerializeField] string boughtFormat = "Đã mua {0} (-{1} VNĐ).";
    [SerializeField] string notEnoughFormat = "Không đủ tiền mua {0}: cần {1} VNĐ, bạn có {2} VNĐ.";
    [SerializeField] string fullFormat = "Túi đầy, không mua được {0}. Tiền không bị trừ.";
    [SerializeField] string notReadyText = "Cửa hàng chưa sẵn sàng.";
    [SerializeField] string alreadyOwnedFormat = "Gà đã có {0} rồi, không cần mua thêm.";
    [SerializeField] string ownedButtonText = "Đã mua";

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
    int quantity = 1;

    /// <summary>How many pieces one Buy click buys (1..maxQuantity).</summary>
    public int Quantity { get { return quantity; } }

    public bool IsOpen { get; private set; }

    /// <summary>Number of goods listed.</summary>
    public int RowCount => shown.Count;

    /// <summary>The catalog entry shown in row 'index', or null.</summary>
    public ShopEntry GetEntry(int index) => index >= 0 && index < shown.Count ? shown[index] : null;

    /// <summary>The Buy button of row 'index', or null. (Lets tests click the real button.)</summary>
    public Button GetBuyButton(int index) => index >= 0 && index < rows.Count ? rows[index].BuyButton : null;

    InventorySystem Inventory => inventory != null ? inventory : InventorySystem.Instance;

    PetEquipment Gear => petEquipment != null ? petEquipment : PetEquipment.Instance;

    /// <summary>True if this entry is worn by the chicken (one piece only, price is never multiplied by the quantity).</summary>
    bool IsGear(ShopEntry entry)
    {
        PetEquipment gear = Gear;
        return entry != null && entry.item != null && gear != null && gear.Handles(entry.item);
    }

    int PriceFor(int rowIndex)
    {
        return IsGear(shown[rowIndex]) ? shown[rowIndex].price : shown[rowIndex].price * quantity;
    }

    /// <summary>Gear rows: button says 'Đã mua' and is disabled once the chicken wears the item.</summary>
    void RefreshOwned()
    {
        PetEquipment gear = Gear;
        for (int i = 0; i < rows.Count && i < shown.Count; i++)
        {
            if (!IsGear(shown[i])) continue;
            bool owned = gear.Owns(shown[i].item);
            rows[i].SetButton(owned ? ownedButtonText : buyText, !owned);
        }
    }

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (closeButtonLabel != null) closeButtonLabel.text = closeText;
        if (titleLabel != null) titleLabel.text = title;
        if (quantityMinusButton != null) quantityMinusButton.onClick.AddListener(() => SetQuantity(quantity - 1));
        if (quantityPlusButton != null) quantityPlusButton.onClick.AddListener(() => SetQuantity(quantity + 1));

        BuildRows();
        SetQuantity(1);
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

        SetQuantity(1);
        if (panel != null) panel.SetActive(true);
        SetStatus(greeting, neutralColor);
        RefreshOwned();
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
        bool gearEntry = IsGear(entry);
        if (entry == null || entry.item == null || wallet == null || (!gearEntry && bag == null))
        {
            SetStatus(notReadyText, failColor);
            return false;
        }

        if (gearEntry) return TryBuyGear(entry);

        int count = quantity;
        int total = entry.price * count;
        string itemName = count > 1 ? entry.item.displayName + " x" + count : entry.item.displayName;

        if (wallet.Balance < total)
        {
            SetStatus(string.Format(notEnoughFormat, itemName, total, wallet.Balance), failColor);
            return false;
        }

        if (!bag.CanAdd(entry.item, count))
        {
            SetStatus(string.Format(fullFormat, itemName), failColor);
            return false;
        }

        if (!wallet.TrySpend(total))
        {
            SetStatus(string.Format(notEnoughFormat, itemName, total, wallet.Balance), failColor);
            return false;
        }

        if (!bag.Add(entry.item, count))
        {
            wallet.Add(total); // defensive: CanAdd said yes, so this should never happen
            SetStatus(string.Format(fullFormat, itemName), failColor);
            return false;
        }

        SetStatus(string.Format(boughtFormat, itemName, total), successColor);
        return true;
    }

    /// <summary>Gear (nón lá, tam kiếm Zoro): one piece, paid once, worn by the chicken at once (not put into the bag).</summary>
    bool TryBuyGear(ShopEntry entry)
    {
        PetEquipment gear = Gear;
        string itemName = entry.item.displayName;

        if (gear.Owns(entry.item))
        {
            SetStatus(string.Format(alreadyOwnedFormat, itemName), failColor);
            RefreshOwned();
            return false;
        }

        if (wallet.Balance < entry.price || !wallet.TrySpend(entry.price))
        {
            SetStatus(string.Format(notEnoughFormat, itemName, entry.price, wallet.Balance), failColor);
            return false;
        }

        if (!gear.Equip(entry.item))
        {
            wallet.Add(entry.price); // defensive: nothing was worn, give the money back
            SetStatus(notReadyText, failColor);
            return false;
        }

        SetStatus(string.Format(boughtFormat, itemName, entry.price), successColor);
        RefreshOwned();
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
            rows[i].SetAffordable(balance >= PriceFor(i));
    }

    /// <summary>Sets how many pieces one Buy click buys. Clamped to 1..maxQuantity; the row prices show unit price x quantity.</summary>
public void SetQuantity(int value)
    {
        quantity = Mathf.Clamp(value, 1, Mathf.Max(1, maxQuantity));
        if (quantityLabel != null) quantityLabel.text = string.Format(quantityFormat, quantity);
        for (int i = 0; i < rows.Count && i < shown.Count; i++)
            rows[i].SetPrice(string.Format(priceFormat, PriceFor(i)));
        RefreshMoney();
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
