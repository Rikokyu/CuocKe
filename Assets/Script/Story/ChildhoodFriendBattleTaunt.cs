using UnityEngine;

/// <summary>
/// The childhood friend's challenge and the first (lost) battle. Uses only existing systems: QuestManager /
/// QuestDefinition ("thu_suc_voi_ban_cu"), DialogueUI, BattleManager (StartBattle, CurrentBattleState,
/// firstBattleCompleted - read only) and MainChickenAcquire. It never edits damage/HP and never forces the result:
/// the loss comes from the existing combat + the existing first-battle balance in BattleManager.
///
/// Flow:
///   - Talk (E): the friend accepts the quest and challenges the player. With the Main Chicken awake the dialogue
///     ends in the button "Nhận lời thách đấu", which calls BattleManager.StartBattle(). Without the chicken he only
///     tells the player to bring it. (The proximity BattleTrigger on the enemy is switched off in the scene so
///     walking near the arena can no longer start the fight by itself.)
///   - After the real loss (BattleManager.Completed + firstBattleCompleted) he says the taunt line by himself once,
///     and the quest is completed once. Talking later only gives the taunt (if it was missed) or idle lines.
///   - While the delivery quest wants this NPC, the QuestObjectiveTarget on him owns the E key (no double dialogue).
/// </summary>
public class ChildhoodFriendBattleTaunt : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] float interactionDistance = 3f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string promptText = "Nhấn E để nói chuyện với Bạn thân";
    [SerializeField] Transform player;

    [Header("Scene references")]
    [SerializeField] DialogueUI dialogue;
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] QuestManager questManager;
    [SerializeField] QuestDefinition questBattle; // thu_suc_voi_ban_cu
    [Tooltip("Optional. The delivery target on the same NPC; when it wants the E key this script stays quiet. Empty = looked up on this object.")]
    [SerializeField] QuestObjectiveTarget deliveryTarget;

    [Header("Timing")]
    [Tooltip("Seconds after the loss before he taunts (the chicken is still getting up).")]
    [SerializeField, Min(0f)] float tauntDelaySeconds = 2.5f;

    [Header("Dialogue")]
    [SerializeField, TextArea(2, 4)] string[] challengeLines =
    {
        "Ơ, thằng IT về làng rồi à? Lâu lắm không gặp!",
        "Ơ, con gà này của mày á? Trông... hơi yếu đấy nhỉ.",
        "Thử sức với gà của tao xem sao? Đấu luôn một trận đi!"
    };
    [SerializeField] string acceptLabel = "Nhận lời thách đấu";
    [SerializeField, TextArea(2, 4)] string[] needChickenLines =
    {
        "Ơ, gà của mày đâu? Dắt nó theo rồi hẵng nói chuyện thách đấu!"
    };
    [SerializeField, TextArea(2, 4)] string[] fightingLines =
    {
        "Đang đấu rồi, nhìn đi đâu đấy?!"
    };
    [SerializeField, TextArea(2, 4)] string[] afterLossLines =
    {
        "Hahaha, thua rồi đó! Bữa nào tái đấu không? MÀY SỢ À"
    };
    [SerializeField, TextArea(2, 4)] string[] idleLines =
    {
        "Lo mà luyện tập đi, tao chờ ngày tái đấu đấy."
    };

    [Header("Player's answer to the taunt (opens only AFTER the friend's dialogue was closed)")]
    [SerializeField] string playerSpeakerName = "Tôi";
    [SerializeField, TextArea(2, 4)] string[] playerReplyLines =
    {
        "Mày đợi đó!"
    };

    /// <summary>Instance of the taunt NPC in the scene (null if the scene has none).</summary>
    public static ChildhoodFriendBattleTaunt Instance { get; private set; }

    /// <summary>True only after the first loss AND the taunt dialogue ("Bữa nào tái đấu không? MÀY SỢ À") was shown and closed. Quests that wait for the loss wait for this.</summary>
    public static bool AfterLossStoryDone { get; private set; }

    // The after-loss scene is strictly sequential: friend speaks -> (his dialogue closed) -> player answers -> (answer closed) -> quest completes
    // and the story moves on. Nothing in the next step starts while a dialogue is open.
    enum TauntPhase { None, FriendSpeaking, PlayerReplying, Done }
    TauntPhase phase = TauntPhase.None;
    int phaseFrame;
    int sequenceAdvancedFrame = -1;

    bool promptShown;
    float lossSeenAt = -1f;

    void Awake()
    {
        Instance = this;
        AfterLossStoryDone = false;
    }

    void OnDestroy()
    {
        if (Instance == this) { Instance = null; AfterLossStoryDone = false; }
    }

    void Start()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (deliveryTarget == null) deliveryTarget = GetComponent<QuestObjectiveTarget>();
        if (player == null) Debug.LogError("[ChildhoodFriendBattleTaunt] No Player found.", this);
        if (dialogue == null || prompt == null || questManager == null || questBattle == null)
            Debug.LogError("[ChildhoodFriendBattleTaunt] Missing a required reference.", this);
    }

    void Update()
    {
        if (player == null || dialogue == null || questManager == null) return;

        UpdateAutoTaunt();
        UpdateTauntSequence();

        bool inSequence = phase == TauntPhase.FriendSpeaking || phase == TauntPhase.PlayerReplying;
        if (RematchController.FriendAway) { SetPrompt(false); return; } // the friend is away: no prompt, no E
        bool deliveryOwnsE = deliveryTarget != null && deliveryTarget.ClaimsInteraction;
        // no new interaction while the scene runs, nor on the frame it advanced (the same E press that closed a dialogue must not reopen one)
        bool canInteract = InRange() && !dialogue.IsOpen && !deliveryOwnsE && !inSequence && sequenceAdvancedFrame != Time.frameCount;
        SetPrompt(canInteract);
        if (canInteract && Input.GetKeyDown(interactKey)) Interact();
    }

    /// <summary>True only after a REAL first-battle loss in this play session (the static flag alone could be stale).</summary>
    bool LostFirstBattle()
    {
        return BattleManager.firstBattleCompleted && BattleManager.Instance != null
            && BattleManager.Instance.CurrentBattleState == BattleManager.BattleState.Completed;
    }

    void UpdateAutoTaunt()
    {
        if (phase != TauntPhase.None) return; // the scene already started (or finished)
        if (questManager.GetState(questBattle.questId) != QuestState.Active || !LostFirstBattle())
        {
            lossSeenAt = -1f;
            return;
        }
        if (lossSeenAt < 0f) lossSeenAt = Time.time;
        if (Time.time - lossSeenAt < tauntDelaySeconds || dialogue.IsOpen) return;
        ShowAfterLoss();
    }

    /// <summary>Called by E in range. Public so a test can drive the same path.</summary>
    public void Interact()
    {
        if (RematchController.FriendAway) return;
        if (dialogue == null || dialogue.IsOpen || questManager == null) return;
        if (phase == TauntPhase.FriendSpeaking || phase == TauntPhase.PlayerReplying || sequenceAdvancedFrame == Time.frameCount) return;

        QuestState state = questManager.GetState(questBattle.questId);
        if (state == QuestState.NotStarted)
        {
            // the challenge (and its quest) only starts once the Main Chicken is awake
            if (MainChickenAcquire.Instance != null && MainChickenAcquire.Instance.IsAcquired) questManager.AcceptQuest(questBattle);
            OpenChallenge();
        }
        else if (state == QuestState.Active)
        {
            if (LostFirstBattle()) ShowAfterLoss();                       // the automatic taunt was missed
            else if (BattleManager.Instance != null && BattleManager.Instance.IsBattleActive)
                dialogue.Open("Bạn thân", fightingLines, null, null);
            else OpenChallenge();
        }
        else
        {
            // first quest done: once the chicken has transformed the friend talks about the rematch (RematchController)
            if (RematchController.Instance == null || !RematchController.Instance.TryInteract())
                dialogue.Open("Bạn thân", idleLines, null, null);
        }

        SetPrompt(false);
    }

    void OpenChallenge()
    {
        MainChickenAcquire mc = MainChickenAcquire.Instance;
        bool chickenReady = mc != null && mc.IsAcquired;
        if (chickenReady) dialogue.Open("Bạn thân", challengeLines, acceptLabel, StartFight);
        else dialogue.Open("Bạn thân", needChickenLines, null, null);
    }

    void StartFight()
    {
        if (BattleManager.Instance == null) { Debug.LogError("[ChildhoodFriendBattleTaunt] No BattleManager.", this); return; }
        if (questManager.GetState(questBattle.questId) != QuestState.Active) return;
        if (LostFirstBattle() || BattleManager.Instance.IsBattleActive) return;
        if (BattleArena.Instance != null) { BattleArena.Instance.RequestStart(BeginFirstFight, null); return; } // the fight starts once chicken + player are in the arena
        BeginFirstFight();
    }

    void BeginFirstFight()
    {
        if (BattleManager.Instance == null || questManager.GetState(questBattle.questId) != QuestState.Active) return;
        if (LostFirstBattle() || BattleManager.Instance.IsBattleActive) return;
        BattleManager.Instance.StartBattle(); // the existing AI + first-battle balance do the rest
    }

    /// <summary>Step 1: the friend taunts ("... MÀY SỢ À"). The quest is NOT completed here any more.</summary>
    void ShowAfterLoss()
    {
        if (phase != TauntPhase.None || dialogue.IsOpen) return; // one scene only, never over another dialogue
        if (questManager.GetState(questBattle.questId) != QuestState.Active) return;
        dialogue.Open("Bạn thân", afterLossLines, null, null);
        if (!dialogue.IsOpen) return; // could not open: try again later, nothing changed
        phase = TauntPhase.FriendSpeaking;
        phaseFrame = Time.frameCount;
        SetPrompt(false);
    }

    /// <summary>Advances the scene only when the current dialogue is closed: friend -> player's answer -> finish.</summary>
    void UpdateTauntSequence()
    {
        if (phase != TauntPhase.FriendSpeaking && phase != TauntPhase.PlayerReplying) return;
        if (dialogue.IsOpen || Time.frameCount <= phaseFrame) return;

        if (phase == TauntPhase.FriendSpeaking) ShowPlayerReply();
        else FinishAfterLoss();
    }

    /// <summary>Step 2: the player answers ("Mày đợi đó"), after the friend's dialogue was closed.</summary>
    void ShowPlayerReply()
    {
        sequenceAdvancedFrame = Time.frameCount;
        if (playerReplyLines == null || playerReplyLines.Length == 0) { FinishAfterLoss(); return; }
        dialogue.Open(playerSpeakerName, playerReplyLines, null, null);
        if (!dialogue.IsOpen) return; // another dialogue got in between: retry next frame (phase unchanged)
        phase = TauntPhase.PlayerReplying;
        phaseFrame = Time.frameCount;
    }

    /// <summary>Step 3: only now (every dialogue closed) the quest completes (one-shot) and the story moves on (grandmother's offer, ...).</summary>
    void FinishAfterLoss()
    {
        phase = TauntPhase.Done;
        sequenceAdvancedFrame = Time.frameCount;
        questManager.ReportProgress(questBattle.objectiveKey, 1);
        questManager.TurnIn(questBattle);
        AfterLossStoryDone = true;
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
