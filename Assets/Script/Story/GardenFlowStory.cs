using UnityEngine;

/// <summary>
/// The garden chain after the watering, as a sequence of gates (nothing here pays money or changes inventory):
///   1. chore quest (watering) completed -> Ông offers the feeding quest (garden_protein) by dialogue; Bà only points to Ông.
///   2. harvest -> place the vegetables on the plate (FeedingSpot) -> Ông calls out: take bran from the jar and mix it in.
///   3. the jar (QuestObjectiveTarget) gives the wrong powder (protein) with a joke message; the story goes on; only now the
///      chicken eats (FeedingSpot) -> garden_protein is ReadyToTurnIn.
///   4. the quest "xin_phep_ngu_trua" starts: the Player must say the nap sentence to Ông; Ông answers; only when that
///      dialogue is finished (its last button) is the nap allowed -> StoryBed lets the Player sleep (GardenFlowStory.NapAllowed).
///   5. sleeping completes garden_protein -> the existing transformation / missing-chicken story continues untouched.
/// It reuses QuestManager, QuestDefinition, DialogueUI and MessageBannerUI; each step fires once (stage only moves forward).
/// </summary>
public class GardenFlowStory : MonoBehaviour
{
    public enum Stage { Idle, OfferPending, Feeding, ReadyForNap, NapAllowed, Done }

    public static GardenFlowStory Instance { get; private set; }

    [Header("Quests")]
    [SerializeField] QuestManager questManager;
    [SerializeField] QuestDefinition choreQuest;  // grandparents_helper (watering)
    [SerializeField] QuestDefinition feedQuest;   // garden_protein (finished by sleeping)
    [SerializeField] QuestDefinition napQuest;    // xin_phep_ngu_trua

    [Header("Scene")]
    [SerializeField] Transform grandfather;
    [SerializeField] Transform grandmother;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] MessageBannerUI banner;
    [SerializeField] FeedingSpot feedingSpot;

    [Header("Texts")]
    [SerializeField] string offerBanner = "Ông đang gọi: ra gặp ông, ông có việc nhờ.";
    [SerializeField] string acceptFeedLabel = "Vâng, để cháu cho gà ăn";
    [SerializeField, TextArea(2, 4)] string[] offerLines =
    {
        "Cháu tưới rau xong rồi hả? Giỏi lắm!",
        "Giờ giúp ông cho con gà ăn nhé. Cháu hái ít rau ngoài vườn rồi đặt lên cái đĩa cho nó ăn."
    };
    [SerializeField, TextArea(2, 4)] string[] graOfferLines = { "Ông đang tìm cháu đấy, ra gặp ông đi." };
    [SerializeField, TextArea(2, 4)] string[] feedRemindLines = { "Cháu hái rau ngoài vườn rồi đặt lên cái đĩa cho gà ăn nhé." };
    [SerializeField, TextArea(2, 4)] string[] eatingLines = { "Cháu để con gà ăn cho xong đã. Trộn cả bột vào rồi, nó ăn xong là cháu nghỉ ngơi được." };
    [SerializeField, TextArea(2, 4)] string[] branRemindLines = { "Này cháu! Nhớ lấy thêm bột cám trong cái hũ ngoài vườn, trộn vào rau cho gà ăn nhé!" };
    [SerializeField] string napAskLabel = "Xin phép đi ngủ trưa";
    [SerializeField, TextArea(2, 4)] string[] napAskLines = { "Ơ, cháu có chuyện gì thế?" };
    [SerializeField] string napThanksLabel = "Dạ, cháu cảm ơn ông";
    [SerializeField, TextArea(2, 4)] string[] napSayLines =
    {
        "[Player] Chắc cháu xin phép vô ngủ trưa 1 chút cho khỏe ạ",
        "Thế vào giường trong phòng mà ngủ"
    };
    [SerializeField, TextArea(2, 4)] string[] napGrandmaLines = { "Cháu xin phép ông đi, ông ở ngoài sân kia." };
    [SerializeField, TextArea(2, 4)] string[] napAllowedLines = { "Cháu vào giường trong phòng mà ngủ đi." };
    [SerializeField] string feedBanner = "Nhận nhiệm vụ: cho gà ăn.";
    [SerializeField] string napBanner = "Gà ăn xong rồi. Giờ xin phép ông bà đi ngủ trưa.";
    [SerializeField] string allowedBanner = "Ông đã đồng ý. Vào giường trong phòng để ngủ.";

    Stage stage = Stage.Idle;
    bool remindShown;

    public Stage CurrentStage { get { return stage; } }
    /// <summary>True once the Player asked and Ông answered (the dialogue is finished). StoryBed reads this.</summary>
    public bool NapAllowed { get { return stage == Stage.NapAllowed || stage == Stage.Done; } }

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (questManager == null || choreQuest == null || feedQuest == null || napQuest == null) return;
        QuestState feed = questManager.GetState(feedQuest.questId);

        switch (stage)
        {
            case Stage.Idle:
                if (questManager.GetState(choreQuest.questId) == QuestState.Completed && feed == QuestState.NotStarted)
                {
                    stage = Stage.OfferPending;
                    if (banner != null && !string.IsNullOrEmpty(offerBanner)) banner.Show(offerBanner, 5f);
                }
                break;

            case Stage.Feeding:
                if (!remindShown && feedingSpot != null && feedingSpot.IsPlaced && dialogue != null && !dialogue.IsOpen)
                {
                    remindShown = true; // once: Ông calls out about the bran jar
                    dialogue.Open("Ông", branRemindLines, null, null);
                }
                if (feed == QuestState.ReadyToTurnIn)
                {
                    stage = Stage.ReadyForNap;
                    if (questManager.GetState(napQuest.questId) == QuestState.NotStarted) questManager.AcceptQuest(napQuest);
                    if (banner != null && !string.IsNullOrEmpty(napBanner)) banner.Show(napBanner, 5f);
                }
                break;

            case Stage.NapAllowed:
                if (feed == QuestState.Completed) stage = Stage.Done;
                break;
        }
    }

    /// <summary>
    /// Called by Ông's and Bà's dialogue scripts first. True = this story handled the conversation (the caller must not open its own).
    /// False outside this part of the story.
    /// </summary>
    public bool TryHandleGrandparent(Transform npc)
    {
        if (npc == null || dialogue == null || dialogue.IsOpen) return false;
        bool isGrandfather = npc == grandfather;
        bool isGrandmother = npc == grandmother;
        if (!isGrandfather && !isGrandmother) return false;
        string speaker = isGrandfather ? "Ông" : "Bà";

        switch (stage)
        {
            case Stage.OfferPending:
                if (isGrandfather) dialogue.Open(speaker, offerLines, acceptFeedLabel, AcceptFeed);
                else dialogue.Open(speaker, graOfferLines, null, null);
                return true;

            case Stage.Feeding:
                dialogue.Open(speaker, FeedingLines(), null, null);
                return true;

            case Stage.ReadyForNap:
                if (isGrandfather) dialogue.Open(speaker, napAskLines, napAskLabel, SayNapSentence);
                else dialogue.Open(speaker, napGrandmaLines, null, null);
                return true;

            case Stage.NapAllowed:
                dialogue.Open(speaker, napAllowedLines, null, null);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Button of Ông's offer: accepts the feeding quest. Once.</summary>
    // Ông's line follows the real step: no vegetable yet -> pick/place; placed -> bran jar; jar used -> let the chicken eat.
    string[] FeedingLines()
    {
        if (feedingSpot == null || !feedingSpot.IsPlaced) return feedRemindLines;
        if (questManager != null && feedQuest != null && questManager.GetStatus(feedQuest).Progress >= 3) return eatingLines;
        return branRemindLines;
    }

    public void AcceptFeed()
    {
        if (stage != Stage.OfferPending) return;
        stage = Stage.Feeding; // flip first
        questManager.AcceptQuest(feedQuest);
        if (banner != null && !string.IsNullOrEmpty(feedBanner)) banner.Show(feedBanner, 4f);
    }

    /// <summary>First button: the Player says the sentence (a second dialogue with the sentence and Ông's answer).</summary>
    void SayNapSentence()
    {
        if (stage != Stage.ReadyForNap || dialogue == null) return;
        dialogue.Open("Ông", napSayLines, napThanksLabel, GrantNap);
    }

    /// <summary>Last button of the nap dialogue = the dialogue is finished. Only now the nap is allowed. Once.</summary>
    public void GrantNap()
    {
        if (stage != Stage.ReadyForNap) return;
        stage = Stage.NapAllowed; // flip first
        questManager.ReportProgress(napQuest.objectiveKey, 1);
        questManager.TurnIn(napQuest);
        if (banner != null && !string.IsNullOrEmpty(allowedBanner)) banner.Show(allowedBanner, 5f);
    }
}
