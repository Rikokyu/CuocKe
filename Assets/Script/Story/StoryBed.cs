using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

/// <summary>
/// The bed in the grandparents' house: the last step of the garden quest ("garden_protein"). E on the bed only works
/// once the quest is ReadyToTurnIn (rau bón, hái, gà ăn all done). It shows a short black "sleep" screen, completes
/// the quest in the dark through QuestManager.TurnIn (which is what triggers the one-time transformation in
/// ChickenTransformationTrigger) and fades out on "Sáng hôm sau...". No day/night system, no scene reload.
/// Player movement is switched off only while the screen is black and is always restored.
/// </summary>
public class StoryBed : MonoBehaviour
{
    [Header("Quest")]
    [SerializeField] QuestDefinition gardenQuest;
    [SerializeField] QuestManager questManager;

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] float interactionDistance = 3.5f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string promptText = "Nhấn E để đi ngủ";
    [SerializeField] string notYetText = "Chưa buồn ngủ, còn việc ngoài vườn";
    [SerializeField] string needPermissionText = "Hãy xin phép ông bà đi ngủ trưa trước đã";

    /// <summary>Sleeping also needs the grandparents' permission (GardenFlowStory). Without that story in the scene nothing is locked.</summary>
    bool PermissionOk { get { return GardenFlowStory.Instance == null || GardenFlowStory.Instance.NapAllowed; } }
    [SerializeField] Transform player;

    [Header("Scene references")]
    [SerializeField] DialogueUI dialogue;
    [SerializeField] InteractionPromptUI prompt;
    [Tooltip("The Player's PlayerMovement. Only enabled/disabled here (never edited).")]
    [SerializeField] MonoBehaviour playerMovement;

    [Header("Player lines (a '[Player] ' prefix = spoken by the Player)")]
    [SerializeField, TextArea(1, 3)] string[] beforeSleepLines = { "[Player] Ơ cái giường với con mèo này cứ quen quen ấy nhờ" };
    [SerializeField, TextArea(1, 3)] string[] afterWakeLines = { "[Player] Ngủ ngon quá. Ra sau kiếm con gà thử" };
    [SerializeField] string playerSpeaker = "Tôi";

    [Header("Second sleep (after the village tournament - ZoroReunionStory)")]
    [SerializeField, TextArea(1, 3)] string[] beforeSleepAgainLines = { "[Player] Hạ xong cả Chị Ba lẫn Chú Tư, mệt rã người. Ngủ một giấc cho khoẻ đã." };
    [SerializeField, TextArea(1, 3)] string[] afterWakeAgainLines = { "[Player] Ngủ ngon quá. Ủa... con gà đâu rồi?" };


    [Header("Sleep video (replaces the black screen + Zzz)")]
    [Tooltip("Assets/Art/UI/video/video_the_next_day-www_tiengdong_com.mp4 (The Next Day).")]
    [SerializeField] VideoClip sleepVideo;
    [SerializeField, Min(1f)] float prepareTimeout = 8f;
    [Tooltip("Extra seconds allowed after the clip length before the video is cut off (a video that never reports its end cannot hang the game).")]
    [SerializeField, Min(1f)] float playbackSlack = 4f;
    [SerializeField, Range(0f, 1f)] float videoVolume = 1f;

    [Header("Fade / fallback screen")]
    [SerializeField] string morningText = "Sáng hôm sau...";
    [SerializeField, Min(0.1f)] float fadeSeconds = 0.6f;
    [SerializeField, Min(0f)] float holdSeconds = 1.5f;

    /// <summary>True only while the video / black screen covers the game (read by audio, quest guide).</summary>
    public bool IsSleeping { get; private set; }
    /// <summary>True for the WHOLE sequence: line before, video, line after. A second E can never start it again.</summary>
    public bool IsBusy { get; private set; }
    /// <summary>The sequence finished once (it never runs again).</summary>
    public bool HasSlept { get; private set; }
    /// <summary>The second sleep (after the tournament) finished once (it never runs again).</summary>
    public bool HasSleptAgain { get; private set; }

    /// <summary>Set when the video could not be played (the reason is also logged as an error).</summary>
    public string VideoError { get; private set; }

    bool promptShown;
    string shownText;
    GameObject overlay;
    CanvasGroup overlayGroup;
    TextMeshProUGUI overlayLabel;
    RawImage videoImage;
    VideoPlayer videoPlayer;
    RenderTexture videoTexture;
    bool videoEnded;
    bool videoFailed;

    void Start()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null || questManager == null || gardenQuest == null || prompt == null)
            Debug.LogError("[StoryBed] Player / QuestManager / quest / prompt reference missing.", this);
        if (sleepVideo == null)
            Debug.LogError("[StoryBed] No sleep video assigned (expected the 'The Next Day' clip from Assets/Art/UI/video). The plain black screen will be used.", this);
    }

void Update()
    {
        if (player == null || questManager == null || gardenQuest == null) return;

        // second sleep: only once the tournament is won (ZoroReunionStory is in its Resting stage)
        bool again = ZoroReunionStory.Instance != null && ZoroReunionStory.Instance.CanSleep;
        QuestState s = questManager.GetState(gardenQuest.questId);
        bool garden = s == QuestState.ReadyToTurnIn;
        bool ready = again || (garden && PermissionOk);
        bool inRange = InRange() && !IsBusy && (dialogue == null || !dialogue.IsOpen) && InteractObject.IsAimed(this);

        SetPrompt(inRange && (again || garden || s == QuestState.Active), ready ? promptText : (garden ? needPermissionText : notYetText));
        if (inRange && ready && Input.GetKeyDown(interactKey))
        {
            if (again) TrySleepAgain();
            else TrySleep();
        }
    }

    /// <summary>Starts the sequence if (and only if) the garden quest is ReadyToTurnIn and it is not already running. Public so a test can drive the same path.</summary>
public bool TrySleep()
    {
        if (IsBusy || HasSlept || questManager == null || gardenQuest == null) return false;
        if (questManager.GetState(gardenQuest.questId) != QuestState.ReadyToTurnIn || !PermissionOk) return false;
        if (dialogue != null && dialogue.IsOpen) return false;

        IsBusy = true; // flip first: a second press can never start a second sequence
        SetPrompt(false, null);
        StartCoroutine(SleepRoutine(beforeSleepLines, afterWakeLines, () => ApplySleep(), false));
        return true;
    }

    /// <summary>
    /// The second sleep: only while ZoroReunionStory is in its Resting stage (the tournament is won and the chicken has not left yet),
    /// only once. It plays the same sequence; while the screen is black the chicken leaves (ZoroReunionStory.OnPlayerSlept).
    /// Public so a test can drive the same path.
    /// </summary>
    public bool TrySleepAgain()
    {
        if (IsBusy || HasSleptAgain) return false;
        ZoroReunionStory story = ZoroReunionStory.Instance;
        if (story == null || !story.CanSleep) return false;
        if (dialogue != null && dialogue.IsOpen) return false;

        IsBusy = true; // flip first: a second press can never start a second sequence
        SetPrompt(false, null);
        StartCoroutine(SleepRoutine(beforeSleepAgainLines, afterWakeAgainLines, () => { ZoroReunionStory s = ZoroReunionStory.Instance; if (s != null) s.OnPlayerSlept(); }, true));
        return true;
    }

    /// <summary>The actual effect of sleeping: completes the garden quest once (-> transformation). Safe to call twice.</summary>
    public bool ApplySleep()
    {
        return questManager != null && gardenQuest != null && questManager.TurnIn(gardenQuest);
    }

IEnumerator SleepRoutine(string[] beforeLines, string[] afterLines, System.Action whileBlack, bool again)
    {
        // 1. the Player's line BEFORE sleeping; the sleep starts only after this dialogue is closed
        yield return SayLines(beforeLines);

        // 2. sleep: the video (or the fallback black screen) covers the game
        IsSleeping = true;
        if (playerMovement != null) playerMovement.enabled = false;
        BuildOverlay();
        yield return PlaySleepScreen(whileBlack);

        // 3. morning: back to the world
        yield return Fade(1f, 0f);
        EndSleep();
        if (again) HasSleptAgain = true; else HasSlept = true;

        // 4. the Player's line AFTER waking; then the find-the-chicken flow goes on (MissingChickenStory / ZoroReunionStory banner, Ông / Bà)
        yield return SayLines(afterLines);
        IsBusy = false;
    }

    IEnumerator SayLines(string[] lines)
    {
        if (dialogue == null || lines == null || lines.Length == 0) yield break;
        while (dialogue.IsOpen) yield return null; // never talk over another dialogue
        dialogue.Open(playerSpeaker, lines, null, null);
        while (dialogue.IsOpen) yield return null;
    }

IEnumerator PlaySleepScreen(System.Action whileBlack)
    {
        yield return Fade(0f, 1f); // black first: the clip never pops over the world

        bool videoOk = false;
        yield return PrepareAndPlayVideo(ok => videoOk = ok);

        if (whileBlack != null) whileBlack(); // the screen is covered (video or black): the chicken changes / leaves now

        if (videoOk)
        {
            float limit = Time.realtimeSinceStartup + (float)sleepVideo.length + playbackSlack;
            while (videoPlayer != null && !videoEnded && Time.realtimeSinceStartup < limit) yield return null;
            if (!videoEnded) Debug.LogWarning("[StoryBed] The sleep video did not report its end in time; continuing.", this);
        }
        else
        {
            // never silent: the reason is logged; a plain card keeps the game going
            Debug.LogError("[StoryBed] Sleep video not played: " + VideoError + ". Showing the plain black screen instead.", this);
            overlayLabel.text = morningText;
            yield return new WaitForSeconds(holdSeconds);
        }
    }

    IEnumerator PrepareAndPlayVideo(System.Action<bool> done)
    {
        videoEnded = false; videoFailed = false; VideoError = null;
        if (sleepVideo == null) { VideoError = "no VideoClip assigned on StoryBed.sleepVideo"; done(false); yield break; }

        videoTexture = new RenderTexture(Mathf.Max(16, (int)sleepVideo.width), Mathf.Max(16, (int)sleepVideo.height), 0);
        videoImage.texture = videoTexture;
        var ar = videoImage.GetComponent<AspectRatioFitter>();
        if (ar != null && sleepVideo.height > 0) ar.aspectRatio = (float)sleepVideo.width / sleepVideo.height;

        videoPlayer = overlay.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = sleepVideo;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.errorReceived += OnVideoError;
        videoPlayer.loopPointReached += OnVideoEnded;
        videoPlayer.Prepare();

        float limit = Time.realtimeSinceStartup + prepareTimeout;
        while (!videoPlayer.isPrepared && !videoFailed && Time.realtimeSinceStartup < limit) yield return null;
        if (videoFailed || !videoPlayer.isPrepared)
        {
            if (VideoError == null) VideoError = "the clip was not prepared within " + prepareTimeout + " s (format/codec not supported by the VideoPlayer on this platform?)";
            done(false);
            yield break;
        }

        for (ushort i = 0; i < videoPlayer.audioTrackCount; i++) videoPlayer.SetDirectAudioVolume(i, videoVolume);
        videoPlayer.Play();
        float startLimit = Time.realtimeSinceStartup + 2f;
        while (videoPlayer.frame < 1 && !videoFailed && !videoEnded && Time.realtimeSinceStartup < startLimit) yield return null;
        if (videoFailed) { done(false); yield break; }
        videoImage.gameObject.SetActive(true); // first frame is ready: show it
        done(true);
    }

    void OnVideoError(VideoPlayer vp, string message) { VideoError = message; videoFailed = true; videoEnded = true; }
    void OnVideoEnded(VideoPlayer vp) { videoEnded = true; }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            overlayGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeSeconds));
            yield return null;
        }
        overlayGroup.alpha = to;
    }

    void EndSleep()
    {
        if (videoPlayer != null)
        {
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.loopPointReached -= OnVideoEnded;
            videoPlayer.Stop();
            videoPlayer = null;
        }
        if (overlay != null) Destroy(overlay);
        overlay = null;
        if (videoTexture != null) { videoTexture.Release(); Destroy(videoTexture); videoTexture = null; }
        if (playerMovement != null) playerMovement.enabled = true;
        IsSleeping = false;
    }

    void BuildOverlay()
    {
        overlay = new GameObject("SleepScreenCanvas");
        var canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        overlayGroup = overlay.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.blocksRaycasts = false;

        var bgGO = new GameObject("Black");
        bgGO.transform.SetParent(overlay.transform, false);
        var bg = bgGO.AddComponent<Image>();
        bg.color = Color.black;
        Stretch(bg.rectTransform);

        // the video: a RawImage fitted in the screen (keeps the clip's shape), hidden until its first frame
        var vidGO = new GameObject("Video");
        vidGO.transform.SetParent(overlay.transform, false);
        videoImage = vidGO.AddComponent<RawImage>();
        videoImage.color = Color.white;
        videoImage.raycastTarget = false;
        var fit = vidGO.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = 16f / 9f;
        vidGO.SetActive(false);

        // only used by the error fallback; empty while the video plays, so video and text never overlap
        var textGO = new GameObject("Label");
        textGO.transform.SetParent(overlay.transform, false);
        overlayLabel = textGO.AddComponent<TextMeshProUGUI>();
        overlayLabel.fontSize = 56;
        overlayLabel.alignment = TextAlignmentOptions.Center;
        overlayLabel.color = Color.white;
        overlayLabel.text = "";
        Stretch(overlayLabel.rectTransform);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    bool InRange()
    {
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= interactionDistance * interactionDistance;
    }

    void SetPrompt(bool visible, string text)
    {
        if (prompt == null) return;
        if (!visible)
        {
            if (!promptShown) return;
            promptShown = false; shownText = null;
            prompt.Hide(this);
            return;
        }
        if (promptShown && shownText == text) return;
        promptShown = true; shownText = text;
        prompt.Show(this, text);
    }

    void OnDisable()
    {
        SetPrompt(false, null);
        if (IsSleeping) EndSleep(); // never leave the player frozen
    }
}
