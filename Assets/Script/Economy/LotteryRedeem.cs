using UnityEngine;

/// <summary>
/// Lottery redeem at MrBeast (the QuestNPC_Villager). The tickets are bought at the shop (ShopNPC) and used up here:
///
///   E on MrBeast while the bag holds tickets
///     -> "Đổi thưởng" (or "Nói chuyện khác" = the NPC's normal quest talk)
///     -> MrBeast asks for CHẴN or LẺ
///     -> a number 1..100 is rolled: 50 even + 50 odd numbers = exactly 50% / 50%
///     -> right guess: prizePerTicket x (number of tickets used); wrong guess: nothing.
///
/// ALL tickets in the bag are used in one go and they are taken out of the inventory BEFORE the prize is paid,
/// so a prize can never be paid without using the tickets, and a second press cannot pay twice.
/// QuestNPC calls TryHandle / CanRedeemWith (same hook pattern as GardenFlowStory); every other NPC is unaffected.
/// </summary>
public class LotteryRedeem : MonoBehaviour
{
    public static LotteryRedeem Instance { get; private set; }

    [Header("Scene references")]
    [Tooltip("The QuestNPC of MrBeast (QuestNPC_Villager).")]
    [SerializeField] QuestNPC mrBeast;
    [SerializeField] ItemDefinition lotteryTicket;
    [SerializeField] CurrencyWallet wallet;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] MessageBannerUI banner;

    [Header("Prize")]
    [SerializeField, Min(1)] int prizePerTicket = 500;

    [Header("Texts ({0} = tickets, {1} = prize, {2} = the rolled number, {3} = CHẴN / LẺ)")]
    [Tooltip("Must match the speaker name MrBeast's NPCRoleAnimator listens to (talk pose).")]
    [SerializeField] string speakerName = "Dân làng";
    [SerializeField, TextArea(2, 4)] string[] offerLines = { "Ồ, cậu có {0} vé số trong túi đấy! Muốn đổi thưởng không?" };
    [SerializeField] string redeemLabel = "Đổi thưởng";
    [SerializeField] string talkLabel = "Nói chuyện khác";
    [SerializeField, TextArea(2, 4)] string[] guessLines =
    {
        "Luật đơn giản thôi: tôi quay một con số. Cậu đoán số đó CHẴN hay LẺ.",
        "Đoán đúng, {0} vé của cậu thưởng {1} VNĐ. Đoán sai thì mất vé. Cậu chọn gì nào?"
    };
    [SerializeField] string evenLabel = "Chẵn";
    [SerializeField] string oddLabel = "Lẻ";
    [SerializeField, TextArea(2, 4)] string[] winLines =
    {
        "Quay số... ra con số {2}! Số {2} là số {3}.",
        "Chúc mừng, cậu đoán đúng! Nhận {1} VNĐ nhé!"
    };
    [SerializeField, TextArea(2, 4)] string[] loseLines =
    {
        "Quay số... ra con số {2}! Số {2} là số {3}.",
        "Tiếc quá, cậu đoán sai rồi. Lần sau may mắn hơn nhé!"
    };

    void Awake() { Instance = this; }

    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>Tickets in the bag.</summary>
    public int TicketCount
    {
        get { return lotteryTicket != null && InventorySystem.Instance != null ? InventorySystem.Instance.GetCount(lotteryTicket) : 0; }
    }

    /// <summary>True if 'npc' is MrBeast and the player has tickets (QuestNPC then keeps its prompt open and lets E come here).</summary>
    public bool CanRedeemWith(QuestNPC npc)
    {
        return npc != null && npc == mrBeast && TicketCount > 0;
    }

    /// <summary>Called by QuestNPC.Interact. Opens the offer dialogue and returns true if this NPC handles the press.</summary>
    public bool TryHandle(QuestNPC npc)
    {
        if (!CanRedeemWith(npc) || dialogue == null || dialogue.IsOpen || wallet == null) return false;

        int tickets = TicketCount;
        dialogue.Open(speakerName, Format(offerLines, tickets, 0, 0, null), redeemLabel, OpenGuess, talkLabel, () => npc.Interact(true));
        return true;
    }

    void OpenGuess()
    {
        int tickets = TicketCount;
        if (tickets <= 0 || dialogue == null) return;
        dialogue.Open(speakerName, Format(guessLines, tickets, tickets * prizePerTicket, 0, null),
            evenLabel, () => Resolve(true), oddLabel, () => Resolve(false));
    }

    /// <summary>
    /// The draw. guessEven = the player's choice. Public so it can be tested without the dialogue.
    /// Returns the prize paid (0 = lost or nothing happened).
    /// </summary>
    public int Resolve(bool guessEven)
    {
        InventorySystem inv = InventorySystem.Instance;
        if (inv == null || lotteryTicket == null || wallet == null) return 0;

        int tickets = inv.GetCount(lotteryTicket);
        if (tickets <= 0) return 0;
        if (!inv.Remove(lotteryTicket, tickets)) return 0; // the tickets are used up BEFORE any prize exists

        int number = Random.Range(1, 101);           // 1..100: 50 even + 50 odd = exactly 50% each
        bool numberIsEven = number % 2 == 0;
        bool won = guessEven == numberIsEven;
        int prize = won ? tickets * prizePerTicket : 0;
        if (won) wallet.Add(prize);

        string kind = numberIsEven ? evenLabel.ToUpper() : oddLabel.ToUpper();
        if (dialogue != null)
            dialogue.Open(speakerName, Format(won ? winLines : loseLines, tickets, prize, number, kind), null, null);
        if (banner != null)
            banner.Show(won ? string.Format("Trúng thưởng! +{0} VNĐ", prize) : "Rất tiếc, bạn đoán sai.");
        Debug.Log("[Lottery] tickets=" + tickets + " number=" + number + " guessEven=" + guessEven + " won=" + won + " prize=" + prize);
        return prize;
    }

    static string[] Format(string[] templates, int tickets, int prize, int number, string kind)
    {
        string[] result = new string[templates.Length];
        for (int i = 0; i < templates.Length; i++)
        {
            try { result[i] = string.Format(templates[i], tickets, prize, number, kind); }
            catch (System.FormatException) { result[i] = templates[i]; }
        }
        return result;
    }
}
