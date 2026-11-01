using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crosshair info (created at runtime, no scene setup): while the dot in the middle of the screen points at an InteractObject its
/// name is shown under the dot. If the object can be used right now (the shared InteractionPromptUI is showing something) a circle
/// with the key (E) appears under the name, with the short action text next to it.
/// The old bottom-of-screen prompt keeps working as the data source (visibility + text) but is made invisible by this HUD.
/// </summary>
public class InteractionAimHUD : MonoBehaviour
{
    static readonly Regex KeyRegex = new Regex(@"^\s*Nh\u1ea5n\s+(\S+)\s+(?:\u0111\u1ec3\s+)?(.*)$");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<InteractionAimHUD>() != null) return;
        new GameObject("InteractionAimHUD").AddComponent<InteractionAimHUD>();
    }

    TextMeshProUGUI nameLabel, keyLabel, actionLabel;
    GameObject circleRoot;
    CanvasGroup group;
    InteractionPromptUI prompt;
    CanvasGroup promptGroup;
    Sprite circleSprite;

    void Awake()
    {
        Build();
    }

    void Build()
    {
        GameObject canvasGO = new GameObject("AimCanvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        group = canvasGO.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f;

        nameLabel = MakeLabel("Name", canvasGO.transform, 34f, new Vector2(0f, -62f), new Vector2(700f, 50f), TextAlignmentOptions.Center);
        nameLabel.fontStyle = FontStyles.Bold;
        nameLabel.outlineWidth = 0.2f;
        nameLabel.outlineColor = new Color32(0, 0, 0, 255);

        circleRoot = new GameObject("KeyCircle");
        circleRoot.transform.SetParent(canvasGO.transform, false);
        Image circle = circleRoot.AddComponent<Image>();
        circle.sprite = GetCircleSprite();
        circle.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        circle.raycastTarget = false;
        RectTransform crt = circle.rectTransform;
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(64f, 64f);
        crt.anchoredPosition = new Vector2(0f, -128f);

        GameObject ringGO = new GameObject("Ring");
        ringGO.transform.SetParent(circleRoot.transform, false);
        Image ring = ringGO.AddComponent<Image>();
        ring.sprite = GetRingSprite();
        ring.color = Color.white;
        ring.raycastTarget = false;
        Stretch(ring.rectTransform);

        keyLabel = MakeLabel("Key", circleRoot.transform, 38f, Vector2.zero, new Vector2(64f, 64f), TextAlignmentOptions.Center);
        keyLabel.fontStyle = FontStyles.Bold;
        Stretch(keyLabel.rectTransform);

        actionLabel = MakeLabel("Action", circleRoot.transform, 28f, new Vector2(52f, 0f), new Vector2(520f, 50f), TextAlignmentOptions.Left);
        actionLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
        actionLabel.outlineWidth = 0.2f;
        actionLabel.outlineColor = new Color32(0, 0, 0, 255);
        circleRoot.SetActive(false);
    }

    TextMeshProUGUI MakeLabel(string objName, Transform parent, float size, Vector2 pos, Vector2 dim, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = dim;
        rt.anchoredPosition = pos;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    void Update()
    {
        // only while the mouse is captured by the game (no dialogue / shop / bag in front)
        bool playing = Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0f;
        InteractObject target = playing ? InteractObject.Aimed : null;
        if (target == null) { group.alpha = 0f; HideOldPrompt(); return; }

        group.alpha = 1f;
        nameLabel.text = target.DisplayName;

        if (prompt == null) prompt = FindFirstObjectByType<InteractionPromptUI>();
        HideOldPrompt();
        bool usable = prompt != null && prompt.IsVisible;
        circleRoot.SetActive(usable);
        if (usable)
        {
            string key = "E", action = prompt.Text;
            Match m = KeyRegex.Match(prompt.Text ?? "");
            if (m.Success) { key = m.Groups[1].Value; action = m.Groups[2].Value; }
            keyLabel.text = key;
            actionLabel.text = action;
        }
    }

    // the old prompt is still the data source (IsVisible / Text); only its look is switched off
    void HideOldPrompt()
    {
        if (prompt == null) return;
        if (promptGroup == null)
        {
            promptGroup = prompt.GetComponent<CanvasGroup>();
            if (promptGroup == null) promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
        }
        promptGroup.alpha = 0f;
    }

    // ---------------------------------------------------------------- procedural sprites

    Sprite GetCircleSprite()
    {
        if (circleSprite == null) circleSprite = MakeDisc(128, 0f);
        return circleSprite;
    }

    Sprite GetRingSprite() { return MakeDisc(128, 0.06f); }

    // ringWidth 0 = filled disc, otherwise only a ring of that fraction of the diameter
    static Sprite MakeDisc(int size, float ringWidth)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f - 1f;
        float c = size * 0.5f - 0.5f;
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(r - d + 0.5f);              // soft outer edge
                if (ringWidth > 0f) a *= Mathf.Clamp01((d - (r - size * ringWidth)) + 0.5f); // hollow inside
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
