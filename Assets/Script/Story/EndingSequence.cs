using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// M13 - the ending. Starts ONCE after the final boss was beaten and the friend's concession dialogue
/// (FinalBossController.VictoryStoryDone) has been shown and closed:
///   1. the chicken celebrates (the existing push-up animation, the same Animator bool TrainingStation uses),
///   2. fade to black, the player and the chicken are put back at the grandparents' house,
///   3. a short funny dialogue with Ông and Bà (existing DialogueUI),
///   4. a one-line epilogue, then rolling credits and a "Thoát game" button.
/// Reuses QuestManager (quest phuc_thu_cuoi_cung must be Completed), DialogueUI, MessageBannerUI. No combat,
/// quest or story script is edited (only FinalBossController got the read-only VictoryStoryDone flag).
/// The project has no menu and no save/load, so the final state is the credits screen: the player is frozen
/// and can only quit. (Replaying by reloading the scene is NOT offered: several story flags are static and
/// would stay set.)
/// </summary>
public class EndingSequence : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] QuestManager questManager;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] MessageBannerUI banner;
    [SerializeField] QuestDefinition finalBossQuest; // phuc_thu_cuoi_cung
    [SerializeField] Transform player;               // empty = found by tag
    [SerializeField] MonoBehaviour playerMovement;
    [SerializeField] MonoBehaviour mouseLook;
    [SerializeField] Transform chicken;              // ChienKe_Pet
    [Tooltip("Where the player stands for the homecoming scene (near Ông và Bà).")]
    [SerializeField] Transform homeSpot;
    [Tooltip("Where the player looks at during the homecoming (Ông).")]
    [SerializeField] Transform lookAtAtHome;

    [Header("Timing")]
    [SerializeField, Min(0f)] float celebrateSeconds = 4f;
    [SerializeField, Min(0.1f)] float fadeSeconds = 1.2f;
    [SerializeField, Min(0f)] float blackHoldSeconds = 2f;
    [SerializeField, Min(1f)] float creditsScrollSpeed = 70f;

    [Header("Text")]
    [SerializeField] string celebrateMessage = "Gà ăn mừng chiến thắng bằng... vài chục cái chống đẩy!";
    [SerializeField] string timeSkipText = "Vài ngày sau...";
    [SerializeField, TextArea(2, 4)] string[] grandfatherLines =
    {
        "Ủa, thằng cháu IT về rồi đấy à? Nghe nói cháu hạ luôn thằng bạn thân hồi bé rồi hả?",
        "Ông đã bảo rồi, con gà nhà mình mà tập thêm tí là thành võ sư!"
    };
    [SerializeField, TextArea(2, 4)] string[] grandmotherLines =
    {
        "Nhờ bà bón phân protein cho rau đấy nhé! Gà ăn rau mới có cơ bắp thế này, công của bà cả.",
        "Thôi vào nhà ăn cơm đi, cả tuần nay chắc ăn mì gói gầy rộc cả người rồi."
    };
    [SerializeField, TextArea(2, 4)] string[] grandfatherClosingLines =
    {
        "À mà... sáng mai nhớ lên tỉnh đi làm nhé. Sếp cháu nhắn: 'Deadline sáng mai'.",
        "Cháu bảo sếp là cháu đang bận... đá gà, ông nói giúp cho."
    };
    [SerializeField, TextArea(2, 4)] string epilogue =
        "Thế là gã IT về quê nghỉ phép, không những chẳng nghỉ ngơi\nmà còn trở thành huấn luyện viên gà chiến số một làng.\n\nCòn deadline ư? Để mai tính.";
    [SerializeField] string creditsTitle = "CUỘC KÊ";
    [SerializeField, TextArea(6, 20)] string credits =
        "HẾT\n\nCảm ơn bạn đã chơi!\n\n\n" +
        "Thiết kế & lập trình\n[[Nhóm mấy quên rồi]\n\n[Nguyễn Trung Hiệp - Nguyễn Lê Anh Tuấn]]\n\n" +
        "Công cụ\nUnity 6 · Universal Render Pipeline\n\n" +
        "Mô hình, hoạt ảnh và âm thanh\n[Đồ free, sài chùa, không tốn 1 cành nào :')]\n\n" +
        "Tri ân\nÔng, Bà và tất cả những con gà trong làng\n\n\n" +
        "(Mọi nhân vật đều hư cấu. Không có con gà nào bị thương khi làm game này.)";
    [SerializeField] string quitLabel = "Thoát game";

    /// <summary>True from the moment the ending started (one-shot, this play session).</summary>
    public static bool Started { get; private set; }
    public static bool CreditsShown { get; private set; }
    public static EndingSequence Instance { get; private set; }

    GameObject overlay;
    CanvasGroup overlayGroup;
    TextMeshProUGUI centerLabel;
    GameObject creditsRoot;

    void Awake()
    {
        Instance = this;
        Started = false;
        CreditsShown = false;
    }

    void OnDestroy()
    {
        if (Instance == this) { Instance = null; Started = false; CreditsShown = false; }
    }

    void Start()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (questManager == null || dialogue == null || finalBossQuest == null || player == null || chicken == null || homeSpot == null)
            Debug.LogError("[EndingSequence] A required reference is missing.", this);
    }

    void Update()
    {
        if (Started) return;
        if (!FinalBossController.VictoryStoryDone) return;
        if (questManager == null || finalBossQuest == null || questManager.GetState(finalBossQuest.questId) != QuestState.Completed) return;
        BeginEnding();
    }

    /// <summary>Starts the ending. Safe to call twice (the second call does nothing). Public so a test can drive the same path.</summary>
    public bool BeginEnding()
    {
        if (Started) return false;
        Started = true;
        StartCoroutine(Run());
        return true;
    }

    void LockPlayer()
    {
        if (playerMovement != null) playerMovement.enabled = false;
        if (mouseLook != null) mouseLook.enabled = false;
    }

    IEnumerator WaitDialogueClosed()
    {
        while (dialogue != null && dialogue.IsOpen) yield return null;
        LockPlayer(); // DialogueUI restores the controls when it closes
    }

    IEnumerator Say(string speaker, string[] lines)
    {
        yield return WaitDialogueClosed();
        if (dialogue == null) yield break;
        dialogue.Open(speaker, lines, null, null);
        yield return null;
        yield return WaitDialogueClosed();
    }

    IEnumerator Run()
    {
        Debug.Log("[EndingSequence] Ending started.");
        LockPlayer();

        // 1. the chicken celebrates
        yield return Celebrate();

        // 2. fade out, go home
        BuildOverlay();
        yield return Fade(0f, 1f);
        centerLabel.text = timeSkipText;
        MoveToHome();
        yield return new WaitForSeconds(blackHoldSeconds);
        centerLabel.text = "";
        yield return Fade(1f, 0f);

        // 3. funny homecoming dialogue
        yield return Say("Ông", grandfatherLines);
        yield return Say("Bà", grandmotherLines);
        yield return Say("Ông", grandfatherClosingLines);

        // 4. epilogue, credits
        yield return Fade(0f, 1f);
        centerLabel.text = epilogue;
        yield return new WaitForSeconds(blackHoldSeconds * 2.5f);
        centerLabel.text = "";
        yield return RollCredits();
    }

    IEnumerator Celebrate()
    {
        if (chicken == null) yield break;
        HealthSystem hp = chicken.GetComponent<HealthSystem>();
        Animator anim = chicken.GetComponent<Animator>();
        PetCombatAI combat = chicken.GetComponent<PetCombatAI>();
        NavMeshAgent agent = chicken.GetComponent<NavMeshAgent>();
        bool canAnimate = anim != null && (hp == null || !hp.IsDead) && HasBool(anim, "IsPushing");

        if (banner != null && !string.IsNullOrEmpty(celebrateMessage)) banner.Show(celebrateMessage, celebrateSeconds + 1f);
        if (canAnimate)
        {
            if (combat != null) combat.enabled = false;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            anim.SetBool("IsPushing", true);
        }

        float t = 0f;
        while (t < celebrateSeconds) { LockPlayer(); t += Time.deltaTime; yield return null; }

        if (canAnimate)
        {
            anim.SetBool("IsPushing", false);
            if (combat != null) { combat.enabled = true; combat.ResumeAfterRecovery(); }
            else if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
        }
    }

    static bool HasBool(Animator a, string name)
    {
        foreach (AnimatorControllerParameter p in a.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Bool) return true;
        return false;
    }

    void MoveToHome()
    {
        if (homeSpot == null) return;
        Vector3 spot = homeSpot.position;
        RaycastHit hit;
        if (Physics.Raycast(spot + Vector3.up * 20f, Vector3.down, out hit, 60f, ~0, QueryTriggerInteraction.Ignore)) spot.y = hit.point.y + 1.1f;
        else spot.y += 1.1f;

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = spot;
        if (lookAtAtHome != null)
        {
            Vector3 look = lookAtAtHome.position - spot; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) player.rotation = Quaternion.LookRotation(look);
        }
        if (cc != null) cc.enabled = true;

        if (chicken != null)
        {
            Vector3 c = homeSpot.position + homeSpot.right * 1.5f - homeSpot.forward * 0.5f;
            NavMeshHit nh;
            if (NavMesh.SamplePosition(c, out nh, 4f, NavMesh.AllAreas)) c = nh.position;
            NavMeshAgent agent = chicken.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(c); else chicken.position = c;
        }
    }

    // ---------------------------------------------------------------- UI (built at runtime, like IntroScreen / StoryBed)

    void BuildOverlay()
    {
        if (overlay != null) return;
        overlay = new GameObject("EndingCanvas");
        var canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;
        var scaler = overlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        overlay.AddComponent<GraphicRaycaster>();
        overlayGroup = overlay.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.blocksRaycasts = false;

        var bgGO = new GameObject("Black");
        bgGO.transform.SetParent(overlay.transform, false);
        var bg = bgGO.AddComponent<Image>();
        bg.color = Color.black;
        Stretch(bg.rectTransform);

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(overlay.transform, false);
        centerLabel = textGO.AddComponent<TextMeshProUGUI>();
        centerLabel.fontSize = 48;
        centerLabel.alignment = TextAlignmentOptions.Center;
        centerLabel.color = Color.white;
        centerLabel.text = "";
        centerLabel.raycastTarget = false;
        Stretch(centerLabel.rectTransform);
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeSeconds)
        {
            LockPlayer();
            t += Time.deltaTime;
            overlayGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeSeconds));
            yield return null;
        }
        overlayGroup.alpha = to;
    }

    IEnumerator RollCredits()
    {
        CreditsShown = true;
        overlayGroup.blocksRaycasts = true; // nothing underneath can be clicked any more
        ParkPlayerAway(); // no NPC prompt / dialogue can open under the credits
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        creditsRoot = new GameObject("Credits");
        creditsRoot.transform.SetParent(overlay.transform, false);

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(creditsRoot.transform, false);
        var title = titleGO.AddComponent<TextMeshProUGUI>();
        title.text = creditsTitle;
        title.fontSize = 96;
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(1f, 0.85f, 0.3f);
        title.raycastTarget = false;
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        title.rectTransform.sizeDelta = new Vector2(1400f, 140f);

        var bodyGO = new GameObject("Body");
        bodyGO.transform.SetParent(creditsRoot.transform, false);
        var body = bodyGO.AddComponent<TextMeshProUGUI>();
        body.text = credits;
        body.fontSize = 40;
        body.alignment = TextAlignmentOptions.Top;
        body.color = new Color(0.92f, 0.92f, 0.92f);
        body.raycastTarget = false;
        body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        body.rectTransform.pivot = new Vector2(0.5f, 1f);
        body.rectTransform.sizeDelta = new Vector2(1400f, 10f);
        body.ForceMeshUpdate();
        float bodyHeight = Mathf.Max(100f, body.preferredHeight);
        body.rectTransform.sizeDelta = new Vector2(1400f, bodyHeight);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);

        float y = -640f; // title starts below the bottom edge (reference height 1080, centre anchor)
        float titleY = y;
        float bodyOffset = 200f;
        while (titleY < 100000f) // bounded; the break below ends it long before
        {
            titleY += creditsScrollSpeed * Time.deltaTime;
            title.rectTransform.anchoredPosition = new Vector2(0f, titleY);
            body.rectTransform.anchoredPosition = new Vector2(0f, titleY - bodyOffset);
            if (titleY - bodyOffset - bodyHeight > 540f) break; // everything has left through the top
            yield return null;
        }

        creditsRoot.SetActive(false);
        centerLabel.text = creditsTitle + "\n\nCảm ơn bạn đã chơi!";
        BuildQuitButton();
        Debug.Log("[EndingSequence] Credits finished - waiting for the player to quit.");
    }

    void ParkPlayerAway()
    {
        if (player == null) return;
        LockPlayer();
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = new Vector3(5000f, 200f, 5000f); // far from every NPC trigger; the player is frozen anyway
        if (cc != null) cc.enabled = true;
        // freeze() keeps gravity from running: PlayerMovement is disabled
    }

    void BuildQuitButton()
    {
        var btnGO = new GameObject("QuitButton");
        btnGO.transform.SetParent(overlay.transform, false);
        var img = btnGO.AddComponent<Image>();
        img.color = new Color(0.65f, 0.2f, 0.2f);
        var btn = btnGO.AddComponent<Button>();
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.4f, 0.06f);
        rt.anchorMax = new Vector2(0.6f, 0.14f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(btnGO.transform, false);
        var lbl = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.text = quitLabel;
        lbl.fontSize = 34;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = Color.white;
        lbl.raycastTarget = false;
        Stretch(lbl.rectTransform);

        btn.onClick.AddListener(Quit);
    }

    /// <summary>The credits button. Public so a test can drive the same path.</summary>
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
