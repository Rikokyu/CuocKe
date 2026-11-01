using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

/// <summary>
/// Plays an optional intro video before showing the full-screen notice and "Bắt đầu" button.
/// Freezes player movement and frees the cursor until dismissed, restores both on dismiss, then accepts
/// the first story quest ("ve_nha_tham_ong_ba") through the existing QuestManager.
/// </summary>
public class IntroScreen : MonoBehaviour
{
    [Header("Intro video")]
    [Tooltip("Optional video shown before the notice. Leave empty to show the notice immediately.")]
    [SerializeField] VideoClip introVideo;
    [SerializeField, Min(1f)] float prepareTimeout = 8f;
    [Tooltip("Extra seconds allowed after the clip length before continuing if the video never reports its end.")]
    [SerializeField, Min(1f)] float playbackSlack = 4f;
    [SerializeField, Range(0f, 1f)] float videoVolume = 1f;
    [Tooltip("Seconds the video takes to fade out into the notice once it has ended (the last frame stays until it is gone). 0 = switch at once.")]
    [SerializeField, Min(0f)] float videoFadeDuration = 1.5f;

    [Header("Notice")]
    [SerializeField] string headline = "CUỘC KÊ";
    [SerializeField, TextArea(2, 5)] string body =
        "Sau bao năm làm IT quần quật với deadline, bạn quyết định xách ba lô về quê thăm ông bà...";
    [SerializeField] string buttonLabel = "Bắt đầu (Enter)";

    [Header("Scene references")]
    [SerializeField] MonoBehaviour playerMovement; // assign the Player's PlayerMovement component
    [SerializeField] QuestManager questManager;
    [SerializeField] QuestDefinition questArrive;   // ve_nha_tham_ong_ba

    bool dismissed; // Destroy(this) only happens at the end of the frame: Update must not take the mouse back after Dismiss
    MouseMovement mouseLook; // camera look is switched off while the intro is on screen, otherwise the view spins behind the notice
    GameObject canvasRoot;
    GameObject videoCanvas;
    RawImage videoImage;
    VideoPlayer videoPlayer;
    RenderTexture videoTexture;
    bool videoEnded;
    bool videoFailed;
    bool musicPausedForIntro;
    string videoError;

    void Start()
    {
        BuildUI();

        if (playerMovement != null) playerMovement.enabled = false;
        mouseLook = FindFirstObjectByType<MouseMovement>();
        if (mouseLook != null) mouseLook.enabled = false; // MouseMovement.Start would lock the cursor again, so it must not run yet
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (introVideo == null) return;

        canvasRoot.SetActive(false);
        SetIntroMusicPaused(true);
        StartCoroutine(PlayIntroVideo());
    }

    void Update()
    {
        if (dismissed) return;
        // keep the mouse free for as long as the intro is up (the Start button must be clickable); Dismiss gives the game its lock back
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;

        // Enter (main or numpad) starts the game, same as clicking the button
        if (canvasRoot != null && canvasRoot.activeSelf &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) Dismiss();
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("IntroScreenCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        canvasRoot = canvasGO;

        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bg = bgGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.92f);
        SetStretch(bg.rectTransform);

        var titleGO = new GameObject("Headline");
        titleGO.transform.SetParent(canvasGO.transform, false);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = headline;
        titleTMP.fontSize = 64;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = Color.white;
        SetAnchors(titleTMP.rectTransform, 0.1f, 0.63f, 0.9f, 0.78f);

        var bodyGO = new GameObject("Body");
        bodyGO.transform.SetParent(canvasGO.transform, false);
        var bodyTMP = bodyGO.AddComponent<TextMeshProUGUI>();
        bodyTMP.text = body;
        bodyTMP.fontSize = 26;
        bodyTMP.alignment = TextAlignmentOptions.Center;
        bodyTMP.color = new Color(0.9f, 0.9f, 0.9f);
        SetAnchors(bodyTMP.rectTransform, 0.14f, 0.38f, 0.86f, 0.6f);

        var btnGO = new GameObject("StartButton");
        btnGO.transform.SetParent(canvasGO.transform, false);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.55f, 0.25f);
        var btn = btnGO.AddComponent<Button>();
        SetAnchors(btnImg.rectTransform, 0.42f, 0.2f, 0.58f, 0.28f);

        var btnTextGO = new GameObject("Label");
        btnTextGO.transform.SetParent(btnGO.transform, false);
        var btnTMP = btnTextGO.AddComponent<TextMeshProUGUI>();
        btnTMP.text = buttonLabel;
        btnTMP.fontSize = 28;
        btnTMP.alignment = TextAlignmentOptions.Center;
        btnTMP.color = Color.white;
        SetStretch(btnTMP.rectTransform);

        btn.onClick.AddListener(Dismiss);
    }

IEnumerator PlayIntroVideo()
    {
        BuildVideoCanvas();
        bool videoOk = false;
        yield return PrepareAndPlayVideo(ok => videoOk = ok);

        bool finishedNormally = false;
        if (videoOk)
        {
            float limit = Time.realtimeSinceStartup + (float)introVideo.length + playbackSlack;
            while (videoPlayer != null && !videoEnded && !videoFailed && Time.realtimeSinceStartup < limit) yield return null;
            if (videoFailed)
                Debug.LogError("[IntroScreen] Intro video playback failed: " + videoError + ". Showing the notice instead.", this);
            else if (!videoEnded)
                Debug.LogWarning("[IntroScreen] Intro video did not report its end in time; showing the notice.", this);
            else
                finishedNormally = true;
        }
        else
        {
            Debug.LogError("[IntroScreen] Intro video could not be played: " + videoError + ". Showing the notice instead.", this);
        }

        // Video ended normally: it is NOT closed at once. The notice appears underneath (the video canvas sorts above it) while the
        // video stays on its last frame and fades out, so the text screen emerges from the video.
        CanvasGroup fade = videoCanvas != null ? videoCanvas.GetComponent<CanvasGroup>() : null;
        if (finishedNormally && fade != null && videoFadeDuration > 0f)
        {
            if (canvasRoot != null) canvasRoot.SetActive(true);
            SetIntroMusicPaused(false);
            fade.blocksRaycasts = false; // the Start button already works during the fade
            for (float t = 0f; t < videoFadeDuration; t += Time.unscaledDeltaTime)
            {
                fade.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / videoFadeDuration);
                yield return null;
            }
        }

        CleanupVideo();
        if (canvasRoot != null) canvasRoot.SetActive(true);
        SetIntroMusicPaused(false);
    }

    IEnumerator PrepareAndPlayVideo(System.Action<bool> done)
    {
        videoEnded = false;
        videoFailed = false;
        videoError = null;

        videoTexture = new RenderTexture(Mathf.Max(16, (int)introVideo.width), Mathf.Max(16, (int)introVideo.height), 0);
        videoImage.texture = videoTexture;
        var aspectFitter = videoImage.GetComponent<AspectRatioFitter>();
        if (aspectFitter != null && introVideo.height > 0)
            aspectFitter.aspectRatio = (float)introVideo.width / introVideo.height;

        videoPlayer = videoCanvas.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = introVideo;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.errorReceived += OnVideoError;
        videoPlayer.loopPointReached += OnVideoEnded;
        videoPlayer.Prepare();

        float prepareLimit = Time.realtimeSinceStartup + prepareTimeout;
        while (!videoPlayer.isPrepared && !videoFailed && Time.realtimeSinceStartup < prepareLimit) yield return null;
        if (videoFailed || !videoPlayer.isPrepared)
        {
            if (videoError == null)
                videoError = "the clip was not prepared within " + prepareTimeout + " s (format/codec may not be supported)";
            done(false);
            yield break;
        }

        for (ushort track = 0; track < videoPlayer.audioTrackCount; track++)
            videoPlayer.SetDirectAudioVolume(track, videoVolume);
        videoPlayer.Play();

        float startLimit = Time.realtimeSinceStartup + 2f;
        while (videoPlayer.frame < 1 && !videoFailed && !videoEnded && Time.realtimeSinceStartup < startLimit) yield return null;
        if (videoFailed)
        {
            done(false);
            yield break;
        }
        if (videoPlayer.frame < 1 && !videoEnded)
        {
            videoError = "the clip did not render its first frame within 2 s";
            done(false);
            yield break;
        }

        videoImage.gameObject.SetActive(true);
        done(true);
    }

    void BuildVideoCanvas()
    {
        videoCanvas = new GameObject("IntroVideoCanvas");
        videoCanvas.AddComponent<CanvasGroup>(); // used to fade the video out
        var canvas = videoCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 501;

        var backgroundGO = new GameObject("Background");
        backgroundGO.transform.SetParent(videoCanvas.transform, false);
        var background = backgroundGO.AddComponent<Image>();
        background.color = Color.black;
        SetStretch(background.rectTransform);

        var videoGO = new GameObject("Video");
        videoGO.transform.SetParent(videoCanvas.transform, false);
        videoImage = videoGO.AddComponent<RawImage>();
        videoImage.color = Color.white;
        videoImage.raycastTarget = false;
        var aspectFitter = videoGO.AddComponent<AspectRatioFitter>();
        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspectFitter.aspectRatio = 16f / 9f;
        videoGO.SetActive(false);
    }

    void OnVideoError(VideoPlayer player, string message)
    {
        videoError = message;
        videoFailed = true;
        videoEnded = true;
    }

    void OnVideoEnded(VideoPlayer player)
    {
        videoEnded = true;
    }

    void CleanupVideo()
    {
        if (videoPlayer != null)
        {
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.loopPointReached -= OnVideoEnded;
            videoPlayer.Stop();
            videoPlayer = null;
        }

        if (videoCanvas != null) Destroy(videoCanvas);
        videoCanvas = null;
        videoImage = null;
        if (videoTexture != null)
        {
            videoTexture.Release();
            Destroy(videoTexture);
            videoTexture = null;
        }
    }

    void SetIntroMusicPaused(bool paused)
    {
        GameAudio audio = GameAudio.Instance;
        if (audio != null) audio.SetMusicPaused(paused);
        musicPausedForIntro = paused;
    }

    static void SetStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void SetAnchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    void Dismiss()
    {
        dismissed = true;
        if (playerMovement != null) playerMovement.enabled = true;
        if (mouseLook != null) mouseLook.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (canvasRoot != null) Destroy(canvasRoot);

        if (questManager != null && questArrive != null && questManager.GetState(questArrive.questId) == QuestState.NotStarted)
            questManager.AcceptQuest(questArrive);

        Destroy(this);
    }

    void OnDestroy()
    {
        if (mouseLook != null) mouseLook.enabled = true;
        CleanupVideo();
        if (musicPausedForIntro) SetIntroMusicPaused(false);
    }
}
