using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Minimal placeholder intro: a plain full-screen text panel with a "Bắt đầu" button, built at runtime
/// (no cutscene, no video, no Timeline/Cinemachine work - explicitly out of scope for this milestone).
/// Freezes player movement and frees the cursor while shown, restores both on dismiss, then accepts the
/// first story quest ("ve_nha_tham_ong_ba") through the existing QuestManager.
/// </summary>
public class IntroScreen : MonoBehaviour
{
    [Header("Text (placeholder)")]
    [SerializeField] string headline = "CUỘC KÊ";
    [SerializeField, TextArea(2, 5)] string body =
        "Sau bao năm làm IT quần quật với deadline, bạn quyết định xách ba lô về quê thăm ông bà...\n\n(Đây là màn hình chữ tạm thời thay cho đoạn intro hoạt hình.)";
    [SerializeField] string buttonLabel = "Bắt đầu";

    [Header("Scene references")]
    [SerializeField] MonoBehaviour playerMovement; // assign the Player's PlayerMovement component
    [SerializeField] QuestManager questManager;
    [SerializeField] QuestDefinition questArrive;   // ve_nha_tham_ong_ba

    GameObject canvasRoot;

    void Start()
    {
        BuildUI();

        if (playerMovement != null) playerMovement.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
        if (playerMovement != null) playerMovement.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (canvasRoot != null) Destroy(canvasRoot);

        if (questManager != null && questArrive != null && questManager.GetState(questArrive.questId) == QuestState.NotStarted)
            questManager.AcceptQuest(questArrive);

        Destroy(this);
    }
}
