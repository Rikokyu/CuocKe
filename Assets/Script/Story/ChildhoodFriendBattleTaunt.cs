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
    [SerializeField] float interactionDistance = 4f;
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

    [Header("Bà's package comes first (the challenge only follows the delivery)")]
    [Tooltip("village_delivery. When set and started (or about to be handed out by Bà): the friend does not challenge before the package is delivered; right after it he thanks the Player and THEN challenges him (same dialogue, the existing 'Nhận lời thách đấu' button starts the fight). Empty = the old behaviour.")]
    [SerializeField] QuestDefinition deliveryQuest;
    [SerializeField, TextArea(2, 4)] string[] needDeliveryLines =
    {
        "Ơ, thằng IT về làng rồi à? Mà sao tay không thế? Bà mày bảo gửi gói hàng cho tao, đâu rồi?"
    };
    [SerializeField, TextArea(2, 4)] string[] thanksLines =
    {
        "Ồ, gói hàng của bà mày gửi đấy hả? Cảm ơn nhé, đúng cái tao đang chờ!"
    };
    [SerializeField, Min(0f)] float thanksDelaySeconds = 1f;
    [SerializeField, Min(1f)] float thanksAutoDistance = 6f;

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

    [Header("Player's line during the FIRST fight (the chicken has not transformed yet and only runs)")]
    [SerializeField, TextArea(1, 3)] string[] firstFightPlayerLines =
    {
        "Ơ đá nó đi chứ, sao chạy không vậy?"
    };
    [Tooltip("Seconds after the first fight starts before the player says it (long enough to see the chicken only running).")]
    [SerializeField, Min(0f)] float firstFightLineDelaySeconds = 4f;
    [Tooltip("The line is shown on this toast (MessageBannerUI), NOT in the DialogueUI, so the fight is never interrupted. Empty = the banner found in the scene.")]
    [SerializeField] MessageBannerUI banner;
    [Tooltip("How long the line stays on the banner.")]
    [SerializeField, Min(0.5f)] float firstFightLineSeconds = 4f;

    bool firstFightLineShown;
    float firstFightStartedAt = -1f;

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
    bool thanksShown;
    float deliveredSeenAt = -1f;

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
        if (banner == null) banner = FindFirstObjectByType<MessageBannerUI>(FindObjectsInactive.Include);
        if (player == null) Debug.LogError("[ChildhoodFriendBattleTaunt] No Player found.", this);
        if (dialogue == null || prompt == null || questManager == null || questBattle == null)
            Debug.LogError("[ChildhoodFriendBattleTaunt] Missing a required reference.", this);
    }

    void Update()
    {
        if (player == null || dialogue == null || questManager == null) return;

        UpdateAutoTaunt();
        UpdateTauntSequence();
        UpdateThanksAuto();
        UpdateFirstFightLine();

        bool inSequence = phase == TauntPhase.FriendSpeaking || phase == TauntPhase.PlayerReplying;
        if (RematchController.FriendAway) { SetPrompt(false); return; } // the friend is away: no prompt, no E
        bool deliveryOwnsE = deliveryTarget != null && deliveryTarget.ClaimsInteraction;
        // no new interaction while the scene runs, nor on the frame it advanced (the same E press that closed a dialogue must not reopen one)
        bool canInteract = InRange() && !dialogue.IsOpen && !deliveryOwnsE && !inSequence && sequenceAdvancedFrame != Time.frameCount && InteractObject.IsAimed(this);
        SetPrompt(canInteract);
        if (canInteract && Input.GetKeyDown(interactKey)) Interact();
    }

    /// <summary>Once, in the first (scripted) fight while the chicken has not transformed: the player wonders why it only runs.</summary>
void UpdateFirstFightLine()
    {
        if (firstFightLineShown || firstFightPlayerLines == null || firstFightPlayerLines.Length == 0) return;
        BattleManager bm = BattleManager.Instance;
        bool applies = bm != null && bm.IsBattleActive && bm.isFirstBattle && !BattleManager.firstBattleCompleted && !ChickenTransformationTrigger.Transformed;
        if (!applies) { firstFightStartedAt = -1f; return; }
        if (firstFightStartedAt < 0f) firstFightStartedAt = Time.time;
        if (Time.time - firstFightStartedAt < firstFightLineDelaySeconds) return;

        // The player's line goes on the banner (one toast), never into the DialogueUI: no dialogue opens and the fight keeps running.
        firstFightLineShown = true; // once only
        if (banner == null) { Debug.LogWarning("[ChildhoodFriendBattleTaunt] No MessageBannerUI - the player's line is not shown.", this); return; }
        banner.Show(playerSpeakerName + ": " + string.Join(" ", firstFightPlayerLines), firstFightLineSeconds);
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
            // Bà's package first: until it is delivered the friend only asks for it (no challenge, no fight)
            if (DeliveryPending()) { dialogue.Open("Bạn thân", needDeliveryLines, null, null); SetPrompt(false); return; }
            // the challenge (and its quest) only starts once the Main Chicken is awake
            if (MainChickenAcquire.Instance != null && MainChickenAcquire.Instance.IsAcquired) questManager.AcceptQuest(questBattle);
            if (!(Delivered() && OpenThanksAndChallenge())) OpenChallenge(); // after Bà's package: thanks first, then the challenge
        }
        else if (state == QuestState.Active)
        {
            if (LostFirstBattle()) ShowAfterLoss();                       // the automatic taunt was missed
            else if (BattleManager.Instance != null && BattleManager.Instance.IsBattleActive)
                dialogue.Open("Bạn thân", fightingLines, null, null);
            else if (DeliveryPending()) dialogue.Open("Bạn thân", needDeliveryLines, null, null);
            else if (!(Delivered() && OpenThanksAndChallenge())) OpenChallenge();
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
        if (DeliveryPending()) return; // no fight before Bà's package is delivered
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
        if (deliveryQuest != null && questManager.GetState(deliveryQuest.questId) == QuestState.ReadyToTurnIn) questManager.Track(deliveryQuest); // HUD + guide go back to Bà: she pays now
    }

    bool Delivered()
    {
        if (deliveryQuest == null) return false;
        QuestState s = questManager.GetState(deliveryQuest.questId);
        return s == QuestState.ReadyToTurnIn || s == QuestState.Completed;
    }

    /// <summary>Bà's package was taken on but is not delivered yet.</summary>
    bool DeliveryPending()
    {
        if (deliveryQuest == null) return false;
        QuestState s = questManager.GetState(deliveryQuest.questId);
        if (s == QuestState.Active) return true;
        // Ông kept the chicken but Bà has not handed the package out yet: no challenge / fight before the delivery
        MainChickenAcquire mc = MainChickenAcquire.Instance;
        return s == QuestState.NotStarted && mc != null && mc.IsAcquired;
    }



    /// <summary>After the delivery: the friend thanks the Player FIRST, then (same dialogue) challenges him. The fight starts only from the dialogue's button
    /// (the existing StartFight, arena rules unchanged). Once only.</summary>
    bool OpenThanksAndChallenge()
    {
        if (thanksShown || dialogue == null || dialogue.IsOpen || RematchController.FriendAway) return false;
        if (LostFirstBattle() || (BattleManager.Instance != null && BattleManager.Instance.IsBattleActive)) return false;
        MainChickenAcquire mc = MainChickenAcquire.Instance;
        bool chickenReady = mc != null && mc.IsAcquired;
        if (chickenReady && questManager.GetState(questBattle.questId) == QuestState.NotStarted) questManager.AcceptQuest(questBattle);
        thanksShown = true; // flip first: never twice
        string[] tail = chickenReady ? challengeLines : needChickenLines;
        string[] all = new string[thanksLines.Length + tail.Length];
        thanksLines.CopyTo(all, 0);
        tail.CopyTo(all, thanksLines.Length);
        if (chickenReady) dialogue.Open("Bạn thân", all, acceptLabel, StartFight);
        else dialogue.Open("Bạn thân", all, null, null);
        if (!dialogue.IsOpen) { thanksShown = false; return false; } // could not open: nothing changed
        SetPrompt(false);
        return true;
    }

    /// <summary>The package reached him: the first quest (HUD + guide) moves to him at once; if the Player is near he thanks him by himself after a short delay.</summary>
    void UpdateThanksAuto()
    {
        if (deliveryQuest == null || thanksShown || !Delivered()) { deliveredSeenAt = -1f; return; }
        if (LostFirstBattle() || questManager.GetState(questBattle.questId) == QuestState.Completed) { thanksShown = true; return; } // past that point
        MainChickenAcquire mc = MainChickenAcquire.Instance;
        if (mc != null && mc.IsAcquired && questManager.GetState(questBattle.questId) == QuestState.NotStarted) questManager.AcceptQuest(questBattle);
        if (deliveredSeenAt < 0f) deliveredSeenAt = Time.time;
        if (Time.time - deliveredSeenAt < thanksDelaySeconds || dialogue.IsOpen || phase != TauntPhase.None || RematchController.FriendAway) return;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        if (d.magnitude > thanksAutoDistance) return; // walked away: he says it when talked to
        OpenThanksAndChallenge();
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
