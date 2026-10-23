using UnityEngine;

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
/// If the dialogue is closed before the button, nothing is lost: talk again.
/// </summary>
public class GrandfatherChickenRescue : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] float interactionDistance = 3f;
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

    bool promptShown;

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
        bool canInteract = InRange() && !dialogue.IsOpen;
        SetPrompt(canInteract);
        if (canInteract && Input.GetKeyDown(interactKey)) Interact();
    }

    /// <summary>Called by E in range. Public so a test can drive the same path.</summary>
    public void Interact()
    {
        if (dialogue == null || dialogue.IsOpen || questManager == null) return;

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
        else dialogue.Open("Ông", greetLines, keepButtonLabel, OnAskToKeep);

        SetPrompt(false);
    }

    void OnAskToKeep()
    {
        if (questManager == null) return;

        // flip first: TurnIn only works once, so a double press cannot repeat any of this
        if (questManager.GetState(questRescue.questId) == QuestState.Active)
        {
            questManager.ReportProgress(questRescue.objectiveKey, 1);
            questManager.TurnIn(questRescue);
        }

        MainChickenAcquire mc = chicken != null ? chicken : MainChickenAcquire.Instance;
        if (mc != null) mc.WakeUp(); // only acts while the chicken is Down; it follows once the wake-up animation ends
        else Debug.LogWarning("[GrandfatherChickenRescue] No MainChickenAcquire - the chicken cannot be woken.", this);

        if (dialogue != null) dialogue.Open("Ông", agreeLines, null, null);
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
}
