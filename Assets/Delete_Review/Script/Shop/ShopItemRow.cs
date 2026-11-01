using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row of the shop list: icon, name, short description, price and a Buy button.
/// ShopUI clones a template row per catalog entry and calls Bind(). The row only DISPLAYS; buying is ShopUI's job.
/// </summary>
public class ShopItemRow : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Text descriptionLabel;
    [SerializeField] TMP_Text priceLabel;
    [SerializeField] TMP_Text buyLabel;
    [SerializeField] Button buyButton;

    [Header("Price colour")]
    [SerializeField] Color affordableColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] Color tooExpensiveColor = new Color(1f, 0.45f, 0.4f, 1f);

    public Button BuyButton => buyButton;

    /// <summary>Fills the row. If the item has no sprite the placeholder colour square is shown (same as the hotbar).</summary>
    public void Bind(ItemDefinition item, string description, string priceText, string buyText)
    {
        if (icon != null)
        {
            icon.enabled = true;
            icon.sprite = item.icon;
            icon.color = item.icon != null ? Color.white : item.placeholderColor;
        }
        if (nameLabel != null) nameLabel.text = item.displayName;
        if (descriptionLabel != null) descriptionLabel.text = description;
        if (priceLabel != null) priceLabel.text = priceText;
        if (buyLabel != null) buyLabel.text = buyText;
    }

    /// <summary>Colours the price red when the player cannot afford it. The button stays clickable on purpose:
    /// clicking then shows the 'not enough money' message instead of failing silently.</summary>
    public void SetAffordable(bool affordable)
    {
        if (priceLabel != null) priceLabel.color = affordable ? affordableColor : tooExpensiveColor;
    }

    /// <summary>M10 (Reward NPC): fills the row without an item, e.g. a money reward. The icon is hidden.</summary>
    public void BindText(string name, string description, string priceText, string buyText)
    {
        if (icon != null) icon.enabled = false;
        if (nameLabel != null) nameLabel.text = name;
        if (descriptionLabel != null) descriptionLabel.text = description;
        if (priceLabel != null) priceLabel.text = priceText;
        if (buyLabel != null) buyLabel.text = buyText;
    }

    /// <summary>M10: changes the button text and whether it can be clicked (used to show a reward as already claimed).</summary>
    public void SetButton(string buyText, bool interactable)
    {
        if (buyLabel != null) buyLabel.text = buyText;
        if (buyButton != null) buyButton.interactable = interactable;
    }
}
