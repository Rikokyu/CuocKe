using UnityEngine;

/// <summary>
/// Glue between the garden quests and the one-time chicken transformation. Only listens to the existing
/// QuestManager and calls its public API; it never touches combat, health or the animator.
///
///   grandparents_helper completed (tưới cây, claimed from Bà)  -> "garden_protein" starts (Bà explains the rest)
///   garden_protein completed (the player went to bed)           -> ONE-TIME placeholder muscle-chicken change,
///                                                                  training unlocked, "chuan_bi_tai_dau" starts
///
/// PLACEHOLDER: no muscle-chicken model exists in the project, so the change is scale + colour tint on the
/// existing model. Swap it for a real model later (ApplyPlaceholderTransform is the only place).
/// The flags live only in this play session (there is no save/load yet); they are reset together with the
/// QuestManager state when the scene loads, so they cannot disagree with the quests.
/// </summary>
public class ChickenTransformationTrigger : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] QuestManager questManager;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] MessageBannerUI banner;
    [SerializeField] QuestDefinition choreQuest;  // grandparents_helper
    [SerializeField] QuestDefinition gardenQuest; // garden_protein
    [SerializeField] QuestDefinition nextQuest;   // chuan_bi_tai_dau
    [SerializeField] Transform chicken;           // ChienKe_Pet

    [Header("Dialogue")]
    [SerializeField, TextArea(2, 4)] string[] gardenOfferLines =
    {
        "Cảm ơn cháu nhé! Cháu giúp bà thêm chút nữa là xong.",
        "Bao phân bón bà để ở góc sân. Cháu bón cho luống rau, rồi hái rau cho con gà ăn.",
        "Xong việc thì vào nhà đi ngủ cho khoẻ nhé."
    };

    [Header("Messages")]
    [SerializeField, TextArea(1, 3)] string transformedMessage = "Sáng dậy, con gà đã to khoẻ hẳn ra! Khu huấn luyện đã mở.";
    [Tooltip("Shown when the chicken is taken back at the training area (missing-chicken flow).")]
    [SerializeField, TextArea(1, 3)] string recoveredMessage = "Đã nhận lại gà! Nó to khoẻ hẳn ra. Khu huấn luyện đã mở.";

    [Header("Placeholder transform")]
    [SerializeField] float scaleMultiplier = 1.35f;
    [SerializeField] Color tint = new Color(0.85f, 0.25f, 0.2f);
    [SerializeField, Range(0f, 1f)] float tintStrength = 0.4f;

    public static ChickenTransformationTrigger Instance { get; private set; }

    /// <summary>True once the one-time transformation has happened (this play session).</summary>
    public static bool Transformed { get; private set; }

    /// <summary>Training stations ask this. If the scene has no such trigger nothing is locked.</summary>
    public static bool TrainingUnlocked { get { return Instance == null || (Transformed && (MissingChickenStory.Instance == null || ChickenRecovered)); } }

    /// <summary>True once the player took the chicken back at the training area (missing-chicken flow). Without that flow it is never needed.</summary>
    public static bool ChickenRecovered { get; private set; }

    /// <summary>Why training is still locked (shown by the trainer and the stations).</summary>
    public static string TrainingLockedMessage
    {
        get
        {
            if (Transformed && MissingChickenStory.Instance != null && !ChickenRecovered)
                return "Con gà đang mất tích. Hãy tìm lại nó trước đã.";
            return "Gà chưa sẵn sàng tập luyện. Hãy xong việc vườn ở nhà ông bà rồi đi ngủ đã.";
        }
    }

    bool pendingGardenInfo;

void Awake()
    {
        Instance = this;
        Transformed = false;
        ChickenRecovered = false;
    }

void OnDestroy()
    {
        if (Instance == this) { Instance = null; Transformed = false; ChickenRecovered = false; }
    }

    void OnEnable()
    {
        if (questManager != null) questManager.QuestCompleted += OnQuestCompleted;
    }

    void OnDisable()
    {
        if (questManager != null) questManager.QuestCompleted -= OnQuestCompleted;
    }

    void OnQuestCompleted(QuestStatus status)
    {
        if (status.Definition == choreQuest) StartGardenQuest();
        else if (status.Definition == gardenQuest) Transform();
    }

    void StartGardenQuest()
    {
        if (questManager == null || gardenQuest == null) return;
        if (questManager.GetState(gardenQuest.questId) != QuestState.NotStarted) return;
        questManager.AcceptQuest(gardenQuest);
        pendingGardenInfo = true; // Bà explains once the claim dialogue has closed (never forced: Esc/E closes it)
    }

    void Update()
    {
        if (!pendingGardenInfo || dialogue == null || dialogue.IsOpen) return;
        pendingGardenInfo = false;
        dialogue.Open("Bà", gardenOfferLines, null, null);
    }

    /// <summary>The one-time change. Guarded by Transformed, and QuestManager.TurnIn only completes the garden quest once.</summary>
void Transform()
    {
        if (Transformed) return;
        Transformed = true;

        ApplyPlaceholderTransform();

        // Missing-chicken flow: the chicken is gone in the morning; training and the next quest wait until it is taken back.
        if (MissingChickenStory.Instance != null) return;

        UnlockTrainingAndNextQuest(transformedMessage);
    }

    /// <summary>The player took the chicken back (called once by MissingChickenStory.TryTakeBack). Unlocks training and starts the next quest.</summary>
    public bool CompleteRecovery()
    {
        if (!Transformed || ChickenRecovered) return false;
        ChickenRecovered = true; // flip first: never twice
        UnlockTrainingAndNextQuest(recoveredMessage);
        return true;
    }

    void UnlockTrainingAndNextQuest(string message)
    {
        if (questManager != null && nextQuest != null && questManager.GetState(nextQuest.questId) == QuestState.NotStarted)
            questManager.AcceptQuest(nextQuest);
        if (banner != null && !string.IsNullOrEmpty(message)) banner.Show(message, 6f);
    }

    void ApplyPlaceholderTransform()
    {
        // Preferred: swap the normal_chicken model for the old chicken_pet model (same GameObject, position and follow state).
        StarterChicken starter = StarterChicken.Instance;
        if (starter == null && chicken != null) starter = chicken.GetComponent<StarterChicken>();
        if (starter != null)
        {
            starter.SwitchToPet();
            return;
        }

        if (chicken == null)
        {
            Debug.LogWarning("[ChickenTransformationTrigger] No chicken Transform assigned - transform skipped.", this);
            return;
        }

        chicken.localScale *= scaleMultiplier;

        foreach (var r in chicken.GetComponentsInChildren<Renderer>())
        {
            var mats = r.materials; // per-renderer instances: the shared assets stay untouched
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), tint, tintStrength));
                else if (m.HasProperty("_Color")) m.SetColor("_Color", Color.Lerp(m.GetColor("_Color"), tint, tintStrength));
            }
            r.materials = mats;
        }

        Debug.Log("[ChickenTransformationTrigger] Placeholder muscle-chicken transform applied (scale + tint only - no real model yet).");
    }
}
