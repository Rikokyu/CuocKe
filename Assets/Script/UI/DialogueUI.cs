using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal one-speaker dialogue panel: a few lines, a "next" button, and on the last line an optional
/// action button (e.g. "Nhận nhiệm vụ") plus an always-available close button.
///
/// Input follows the project's existing legacy-Input convention: confirmKey (E) advances / confirms,
/// closeKey (Esc) closes. Both are ignored on the frame the panel opened, so the same E press that opened
/// the dialogue can never also confirm it. The action runs AFTER the panel closed and only once.
///
/// While open it frees the cursor and disables the configured behaviours (player movement + mouse look),
/// then restores exactly the previous state on close. Must live on an object that stays active.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text speakerLabel;
    [SerializeField] TMP_Text bodyLabel;
    [SerializeField] Button primaryButton;
    [SerializeField] TMP_Text primaryButtonLabel;
    [SerializeField] Button closeButton;
    [SerializeField] TMP_Text closeButtonLabel;

    [Header("Input / texts")]
    [SerializeField] KeyCode confirmKey = KeyCode.E;
    [SerializeField] KeyCode closeKey = KeyCode.Escape;
    [SerializeField] string nextText = "Tiếp tục";
    [SerializeField] string closeText = "Đóng (Esc)";

    [Header("Typewriter (text appears letter by letter)")]
    [SerializeField] bool typewriter = true;
    [Tooltip("Letters per second. E (confirmKey) or a click on the text shows the whole line at once; the next E / the button goes on.")]
    [SerializeField, Min(1f)] float charactersPerSecond = 45f;
    [Tooltip("Optional: a Button on the body text; clicking it shows the whole line at once.")]
    [SerializeField] Button revealTextButton;

    [Header("Speakers: NPC lines use the name passed to Open(); a line that starts with the prefix is spoken by the Player")]
    [SerializeField] string playerLinePrefix = "[Player] ";
    [SerializeField] string playerSpeakerName = "Tôi";
    [SerializeField] Color playerNameColor = new Color(0.45f, 0.85f, 1f, 1f);
    [SerializeField] Color playerPanelColor = new Color(0.05f, 0.10f, 0.18f, 0.93f);

    [Header("Frozen while the dialogue is open (e.g. PlayerMovement, MouseMovement)")]
    [SerializeField] Behaviour[] disableWhileOpen;

    string[] lines;
    int lineIndex;
    Action primaryAction;
    string actionLabel;
    bool[] wasEnabled;
    CursorLockMode previousLockState;
    bool previousCursorVisible;
    int openedOnFrame = -1;

    string currentSpeaker;
    Color npcNameColor = Color.white;
    Color npcPanelColor = Color.black;
    Image panelImage;
    float visibleCharacters;
    int totalCharacters;
    bool typing;

    /// <summary>True while the current line is still being typed out.</summary>
    public bool IsTyping { get { return typing; } }

    /// <summary>Raised when a line is shown. true = the Player speaks it, false = an NPC (used by GameAudio).</summary>
    public event Action<bool> LineShown;

    public bool IsOpen { get; private set; }
    /// <summary>Read-only: the NPC name passed to Open() for the dialogue that is open now (null when closed). Used only to drive NPC gesture animations; it changes nothing in the dialogue.</summary>
    public string CurrentSpeaker { get { return IsOpen ? currentSpeaker : null; } }

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (primaryButton != null) primaryButton.onClick.AddListener(OnPrimaryPressed);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (closeButtonLabel != null) closeButtonLabel.text = closeText;
        if (revealTextButton != null) revealTextButton.onClick.AddListener(CompleteLine);
        if (speakerLabel != null) npcNameColor = speakerLabel.color;
        if (panel != null) panelImage = panel.GetComponent<Image>();
        if (panelImage != null) npcPanelColor = panelImage.color;
    }

    /// <summary>
    /// Opens the dialogue. primaryLabel/primaryAction are used on the LAST line only; pass null for a
    /// plain conversation (the last line then just offers to close).
    /// </summary>
    public void Open(string speaker, string[] dialogueLines, string primaryLabel, Action action)
    {
        if (IsOpen || dialogueLines == null || dialogueLines.Length == 0) return;

        lines = dialogueLines;
        primaryAction = action;
        actionLabel = primaryLabel;

        IsOpen = true;
        openedOnFrame = Time.frameCount;
        FreezePlayer();

        currentSpeaker = speaker;
        if (panel != null) panel.SetActive(true);
        ShowLine(0);
    }

    void ShowLine(int index)
    {
        lineIndex = index;
        ApplyLine(lines[index]);

        bool isLast = index >= lines.Length - 1;
        if (!isLast)
            SetPrimary(true, nextText);
        else if (primaryAction != null)
            SetPrimary(true, actionLabel);
        else
            SetPrimary(false, null);
    }

    /// <summary>Picks the speaker (Player or the NPC), colours the name / panel and starts the typewriter.</summary>
    void ApplyLine(string raw)
    {
        bool byPlayer = !string.IsNullOrEmpty(playerLinePrefix) && raw.StartsWith(playerLinePrefix);
        string text = byPlayer ? raw.Substring(playerLinePrefix.Length) : raw;

        if (speakerLabel != null)
        {
            speakerLabel.text = byPlayer ? playerSpeakerName : currentSpeaker;
            speakerLabel.color = byPlayer ? playerNameColor : npcNameColor;
        }
        if (panelImage != null) panelImage.color = byPlayer ? playerPanelColor : npcPanelColor;

        if (LineShown != null) LineShown(byPlayer);
        if (bodyLabel == null) return;
        bodyLabel.text = text;
        if (typewriter && charactersPerSecond > 0f)
        {
            bodyLabel.ForceMeshUpdate();
            totalCharacters = bodyLabel.textInfo.characterCount;
            visibleCharacters = 0f;
            bodyLabel.maxVisibleCharacters = 0;
            typing = totalCharacters > 0;
        }
        else
        {
            typing = false;
            bodyLabel.maxVisibleCharacters = int.MaxValue;
        }
    }

    /// <summary>Shows the rest of the current line at once (E while typing, or a click on the text).</summary>
    public void CompleteLine()
    {
        if (!IsOpen || !typing) return;
        typing = false;
        if (bodyLabel != null) bodyLabel.maxVisibleCharacters = int.MaxValue;
    }

    void AdvanceTyping()
    {
        visibleCharacters += charactersPerSecond * Time.unscaledDeltaTime;
        int shown = Mathf.FloorToInt(visibleCharacters);
        if (shown >= totalCharacters) { CompleteLine(); return; }
        if (bodyLabel != null) bodyLabel.maxVisibleCharacters = shown;
    }

    void SetPrimary(bool visible, string text)
    {
        if (primaryButton != null) primaryButton.gameObject.SetActive(visible);
        if (visible && primaryButtonLabel != null) primaryButtonLabel.text = text;
    }

    void OnPrimaryPressed()
    {
        if (!IsOpen) return; // stale click after closing

        if (lineIndex < lines.Length - 1)
        {
            ShowLine(lineIndex + 1);
            return;
        }

        Action action = primaryAction;
        primaryAction = null;
        Close();
        if (action != null) action();
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        typing = false;
        if (bodyLabel != null) bodyLabel.maxVisibleCharacters = int.MaxValue;
        if (speakerLabel != null) speakerLabel.color = npcNameColor;
        if (panelImage != null) panelImage.color = npcPanelColor;
        if (panel != null) panel.SetActive(false);
        RestorePlayer();
    }

    void Update()
    {
        if (!IsOpen) return;
        if (typing) AdvanceTyping();
        if (Time.frameCount == openedOnFrame) return;

        if (Input.GetKeyDown(closeKey))
        {
            Close();
            return;
        }

        if (Input.GetKeyDown(confirmKey))
        {
            if (typing) { CompleteLine(); return; } // first E: show the whole line, second E: go on
            bool isLast = lineIndex >= lines.Length - 1;
            if (isLast && primaryAction == null) Close();   // plain conversation: E closes it
            else OnPrimaryPressed();
        }
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
        for (int i = 0; i < count && wasEnabled != null && i < wasEnabled.Length; i++)
        {
            Behaviour b = disableWhileOpen[i];
            if (b != null && wasEnabled[i]) b.enabled = true;
        }

        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
    }

    void OnDisable()
    {
        Close(); // never leave the player frozen (e.g. when leaving Play Mode)
    }
}
