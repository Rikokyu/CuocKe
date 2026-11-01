using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The trainer NPC (replaces paying each training station separately). E next to him opens a small window: the player picks a
/// number of hours, pays up front (CurrencyWallet.TrySpend, once) and the chicken does the circuit run -> push-up -> boxing on the
/// three existing TrainingStation props, each exercise exactly 1/3 of an hour; one hour = secondsPerHour seconds. Every COMPLETED
/// hour adds +damagePerTrainingHour damage through ChickenStats.AddTrainingHours (one Changed event per session = one 'buổi tập'
/// for the rematch quest).
///
/// Price: hour k costs firstHourCost + (k-1) * extraHourIncrease, so the total grows faster than the number of hours.
/// Money safety, in this order: window must be open, no session running, story gate / chicken awake / not in battle / required
/// items (the push-up weights) / money - then ONE TrySpend. If TrySpend fails nothing else happens (no session, no hours).
/// A session cut short (chicken dies, battle starts, NPC disabled) refunds the cost of the hours that were NOT completed.
/// Window: closed with Esc / button; while open the player behaviours listed in disableWhileOpen are frozen and the cursor is freed.
/// </summary>
public class TrainerNPC : MonoBehaviour
{
    [Header("Price (hour k costs first + (k-1) * extra)")]
    [SerializeField, Min(1)] int firstHourCost = 150;
    [SerializeField, Min(0)] int extraHourIncrease = 50;
    [SerializeField, Range(1, 8)] int maxHoursPerSession = 5;

    [Header("Training time")]
    [Tooltip("Real seconds of ONE paid hour. It is split into equal thirds: run, push-up, boxing.")]
    [SerializeField, Min(3f)] float secondsPerHour = 12f;
    [Tooltip("Circuit order. Each station gets exactly 1/3 of every hour.")]
    [SerializeField] TrainingStation[] circuit;

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] float interactionDistance = 2.2f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] KeyCode closeKey = KeyCode.Escape;
    [SerializeField] string promptText = "Nhấn E để nói chuyện với Huấn luyện viên";
    [SerializeField, TextArea(2, 3)] string greeting = "Muốn gà khỏe thì phải tập! Chọn số giờ, trả tiền trước, tập càng lâu càng đắt.";
    [Tooltip("Optional. Turned toward the player when the window opens.")]
    [SerializeField] Transform modelRoot;
    [SerializeField] Transform player;

    [Header("Scene references")]
    [SerializeField] ChickenStats stats;
    [SerializeField] CurrencyWallet wallet;
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] MessageBannerUI banner;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] ShopUI shop;
    [Header("Frozen while the window is open (same list as the shop: movement, mouse look, hotbar, inventory key)")]
    [SerializeField] Behaviour[] disableWhileOpen;

    // ---- state
    GameObject canvasRoot;
    TextMeshProUGUI infoLabel;
    TextMeshProUGUI statusLabel;
    readonly List<Button> hourButtons = new List<Button>();
    readonly List<TextMeshProUGUI> hourLabels = new List<TextMeshProUGUI>();
    bool[] wasEnabled;
    CursorLockMode previousLockState;
    bool previousCursorVisible;
    int openedOnFrame = -1;
    bool promptShown;
    Coroutine routine;
    int bookedHours;
    int completedHours;
    int paidTotal;

    public bool IsOpen { get; private set; }
    public bool IsSessionActive { get; private set; }
    public int MaxHours => maxHoursPerSession;

    /// <summary>The Button of the 'hours' option (1-based), or null. Lets a test press the real button.</summary>
    public Button GetHourButton(int hours) { return hours >= 1 && hours <= hourButtons.Count ? hourButtons[hours - 1] : null; }

    /// <summary>Total price of 'hours' hours: sum over k = 1..hours of first + (k-1) * extra.</summary>
    public int CostForHours(int hours)
    {
        int total = 0;
        for (int k = 1; k <= hours; k++) total += firstHourCost + (k - 1) * extraHourIncrease;
        return total;
    }

    /// <summary>Price of the k-th hour alone.</summary>
    public int CostOfHour(int k) { return firstHourCost + (k - 1) * extraHourIncrease; }

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null || stats == null || wallet == null || circuit == null || circuit.Length == 0)
            Debug.LogError("[TrainerNPC] Player / ChickenStats / CurrencyWallet / circuit reference missing.", this);
        foreach (TrainingStation s in circuit)
            if (s == null) Debug.LogError("[TrainerNPC] A circuit station is missing.", this);
    }

    void OnEnable()
    {
        if (wallet != null) wallet.BalanceChanged += OnBalanceChanged;
    }

    void OnDisable()
    {
        if (wallet != null) wallet.BalanceChanged -= OnBalanceChanged;
        SetPrompt(false);
        Close();
        if (IsSessionActive) AbortSession(); // never leave the chicken taken over or the money lost
    }

    void Update()
    {
        if (player == null || stats == null) return;

        if (IsOpen)
        {
            if (Time.frameCount != openedOnFrame && Input.GetKeyDown(closeKey)) Close();
            return;
        }

        bool canInteract = IsPlayerInRange() && !IsSessionActive && !IsBlockedByUI();
        SetPrompt(canInteract);
        if (canInteract && Input.GetKeyDown(interactKey)) Open();
    }

    // ---------------------------------------------------------------- window

    /// <summary>Opens the hour window. False if it cannot open now.</summary>
    public bool Open()
    {
        if (IsOpen || IsSessionActive || IsBlockedByUI()) return false;
        if (canvasRoot == null) BuildUI();

        IsOpen = true;
        openedOnFrame = Time.frameCount;
        SetPrompt(false);
        FreezePlayer();
        FacePlayer();
        canvasRoot.SetActive(true);
        RefreshWindow();
        SetStatus(greeting, Color.white);
        return true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (canvasRoot != null) canvasRoot.SetActive(false);
        RestorePlayer();
    }

    void OnBalanceChanged(int balance)
    {
        if (IsOpen) RefreshWindow();
    }

    void RefreshWindow()
    {
        if (infoLabel != null && wallet != null && stats != null)
            infoLabel.text = string.Format("Tiền: {0}      Tổng giờ tập: {1}      Sát thương gà: {2}", wallet.Balance, stats.TrainingHours, Mathf.RoundToInt(stats.Damage));
        for (int i = 0; i < hourButtons.Count; i++)
        {
            int hours = i + 1;
            int cost = CostForHours(hours);
            bool afford = wallet != null && wallet.CanAfford(cost);
            hourButtons[i].interactable = afford && !IsSessionActive;
            hourLabels[i].text = string.Format("{0} giờ  -  {1} tiền   (+{2} sát thương){3}", hours, cost, Mathf.RoundToInt(hours * stats.DamagePerTrainingHour), afford ? "" : "   [thiếu tiền]");
        }
    }

    // ---------------------------------------------------------------- booking

    /// <summary>The reason a booking cannot start (null = it can). Money is checked last.</summary>
    string Refusal(int hours)
    {
        if (!ChickenTransformationTrigger.TrainingUnlocked) return ChickenTransformationTrigger.TrainingLockedMessage;
        if (BattleManager.Instance != null && BattleManager.Instance.IsBattleActive) return "Đang trong trận đấu, chưa thể tập.";
        MainChickenAcquire acquire = MainChickenAcquire.Instance;
        if (acquire != null && !acquire.IsAcquired) return "Gà chưa tỉnh dậy, chưa thể tập.";
        HealthSystem health = stats.GetComponent<HealthSystem>();
        if (health != null && health.IsDead) return "Gà đang gục, chưa thể tập.";
        if (TrainingStation.IsBusy) return "Gà đang bận tập, đợi chút.";

        foreach (TrainingStation s in circuit)
        {
            if (s == null) return "Khu tập chưa sẵn sàng.";
            string problem = s.RequirementProblem();
            if (problem != null) return problem + ". Tiền không bị trừ.";
        }

        int cost = CostForHours(hours);
        if (wallet == null || !wallet.CanAfford(cost)) return string.Format("Không đủ tiền: {0} giờ cần {1}, bạn có {2}.", hours, cost, wallet != null ? wallet.Balance : 0);
        return null;
    }

    /// <summary>
    /// Books 'hours' hours: checks everything, pays ONCE, closes the window and starts the session. Returns true only if the money was
    /// spent and the session began. A stale call (window closed / session already running) can never charge.
    /// </summary>
    public bool TryBook(int hours)
    {
        if (!IsOpen || IsSessionActive) return false;
        if (hours < 1 || hours > maxHoursPerSession) return false;

        string why = Refusal(hours);
        if (why != null) { SetStatus(why, new Color(1f, 0.5f, 0.45f)); return false; }

        int cost = CostForHours(hours);
        if (!wallet.TrySpend(cost)) { SetStatus("Thanh toán thất bại, không có gì thay đổi.", new Color(1f, 0.5f, 0.45f)); return false; }

        IsSessionActive = true; // set in the same frame as the payment: no second booking is possible
        bookedHours = hours;
        completedHours = 0;
        paidTotal = cost;
        Close();
        routine = StartCoroutine(RunSession());
        return true;
    }

    IEnumerator RunSession()
    {
        TrainingStation first = circuit[0];
        if (!first.BeginDriven())
        {
            // could not take over the chicken: undo the payment, nothing was trained
            wallet.Add(paidTotal);
            if (banner != null) banner.Show("Gà đang bận, đã hoàn tiền.");
            IsSessionActive = false;
            paidTotal = 0;
            yield break;
        }

        float perExercise = secondsPerHour / circuit.Length;
        int damageBefore = Mathf.RoundToInt(stats.Damage);
        bool aborted = false;

        for (int hour = 1; hour <= bookedHours && !aborted; hour++)
        {
            for (int i = 0; i < circuit.Length; i++)
            {
                if (banner != null) banner.Show(string.Format("Giờ {0}/{1}: {2}", hour, bookedHours, circuit[i].DisplayName), perExercise + 0.5f);
                yield return circuit[i].PerformDriven(perExercise);
                if (!circuit[i].LastExerciseCompleted) { aborted = true; break; }
            }
            if (!aborted) completedHours++;
        }

        FinishSession(damageBefore, aborted);
    }

    void FinishSession(int damageBefore, bool aborted)
    {
        circuit[0].EndDriven();

        int refund = 0;
        for (int k = completedHours + 1; k <= bookedHours; k++) refund += CostOfHour(k); // hours that were paid but not done
        if (refund > 0) wallet.Add(refund);
        if (completedHours > 0) stats.AddTrainingHours(completedHours); // once: one session = one Changed event

        int paidNet = paidTotal - refund;
        string message = completedHours > 0
            ? string.Format("Tập xong {0} giờ! Sát thương {1} → {2}  |  Tổng giờ tập: {3}  (-{4} tiền)", completedHours, damageBefore, Mathf.RoundToInt(stats.Damage), stats.TrainingHours, paidNet)
            : "Buổi tập bị gián đoạn, đã hoàn tiền.";
        if (aborted && completedHours > 0) message += string.Format("  [bị gián đoạn, hoàn {0} tiền]", refund);
        if (banner != null) banner.Show(message, 7f);
        Debug.Log("[Trainer] " + message);

        IsSessionActive = false;
        routine = null;
        paidTotal = 0;
    }

    /// <summary>Emergency stop (component disabled mid-session): same bookkeeping as an interrupted session.</summary>
    void AbortSession()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (circuit != null && circuit.Length > 0 && circuit[0] != null) FinishSession(Mathf.RoundToInt(stats != null ? stats.Damage : 0f), true);
        else IsSessionActive = false;
    }

    // ---------------------------------------------------------------- helpers

    bool IsPlayerInRange()
    {
        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= interactionDistance * interactionDistance;
    }

    bool IsBlockedByUI()
    {
        if (dialogue != null && dialogue.IsOpen) return true;
        if (shop != null && shop.IsOpen) return true;
        return InventorySystem.Instance != null && InventorySystem.Instance.isOpen;
    }

    void SetPrompt(bool visible)
    {
        if (prompt == null || visible == promptShown) return;
        promptShown = visible;
        if (visible) prompt.Show(this, promptText);
        else prompt.Hide(this);
    }

    void FacePlayer()
    {
        if (player == null) return;
        Transform t = modelRoot != null ? modelRoot : transform;
        Vector3 toPlayer = player.position - t.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(toPlayer);
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
        for (int i = 0; i < count; i++)
        {
            Behaviour b = disableWhileOpen[i];
            if (b != null && wasEnabled != null && i < wasEnabled.Length) b.enabled = wasEnabled[i];
        }
        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
    }

    void SetStatus(string text, Color color)
    {
        if (statusLabel == null) return;
        statusLabel.text = text;
        statusLabel.color = color;
    }

    // ---------------------------------------------------------------- UI (built at runtime, like IntroScreen / EndingSequence)

    void BuildUI()
    {
        canvasRoot = new GameObject("TrainerCanvas");
        var canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;
        var scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasRoot.AddComponent<GraphicRaycaster>();

        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasRoot.transform, false);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.1f, 0.12f, 0.94f);
        var prt = panelImg.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(900f, 720f);

        MakeLabel(panelGO.transform, "Huấn luyện viên - Tập luyện cho gà", 44, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f), new Vector2(0f, 300f), new Vector2(860f, 70f));
        infoLabel = MakeLabel(panelGO.transform, "", 28, TextAlignmentOptions.Center, Color.white, new Vector2(0f, 235f), new Vector2(860f, 50f));
        MakeLabel(panelGO.transform, "Mỗi giờ gà chạy bộ, hít đất và đấm bao cát (mỗi môn 1/3 giờ). Mỗi giờ +7 sát thương.", 24, TextAlignmentOptions.Center, new Color(0.8f, 0.8f, 0.8f), new Vector2(0f, 185f), new Vector2(860f, 60f));

        float y = 110f;
        for (int h = 1; h <= maxHoursPerSession; h++)
        {
            int hours = h;
            var bGO = new GameObject("Hours_" + h);
            bGO.transform.SetParent(panelGO.transform, false);
            var img = bGO.AddComponent<Image>();
            img.color = new Color(0.2f, 0.5f, 0.3f);
            var btn = bGO.AddComponent<Button>();
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(780f, 60f);
            rt.anchoredPosition = new Vector2(0f, y);
            var cb = btn.colors; cb.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.9f); btn.colors = cb;
            btn.onClick.AddListener(() => TryBook(hours));
            hourButtons.Add(btn);
            hourLabels.Add(MakeLabel(bGO.transform, "", 28, TextAlignmentOptions.Center, Color.white, Vector2.zero, new Vector2(760f, 56f)));
            y -= 72f;
        }

        statusLabel = MakeLabel(panelGO.transform, "", 26, TextAlignmentOptions.Center, Color.white, new Vector2(0f, -285f), new Vector2(860f, 80f));

        var cGO = new GameObject("CloseButton");
        cGO.transform.SetParent(panelGO.transform, false);
        var cImg = cGO.AddComponent<Image>();
        cImg.color = new Color(0.6f, 0.2f, 0.2f);
        var cBtn = cGO.AddComponent<Button>();
        var crt = cImg.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(240f, 56f);
        crt.anchoredPosition = new Vector2(0f, -335f);
        MakeLabel(cGO.transform, "Đóng (Esc)", 26, TextAlignmentOptions.Center, Color.white, Vector2.zero, new Vector2(230f, 50f));
        cBtn.onClick.AddListener(Close);

        canvasRoot.SetActive(false);
    }

    static TextMeshProUGUI MakeLabel(Transform parent, string text, float size, TextAlignmentOptions align, Color color, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        var rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        return tmp;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
