using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Story beat at the grandparents' house, played by "Ông" (NPC_Grandfather). Uses only the existing systems:
/// QuestManager / QuestDefinition, DialogueUI, InteractionPromptUI and MainChickenAcquire.
///
/// Order (nothing completes by itself - every step needs the player to walk here and press E):
///   1. First talk  -> "Về nhà thăm ông bà" (Quest 1) is completed, "Nhận nuôi chú gà" is offered (HUD shows it).
///                     Ông greets and says he will cook the chicken (the required line).
///   2. Button "Xin ông giữ lại con gà" -> the rescue quest is completed (once) and the chicken is woken through
///                     MainChickenAcquire.WakeUp(); it starts following when its own wake-up animation ends.
///   3. Ông agrees (second short dialogue). Later talks only show idle lines.
///   Bà's package is NOT handed out here: the quest guide points to Bà, and she gives the delivery quest when talked to
///   (TryHandleGrandmother, called by her QuestNPC). The friend only comes after the delivery.
/// If the dialogue is closed before the button, nothing is lost: talk again.
/// </summary>
public class GrandfatherChickenRescue : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] float interactionDistance = 4f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string promptText = "Nhấn E để nói chuyện với Ông";
    [SerializeField] Transform player;

    [Header("Scene references")]
    [SerializeField] DialogueUI dialogue;
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] QuestManager questManager;
    [SerializeField] QuestDefinition questArrive;  // ve_nha_tham_ong_ba
    [SerializeField] QuestDefinition questRescue;  // nhan_nuoi_chu_ga
    [Tooltip("The chicken lying in the yard. Empty = MainChickenAcquire.Instance.")]
    [SerializeField] MainChickenAcquire chicken;

    [Header("Endgame (choice 'Đồng ý': Ông cooks the chicken -> video -> quit)")]
    [Tooltip("Played full-screen when the Player agrees. Assets/Art/UI/video/endgame.mp4. The game quits when it ends (Editor: Play Mode stops).")]
    [SerializeField] VideoClip endgameVideo;
    [SerializeField] string agreeCookLabel = "Đồng ý";
    [Tooltip("Played right after 'Đồng ý', before the video. A line that starts with '[Player] ' is spoken by the Player, any other line by Ông.")]
    [SerializeField, TextArea(2, 4)] string[] agreeCookLines =
    {
        "[Player] Liền luôn ông anh, tôi đói rồi",
        "Thằng này láo"
    };
    [SerializeField] string agreeContinueLabel = "Tiếp tục";
    [SerializeField, Min(1f)] float endgamePrepareTimeout = 8f;
    [Tooltip("Extra seconds allowed after the clip length before quitting if the video never reports its end.")]
    [SerializeField, Min(1f)] float endgamePlaybackSlack = 4f;
    [SerializeField, Range(0f, 1f)] float endgameVolume = 1f;

    [Header("Bà's delivery quest (given when the Player talks to Bà after Ông kept the chicken)")]
    [Tooltip("village_delivery. NOT started when Ông agrees: the guide points to Bà, and she hands it out from a button when talked to. Empty = she hands out nothing.")]
    [SerializeField] QuestDefinition deliveryQuest;
    [Tooltip("NPC_Grandmother (the QuestNPC calls TryHandleGrandmother(transform) when talked to).")]
    [SerializeField] Transform grandmother;
    [SerializeField] string grandmaSpeaker = "Bà";
    [SerializeField] string acceptTaskLabel = "Nhận nhiệm vụ";
    [Tooltip("{0} = the reward money.")]
    [SerializeField, TextArea(2, 4)] string[] grandmaTaskLines =
    {
        "Cháu ơi, khoan đã! Bà có việc nhờ cháu.",
        "Gói hàng để cạnh nhà ông bà đấy. Cháu mang qua cho thằng bạn thuở nhỏ của cháu giúp bà nhé.",
        "Giao xong thì quay lại gặp bà, bà trả {0} VNĐ tiền công."
    };

    [Header("Dialogue")]
    [SerializeField, TextArea(2, 4)] string[] greetLines =
    {
        "A, cháu về rồi đấy à! Lâu lắm mới thấy mặt.",
        "Để ông mừng thịt con gà cho chúng ta cùng ăn nhé!"
    };
    [SerializeField] string keepButtonLabel = "Xin ông giữ lại con gà";
    [SerializeField, TextArea(2, 4)] string[] agreeLines =
    {
        "Thôi được, cháu đã xin vậy thì ông chiều cháu. Con gà là của cháu rồi đấy!",
        "Nó đang tỉnh dậy kìa. Chăm nó cho tử tế nhé."
    };
    [SerializeField, TextArea(2, 4)] string[] idleLines =
    {
        "Con gà đó giờ là của cháu, nhớ chăm cho nó khoẻ nhé."
    };

    public static GrandfatherChickenRescue Instance { get; private set; }

    bool promptShown;

    // Endgame state
    bool endingStarted;
    bool musicPausedForEnding;
    GameObject endgameCanvas;
    RawImage endgameImage;
    VideoPlayer endgamePlayer;
    RenderTexture endgameTexture;
    bool endgameEnded;
    bool endgameFailed;
    string endgameError;

    void Awake() { Instance = this; }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        CleanupEndgame();
        if (endingStarted)
        {
            Time.timeScale = 1f; // never leave the world frozen (e.g. when leaving Play Mode)
            if (musicPausedForEnding && GameAudio.Instance != null) GameAudio.Instance.SetMusicPaused(false);
        }
    }

    void Start()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null) Debug.LogError("[GrandfatherChickenRescue] No Player found.", this);
        if (dialogue == null || prompt == null || questManager == null || questArrive == null || questRescue == null)
            Debug.LogError("[GrandfatherChickenRescue] Missing a required reference.", this);
    }

    void Update()
    {
        if (player == null || dialogue == null) return;
        bool canInteract = !endingStarted && InRange() && !dialogue.IsOpen && InteractObject.IsAimed(this);
        SetPrompt(canInteract);
        if (canInteract && Input.GetKeyDown(interactKey)) Interact();
    }

    /// <summary>Called by E in range. Public so a test can drive the same path.</summary>
    public void Interact()
    {
        if (endingStarted || dialogue == null || dialogue.IsOpen || questManager == null) return;

        // Morning after the garden quest: the chicken is missing, Ông answers that question first (MissingChickenStory). Otherwise this is false.
        // After the watering: Ông offers the feeding quest, reminds, and answers the nap request (GardenFlowStory). False otherwise.
        if (GardenFlowStory.Instance != null && GardenFlowStory.Instance.TryHandleGrandparent(transform)) { SetPrompt(false); return; }
        if (MissingChickenStory.Instance != null && MissingChickenStory.Instance.TryHandleGrandparent(transform)) { SetPrompt(false); return; }
        // After the tournament: Ông tells the player to rest, and answers 'where is my chicken' the second time (ZoroReunionStory). False otherwise.
        if (ZoroReunionStory.Instance != null && ZoroReunionStory.Instance.TryHandleGrandparent(transform)) { SetPrompt(false); return; }


        // 1) Arrival: the player really reached Ông and talks to him -> Quest 1 is done (once).
        QuestState arrive = questManager.GetState(questArrive.questId);
        if (arrive == QuestState.NotStarted) questManager.AcceptQuest(questArrive); // safety: intro was skipped
        if (questManager.GetState(questArrive.questId) == QuestState.Active)
        {
            questManager.ReportProgress(questArrive.objectiveKey, 1);
            questManager.TurnIn(questArrive);
        }

        // 2) The rescue quest becomes active now (HUD: ask Ông to keep the chicken).
        if (questManager.GetState(questRescue.questId) == QuestState.NotStarted)
            questManager.AcceptQuest(questRescue);

        bool rescued = questManager.GetState(questRescue.questId) == QuestState.Completed;
        if (rescued) dialogue.Open("Ông", idleLines, null, null);
        else dialogue.Open("Ông", greetLines, keepButtonLabel, OnAskToKeep, agreeCookLabel, OnAgreeToCook); // two exclusive choices: keep the chicken / agree to cook it

        SetPrompt(false);
    }

    void OnAskToKeep()
    {
        if (endingStarted || questManager == null) return;

        // flip first: TurnIn only works once, so a double press cannot repeat any of this
        if (questManager.GetState(questRescue.questId) == QuestState.Active)
        {
            questManager.ReportProgress(questRescue.objectiveKey, 1);
            questManager.TurnIn(questRescue);
        }

        MainChickenAcquire mc = chicken != null ? chicken : MainChickenAcquire.Instance;
        if (mc != null) mc.WakeUp(); // only acts while the chicken is Down; it follows once the wake-up animation ends
        else Debug.LogWarning("[GrandfatherChickenRescue] No MainChickenAcquire - the chicken cannot be woken.", this);

        // Nothing else opens here: the guide now points to Bà, who gives the delivery quest when talked to (TryHandleGrandmother).
        if (dialogue != null) dialogue.Open("Ông", agreeLines, null, null);
    }

    /// <summary>
    /// Called by Bà's QuestNPC when she is talked to. Once Ông kept the chicken and the delivery was not handed out yet, she gives it here
    /// (dialogue + button 'Nhận nhiệm vụ'). True = handled (the caller must not open its normal dialogue); false for every other NPC / moment.
    /// </summary>
    public bool TryHandleGrandmother(Transform npc)
    {
        if (npc == null || npc != grandmother || deliveryQuest == null || dialogue == null || dialogue.IsOpen || questManager == null) return false;
        if (questManager.GetState(questRescue.questId) != QuestState.Completed) return false;      // Ông must keep the chicken first
        if (questManager.GetState(deliveryQuest.questId) != QuestState.NotStarted) return false;   // already given: her normal dialogue (reminder / pay)

        string[] lines = new string[grandmaTaskLines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            try { lines[i] = string.Format(grandmaTaskLines[i], deliveryQuest.rewardMoney); }
            catch (System.FormatException) { lines[i] = grandmaTaskLines[i]; } // a stray '{' in the Inspector text must not break the dialogue
        }
        dialogue.Open(grandmaSpeaker, lines, acceptTaskLabel, AcceptDelivery);
        return true;
    }

    /// <summary>The dialogue button: starts the delivery quest (HUD + guide go to the package). Once only.</summary>
    void AcceptDelivery()
    {
        if (deliveryQuest == null || questManager == null) return;
        if (questManager.GetState(deliveryQuest.questId) != QuestState.NotStarted) return; // never twice
        questManager.AcceptQuest(deliveryQuest);
    }


    /// <summary>
    /// Choice 'Đồng ý': the Player lets Ông cook the chicken. This ends the story: the chicken is NOT woken, no quest is accepted or paid,
    /// nothing else opens. Plays the endgame video, then quits. DialogueUI guarantees it never runs together with OnAskToKeep.
    /// </summary>
void OnAgreeToCook()
    {
        if (endingStarted || questManager == null) return;
        if (questManager.GetState(questRescue.questId) == QuestState.Completed) return; // the chicken was already kept

        endingStarted = true; // the choice is made: nothing else (keep the chicken, E on Ông) can run any more
        SetPrompt(false);
        StartCoroutine(AgreeDialogueThenEndgame());
    }

    /// <summary>The short exchange after 'Đồng ý' (Player, then Ông), then the endgame video. The video follows however the exchange is closed (button, E or Esc).</summary>
    IEnumerator AgreeDialogueThenEndgame()
    {
        if (dialogue != null && agreeCookLines != null && agreeCookLines.Length > 0)
        {
            dialogue.Open("Ông", agreeCookLines, agreeContinueLabel, () => { });
            while (dialogue.IsOpen) yield return null;
        }
        yield return PlayEndgameThenQuit();
    }

    IEnumerator PlayEndgameThenQuit()
    {
        // Freeze the world under the video (realtime waits below, VideoPlayer uses UnscaledGameTime).
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        GameAudio gameAudio = GameAudio.Instance;
        if (gameAudio != null) { gameAudio.SetMusicPaused(true); musicPausedForEnding = true; }

        bool played = false;
        if (endgameVideo != null) yield return PrepareAndPlayEndgame(ok => played = ok);
        else endgameError = "no Endgame Video assigned";

        if (played)
        {
            float limit = Time.realtimeSinceStartup + (float)endgameVideo.length + endgamePlaybackSlack;
            while (endgamePlayer != null && !endgameEnded && !endgameFailed && Time.realtimeSinceStartup < limit) yield return null;
            if (endgameFailed) Debug.LogError("[GrandfatherChickenRescue] Endgame video playback failed: " + endgameError + ". Quitting anyway.", this);
            else if (!endgameEnded) Debug.LogWarning("[GrandfatherChickenRescue] Endgame video did not report its end in time. Quitting anyway.", this);
        }
        else
        {
            Debug.LogError("[GrandfatherChickenRescue] Endgame video could not be played: " + endgameError + ". Quitting anyway.", this);
        }

        CleanupEndgame();

        // After the video: the credits ('hết game') come first, the game is left only from the credits' 'Thoát game' button.
        Time.timeScale = 1f; // the credits scroll with Time.deltaTime
        if (musicPausedForEnding && GameAudio.Instance != null) { GameAudio.Instance.SetMusicPaused(false); musicPausedForEnding = false; }
        EndingSequence ending = EndingSequence.Instance;
        if (ending != null && ending.BeginCreditsOnly()) yield break;

        Debug.LogWarning("[GrandfatherChickenRescue] No EndingSequence in the scene - quitting without credits.", this);
        QuitGame();
    }

    IEnumerator PrepareAndPlayEndgame(System.Action<bool> done)
    {
        endgameEnded = false;
        endgameFailed = false;
        endgameError = null;
        BuildEndgameCanvas();

        endgameTexture = new RenderTexture(Mathf.Max(16, (int)endgameVideo.width), Mathf.Max(16, (int)endgameVideo.height), 0);
        endgameImage.texture = endgameTexture;
        var fitter = endgameImage.GetComponent<AspectRatioFitter>();
        if (fitter != null && endgameVideo.height > 0) fitter.aspectRatio = (float)endgameVideo.width / endgameVideo.height;

        endgamePlayer = endgameCanvas.AddComponent<VideoPlayer>();
        endgamePlayer.playOnAwake = false;
        endgamePlayer.isLooping = false;
        endgamePlayer.source = VideoSource.VideoClip;
        endgamePlayer.clip = endgameVideo;
        endgamePlayer.renderMode = VideoRenderMode.RenderTexture;
        endgamePlayer.targetTexture = endgameTexture;
        endgamePlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        endgamePlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime; // Time.timeScale is 0 while the ending plays
        endgamePlayer.errorReceived += OnEndgameError;
        endgamePlayer.loopPointReached += OnEndgameEnded;
        endgamePlayer.Prepare();

        float prepareLimit = Time.realtimeSinceStartup + endgamePrepareTimeout;
        while (!endgamePlayer.isPrepared && !endgameFailed && Time.realtimeSinceStartup < prepareLimit) yield return null;
        if (endgameFailed || !endgamePlayer.isPrepared)
        {
            if (endgameError == null) endgameError = "the clip was not prepared within " + endgamePrepareTimeout + " s (format/codec may not be supported)";
            done(false);
            yield break;
        }

        for (ushort track = 0; track < endgamePlayer.audioTrackCount; track++)
            endgamePlayer.SetDirectAudioVolume(track, endgameVolume);
        endgamePlayer.Play();

        float startLimit = Time.realtimeSinceStartup + 2f;
        while (endgamePlayer.frame < 1 && !endgameFailed && !endgameEnded && Time.realtimeSinceStartup < startLimit) yield return null;
        if (endgameFailed) { done(false); yield break; }
        if (endgamePlayer.frame < 1 && !endgameEnded)
        {
            endgameError = "the clip did not render its first frame within 2 s";
            done(false);
            yield break;
        }

        endgameImage.gameObject.SetActive(true); // shown only once a frame exists: no white flash
        done(true);
    }

    void BuildEndgameCanvas()
    {
        endgameCanvas = new GameObject("EndgameVideoCanvas");
        var canvas = endgameCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue; // above every other UI
        var scaler = endgameCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        endgameCanvas.AddComponent<GraphicRaycaster>();

        // opaque black, full screen: also swallows every click while the ending plays
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(endgameCanvas.transform, false);
        var bg = bgGO.AddComponent<Image>();
        bg.color = Color.black;
        StretchToParent(bg.rectTransform);

        var videoGO = new GameObject("Video");
        videoGO.transform.SetParent(endgameCanvas.transform, false);
        endgameImage = videoGO.AddComponent<RawImage>();
        endgameImage.color = Color.white;
        endgameImage.raycastTarget = false;
        var fitter = videoGO.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 16f / 9f;
        videoGO.SetActive(false);
    }

    static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void OnEndgameError(VideoPlayer source, string message)
    {
        endgameError = message;
        endgameFailed = true;
        endgameEnded = true;
    }

    void OnEndgameEnded(VideoPlayer source)
    {
        endgameEnded = true;
    }

    void CleanupEndgame()
    {
        if (endgamePlayer != null)
        {
            endgamePlayer.errorReceived -= OnEndgameError;
            endgamePlayer.loopPointReached -= OnEndgameEnded;
            endgamePlayer.Stop();
            endgamePlayer = null;
        }
        if (endgameCanvas != null) Destroy(endgameCanvas);
        endgameCanvas = null;
        endgameImage = null;
        if (endgameTexture != null)
        {
            endgameTexture.Release();
            Destroy(endgameTexture);
            endgameTexture = null;
        }
    }

    /// <summary>Quits the game: stops Play Mode in the Editor, Application.Quit in a build (Application.Quit does nothing in the Editor).</summary>
    static void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    bool InRange()
    {
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= interactionDistance * interactionDistance;
    }

    void SetPrompt(bool visible)
    {
        if (prompt == null || visible == promptShown) return;
        promptShown = visible;
        if (visible) prompt.Show(this, promptText);
        else prompt.Hide(this);
    }

    void OnDisable() { SetPrompt(false); }


/// <summary>True while Bà has the delivery task to hand out (Ông kept the chicken, delivery not started). Lets her QuestNPC show the E prompt before the first battle.</summary>
    public bool HasGrandmotherTask(Transform npc)
    {
        if (npc == null || npc != grandmother || deliveryQuest == null || questManager == null) return false;
        if (questManager.GetState(questRescue.questId) != QuestState.Completed) return false;
        return questManager.GetState(deliveryQuest.questId) == QuestState.NotStarted;
    }
}
