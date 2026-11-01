using UnityEngine;

/// <summary>
/// "Use the selected hotbar item" (key F). One small controller for the three shop goods; it reuses HotbarUI (which slot is selected),
/// InventorySystem (consume), CurrencyWallet (prize), InteractionPromptUI / MessageBannerUI (feedback). No new shop or inventory.
///
///  - Training mat (Thảm tập cho gà): stand next to the push-up station, select the mat, press E: ONE mat is consumed and the station is
///    marked as placed (TrainingStation.Place) - the mat prop appears and the trainer accepts the push-up part. Placing works once.
///  - Lottery ticket (Vé số): NOT used with F any more - tickets are redeemed at MrBeast (LotteryRedeem: guess even / odd).
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
    [Tooltip("Key that places the training mat (the other items keep using 'useKey').")]
    [SerializeField] KeyCode matPlaceKey = KeyCode.E;

    [Header("Training mat - placement cue (scrap-collector style clink)")]
    [Tooltip("3D clinking sound played at the placement spot while the player carries the mat and it is not placed yet, so the player can hear where to put it. Played through GameAudio (no extra AudioSource).")]
    [SerializeField] GameAudio.Bank matCue = new GameAudio.Bank { name = "matCue", volume = 0.55f, minInterval = 0.1f, pitchJitter = 0.15f };
    [SerializeField, Min(0.5f)] float matCueInterval = 2.5f;
    [Tooltip("The cue is only played when the player is closer than this to the placement spot.")]
    [SerializeField, Min(1f)] float matCueHearDistance = 20f;

    [Header("Protein fertilizer")]
    [SerializeField] Transform[] gardenPlots;
    [SerializeField, Min(0.5f)] float fertilizerDistance = 3f;

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
    float nextMatCueTime;

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

        TickMatCue();

        ItemDefinition selected = hotbar.SelectedItem;
        bool matReady = selected != null && selected == trainingMat && CanPlaceMatHere() && MatKeyFree();
        string promptText = null;
        if (matReady) promptText = "Nhấn E để trải thảm tập";
        else if (selected != null && selected == proteinFertilizer && NearestPlot() != null) promptText = "Nhấn F để rắc phân bón protein";
        SetPrompt(promptText != null, promptText);

        if (selected == null) return;
        if (selected == trainingMat)
        {
            // the mat uses E (not F); stays silent when out of range or when the crosshair is on something else (trainer, NPC...)
            if (matReady && Input.GetKeyDown(matPlaceKey)) TryPlaceMat();
        }
        else if (Input.GetKeyDown(useKey)) UseSelected();
    }

/// <summary>E is shared with every NPC / pickup: the mat only reacts when the crosshair is on nothing or on the placement spot itself.</summary>
    bool MatKeyFree()
    {
        return InteractObject.Aimed == null || InteractObject.IsAimed(matStation);
    }

    /// <summary>Plays a short double clink (like a scrap collector's bottles) at the placement spot every few seconds, only while it helps the player find the spot.</summary>
    void TickMatCue()
    {
        if (Time.time < nextMatCueTime) return;
        nextMatCueTime = Time.time + matCueInterval * Random.Range(0.85f, 1.15f);

        InventorySystem inv = InventorySystem.Instance;
        if (trainingMat == null || matStation == null || inv == null || player == null) return;
        if (!matStation.CanPlace || !inv.Has(trainingMat)) return;
        Vector3 offset = player.position - matStation.transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > matCueHearDistance * matCueHearDistance) return;
        if (GameAudio.Instance == null) return;

        PlayMatClink();
        Invoke(nameof(PlayMatClink), 0.22f);
    }

    void PlayMatClink()
    {
        if (GameAudio.Instance == null || matStation == null || !matStation.CanPlace) return;
        GameAudio.Instance.Play(matCue, matStation.transform.position, 1f, Random.Range(0.95f, 1.25f), 1f);
    }


    /// <summary>What F does: uses the selected hotbar item. True if something was used (an item consumed).</summary>
    public bool UseSelected()
    {
        if (hotbar == null) return false;
        ItemDefinition selected = hotbar.SelectedItem;
        if (selected == null) return false;

        if (selected == trainingMat) return TryPlaceMat();
        if (selected == lotteryTicket) { Say("Mang vé số đến MrBeast để đổi thưởng nhé!"); return false; }
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
