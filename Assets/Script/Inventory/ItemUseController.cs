using UnityEngine;

/// <summary>
/// "Use the selected hotbar item" (key F). One small controller for the three shop goods; it reuses HotbarUI (which slot is selected),
/// InventorySystem (consume), CurrencyWallet (prize), InteractionPromptUI / MessageBannerUI (feedback). No new shop or inventory.
///
///  - Training mat (Thảm tập cho gà): stand next to the push-up station, select the mat, press F: ONE mat is consumed and the station is
///    marked as placed (TrainingStation.Place) - the mat prop appears and the trainer accepts the push-up part. Placing works once.
///  - Lottery ticket (Vé số): select it, press F anywhere: ONE ticket is consumed FIRST, then a prize is rolled from prizeAmounts /
///    prizeWeights (expected value stays below the ticket price) and paid with wallet.Add. A press can never pay without using a ticket.
///  - Protein fertilizer (Phân bón protein): select it, press F next to a vegetable plot: ONE bag is consumed, short water-style effect and
///    a message. It does NOT change the garden quest (that quest keeps its own sack) - the real gameplay effect is still to be designed.
///
/// Keys and inventory opening are ignored while a dialogue, the shop, the old inventory screen or the trainer window is open.
/// </summary>
public class ItemUseController : MonoBehaviour
{
    [Header("Items")]
    [SerializeField] ItemDefinition trainingMat;
    [SerializeField] ItemDefinition lotteryTicket;
    [SerializeField] ItemDefinition proteinFertilizer;

    [Header("Input")]
    [SerializeField] KeyCode useKey = KeyCode.F;

    [Header("Training mat")]
    [SerializeField] TrainingStation matStation;
    [SerializeField, Min(0.5f)] float matPlaceDistance = 3.5f;

    [Header("Protein fertilizer")]
    [SerializeField] Transform[] gardenPlots;
    [SerializeField, Min(0.5f)] float fertilizerDistance = 3f;

    [Header("Lottery (prizeAmounts[i] is won with chance prizeWeights[i] / sum)")]
    [SerializeField] int[] prizeAmounts = { 0, 20, 50, 200, 1000 };
    [SerializeField] float[] prizeWeights = { 70f, 20f, 7f, 2.5f, 0.5f };

    [Header("Scene references")]
    [SerializeField] HotbarUI hotbar;
    [SerializeField] CurrencyWallet wallet;
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] MessageBannerUI banner;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] ShopUI shop;
    [SerializeField] TrainerNPC trainer;
    [Tooltip("Optional. If empty, the GameObject tagged 'Player' is used.")]
    [SerializeField] Transform player;

    bool promptShown;

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (hotbar == null || wallet == null || player == null)
            Debug.LogError("[ItemUseController] HotbarUI / CurrencyWallet / Player reference missing.", this);
    }

    void OnDisable()
    {
        SetPrompt(false, null);
    }

    void Update()
    {
        if (hotbar == null || player == null) return;

        if (IsBlocked()) { SetPrompt(false, null); return; }

        ItemDefinition selected = hotbar.SelectedItem;
        string promptText = null;
        if (selected != null && selected == trainingMat && CanPlaceMatHere()) promptText = "Nhấn F để trải thảm tập";
        else if (selected != null && selected == proteinFertilizer && NearestPlot() != null) promptText = "Nhấn F để rắc phân bón protein";
        SetPrompt(promptText != null, promptText);

        if (selected != null && Input.GetKeyDown(useKey)) UseSelected();
    }

    /// <summary>What F does: uses the selected hotbar item. True if something was used (an item consumed).</summary>
    public bool UseSelected()
    {
        if (hotbar == null) return false;
        ItemDefinition selected = hotbar.SelectedItem;
        if (selected == null) return false;

        if (selected == trainingMat) return TryPlaceMat();
        if (selected == lotteryTicket) return TryUseTicket();
        if (selected == proteinFertilizer) return TryUseFertilizer();
        return false; // other items have no use yet
    }

    // ---------------------------------------------------------------- training mat

    bool CanPlaceMatHere()
    {
        if (matStation == null || !matStation.CanPlace || player == null) return false;
        Vector3 offset = player.position - matStation.transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= matPlaceDistance * matPlaceDistance;
    }

    public bool TryPlaceMat()
    {
        InventorySystem inv = InventorySystem.Instance;
        if (trainingMat == null || matStation == null || inv == null) return false;
        if (!matStation.CanPlace) { Say("Thảm tập đã được trải ở khu tập rồi."); return false; }
        if (!inv.Has(trainingMat)) return false;
        if (!CanPlaceMatHere()) { Say("Hãy đứng cạnh chỗ hít đất ở khu tập để trải thảm."); return false; }

        if (!inv.Remove(trainingMat, 1)) return false; // consume first, then place (once)
        if (!matStation.Place())
        {
            inv.Add(trainingMat, 1); // defensive: nothing was placed, give the mat back
            return false;
        }
        Say("Đã trải thảm tập ở khu tập. Gà có thể hít đất rồi!");
        return true;
    }

    // ---------------------------------------------------------------- lottery

    /// <summary>The prize for a roll in [0,1) (0 = the first prize in the table). Public so the table can be tested without randomness.</summary>
    public int PrizeForRoll(float roll01)
    {
        if (prizeAmounts == null || prizeWeights == null) return 0;
        int n = Mathf.Min(prizeAmounts.Length, prizeWeights.Length);
        float total = 0f;
        for (int i = 0; i < n; i++) total += Mathf.Max(0f, prizeWeights[i]);
        if (total <= 0f) return 0;

        float target = Mathf.Clamp01(roll01) * total;
        float acc = 0f;
        for (int i = 0; i < n; i++)
        {
            acc += Mathf.Max(0f, prizeWeights[i]);
            if (target < acc) return Mathf.Max(0, prizeAmounts[i]);
        }
        return Mathf.Max(0, prizeAmounts[n - 1]);
    }

    public bool TryUseTicket()
    {
        InventorySystem inv = InventorySystem.Instance;
        if (lotteryTicket == null || wallet == null || inv == null) return false;
        if (!inv.Has(lotteryTicket)) return false;

        if (!inv.Remove(lotteryTicket, 1)) return false; // the ticket is used up BEFORE any prize exists
        int prize = PrizeForRoll(Random.value);
        if (prize > 0)
        {
            wallet.Add(prize);
            Say(string.Format("Cào vé số: TRÚNG {0} tiền!", prize));
        }
        else Say("Cào vé số: trượt rồi, chúc may mắn lần sau!");
        return true;
    }

    // ---------------------------------------------------------------- protein fertilizer

    Transform NearestPlot()
    {
        if (gardenPlots == null || player == null) return null;
        Transform best = null;
        float bestSqr = fertilizerDistance * fertilizerDistance;
        foreach (Transform plot in gardenPlots)
        {
            if (plot == null) continue;
            Vector3 offset = player.position - plot.position;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            if (sqr <= bestSqr) { bestSqr = sqr; best = plot; }
        }
        return best;
    }

    public bool TryUseFertilizer()
    {
        InventorySystem inv = InventorySystem.Instance;
        if (proteinFertilizer == null || inv == null || !inv.Has(proteinFertilizer)) return false;

        Transform plot = NearestPlot();
        if (plot == null) { Say("Hãy đứng cạnh luống rau để rắc phân bón."); return false; }

        if (!inv.Remove(proteinFertilizer, 1)) return false;
        GameVfx.Water(plot.position + Vector3.up * 0.3f);
        Say("Đã rắc phân bón protein lên luống rau.");
        return true;
    }

    // ---------------------------------------------------------------- helpers

    bool IsBlocked()
    {
        if (dialogue != null && dialogue.IsOpen) return true;
        if (shop != null && shop.IsOpen) return true;
        if (trainer != null && trainer.IsOpen) return true;
        return InventorySystem.Instance != null && InventorySystem.Instance.isOpen;
    }

    void Say(string message)
    {
        if (banner != null) banner.Show(message);
    }

    void SetPrompt(bool visible, string text)
    {
        if (prompt == null) return;
        if (visible)
        {
            prompt.Show(this, text); // same owner + new text just updates the label
            promptShown = true;
        }
        else if (promptShown)
        {
            prompt.Hide(this);
            promptShown = false;
        }
    }
}
