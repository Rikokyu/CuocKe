using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField, Min(0.5f)] float interactionDistance = 2.5f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string promptText = "Nhấn E để đi ngủ";
    [SerializeField] string notYetText = "Chưa buồn ngủ, còn việc ngoài vườn";
    [SerializeField] Transform player;

    [Header("Scene references")]
    [SerializeField] DialogueUI dialogue;
    [SerializeField] InteractionPromptUI prompt;
    [Tooltip("The Player's PlayerMovement. Only enabled/disabled here (never edited).")]
    [SerializeField] MonoBehaviour playerMovement;

    [Header("Sleep screen")]
    [SerializeField] string sleepText = "Zzz...";
    [SerializeField] string morningText = "Sáng hôm sau...";
    [SerializeField, Min(0.1f)] float fadeSeconds = 1f;
    [SerializeField, Min(0f)] float holdSeconds = 1.5f;

    public bool IsSleeping { get; private set; }

    bool promptShown;
    string shownText;
    GameObject overlay;
    CanvasGroup overlayGroup;
    TextMeshProUGUI overlayLabel;

    void Start()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null || questManager == null || gardenQuest == null || prompt == null)
            Debug.LogError("[StoryBed] Player / QuestManager / quest / prompt reference missing.", this);
    }

    void Update()
    {
        if (player == null || questManager == null || gardenQuest == null) return;

        QuestState s = questManager.GetState(gardenQuest.questId);
        bool ready = s == QuestState.ReadyToTurnIn;
        bool inRange = InRange() && !IsSleeping && (dialogue == null || !dialogue.IsOpen);

        SetPrompt(inRange && (ready || s == QuestState.Active), ready ? promptText : notYetText);
        if (inRange && ready && Input.GetKeyDown(interactKey)) TrySleep();
    }

    /// <summary>Starts sleeping if (and only if) the garden quest is ReadyToTurnIn. Public so a test can drive the same path.</summary>
    public bool TrySleep()
    {
        if (IsSleeping || questManager == null || gardenQuest == null) return false;
        if (questManager.GetState(gardenQuest.questId) != QuestState.ReadyToTurnIn) return false;

        IsSleeping = true;
        SetPrompt(false, null);
        StartCoroutine(SleepRoutine());
        return true;
    }

    /// <summary>The actual effect of sleeping: completes the garden quest once (-> transformation). Safe to call twice.</summary>
    public bool ApplySleep()
    {
        return questManager != null && gardenQuest != null && questManager.TurnIn(gardenQuest);
    }

    IEnumerator SleepRoutine()
    {
        if (playerMovement != null) playerMovement.enabled = false;
        BuildOverlay();

        yield return Fade(0f, 1f);
        overlayLabel.text = sleepText;
        yield return new WaitForSeconds(holdSeconds);

        ApplySleep(); // the screen is black while the chicken changes
        overlayLabel.text = morningText;
        yield return new WaitForSeconds(holdSeconds);

        yield return Fade(1f, 0f);
        EndSleep();
    }

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
        if (overlay != null) Destroy(overlay);
        overlay = null;
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
