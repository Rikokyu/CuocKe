using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The rematch against the childhood friend (quest "chuan_bi_tai_dau", objective key "prepare_rematch").
/// Uses only existing systems: QuestManager (progress + one-shot TurnIn), DialogueUI, MessageBannerUI, ChickenStats
/// (its Changed event = one finished training session), BattleManager (StartBattle, state, the fighters it already
/// references) and HealthSystem / PetCombatAI public methods. No combat script is edited.
///
/// Flow (assumptions are in PROJECT_STATUS.md):
///   - The quest starts when the chicken transforms (ChickenTransformationTrigger, unchanged).
///   - Quest progress follows the chicken's total training hours (ChickenStats.TrainingHours, capped at the required amount). QuestManager ignores it unless the
///     quest is Active and clamps at requiredAmount, so nothing counts early or twice.
///   - At the required amount the quest is ReadyToTurnIn: only then does the friend offer the rematch.
///   - The rematch uses the NORMAL balance (BattleManager.isFirstBattle is switched off; that flag otherwise stays true
///     forever and would re-apply the scripted first-battle damage).
///   - Win: TurnIn(quest) once. Loss: the chicken is revived and the rooster healed so the player can train and retry.
/// The friend's E key is owned by ChildhoodFriendBattleTaunt, which calls TryInteract() once the first quest is done.
/// </summary>
public class RematchController : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] QuestManager questManager;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] MessageBannerUI banner;
    [SerializeField] QuestDefinition rematchQuest; // tai_dau_ban_than: the rematch itself (starts when the chicken is taken back)
    [Tooltip("chuan_bi_tai_dau: earn money + train. Opened ONLY after the rematch quest is Completed; completes by itself at the required training hours.")]
    [SerializeField] QuestDefinition trainingQuest;
    [Tooltip("village_delivery (Bà's delivery to the friend, handed out by Bà after Ông keeps the chicken). Used here only to keep the money / training quest closed until Bà has paid it, and to put the HUD back on it after a fight. The thanks + first challenge live in ChildhoodFriendBattleTaunt.")]
    [SerializeField] QuestDefinition deliveryQuest;
    [Tooltip("The friend's rooster (GuguGaga). Hidden together with the friend while he is away. Its GameObject stays active.")]
    [SerializeField] GameObject friendRooster;
    [SerializeField] ChickenStats chickenStats;

    [Header("Rematch")]
    [Tooltip("The chicken is placed this far from the rooster when the fight starts (the pet only fights what is within its detection radius).")]
    [SerializeField, Min(1f)] float arenaStartDistance = 4f;
    [SerializeField, Min(0f)] float victoryDialogueDelay = 2.5f;
    [SerializeField, Min(0f)] float getUpDelay = 3f;
    [SerializeField, Min(1f)] float getUpTimeout = 8f;
    [SerializeField] string idleStateName = "ChickenIdle";

    [Header("First rematch: lost by script (real damage through BattleManager's first-battle balance)")]
    [Tooltip("On: the FIRST rematch uses BattleManager's first-battle balance (damage 1 vs 15), so the chicken loses by real damage; then the friend advises training and the rematch quest completes. Off: normal balance (winnable), the old behaviour.")]
    [SerializeField] bool scriptedLoss = true;
    [SerializeField, TextArea(2, 4)] string[] adviceLines =
    {
        "Thấy chưa? Gà mày còn non lắm.",
        "Đi huấn luyện thêm đi. Đánh bại Chị Ba và Chú Tư rồi quay lại đấu với tao!"
    };

    [Header("Arrival gate (new): combat does not start until Player AND chicken are both inside this radius of the opponent")]
    [SerializeField, Min(1f)] float arenaRadius = 6f;
    [SerializeField, Min(1f)] float guidanceReminderSeconds = 6f;
    [SerializeField] string guidanceMessage = "Hãy đến chỗ bạn thân để bắt đầu trận đấu!";

    [Header("Dialogue ({0} = sessions done, {1} = sessions needed)")]
    [SerializeField, TextArea(2, 4)] string[] notReadyLines =
    {
        "Gà mày mới tập {0}/{1} buổi thôi à? Chưa đủ đô để đấu với tao đâu.",
        "Không có tiền thì đi nhặt ve chai, giao hàng đi. Mua thảm tập ở chợ, trải ở khu huấn luyện rồi mà luyện."
    };
    [SerializeField, TextArea(2, 4)] string[] readyLines =
    {
        "Ồ, nhìn con gà mày khác hẳn rồi đấy. Sẵn sàng tái đấu chưa?"
    };
    [SerializeField, TextArea(2, 4)] string[] retryLines =
    {
        "Sao, chưa phục à? Tập thêm cũng được, hoặc đấu lại luôn!"
    };
    [SerializeField] string rematchLabel = "Tái đấu!";
    // M11: lines that follow the real story state (all optional text, edited in the Inspector)
    [SerializeField, TextArea(2, 4)] string[] transformedLines =
    {
        "Khoan... gà mày sao tự dưng to ra cả vòng thế kia? Mày cho nó ăn cái gì vậy?"
    };
    [SerializeField, TextArea(2, 4)] string[] tournamentOpenLines =
    {
        "Gà mày mạnh lên thật rồi đấy. Nghe nói giải làng đang mở, chị Ba với chú Tư đang chờ.",
        "Hạ được cả hai người đó rồi hẵng quay lại nói chuyện với tao."
    };
    [SerializeField, TextArea(2, 4)] string[] tournamentPartLines =
    {
        "Nghe nói mày hạ được {0}/{1} người ở giải làng rồi hả? Hạ nốt người còn lại đi, rồi quay lại đây."
    };
    [SerializeField, TextArea(2, 4)] string[] tournamentDoneLines =
    {
        "Hạ cả chị Ba lẫn chú Tư, lấy đủ {0} huy chương rồi cơ à? Gà mày giờ không phải dạng vừa đâu.",
        "Được rồi. Lần này tao không đùa nữa."
    };
    [SerializeField, TextArea(2, 4)] string[] fightingLines = { "Đang đấu rồi, nhìn đi đâu đấy?!" };
    [SerializeField, TextArea(2, 4)] string[] recoveringLines = { "Đợi gà mày đứng dậy đã rồi hẵng nói chuyện." };
    [SerializeField, TextArea(2, 4)] string[] needChickenLines = { "Gà của mày đâu? Dắt nó theo rồi hẵng tái đấu!" };
    [SerializeField, TextArea(2, 4)] string[] wonLines =
    {
        "Hả?! Tao thua thật à... Công nhận gà mày khoẻ thật đấy.",
        "Nhưng đừng tưởng tao bỏ cuộc nhé. Còn lâu mới xong!"
    };

    [Header("Messages")]
    [SerializeField] string startMessage = "Trận tái đấu bắt đầu!";
    [SerializeField] string lostMessage = "Thua rồi! Luyện thêm rồi nói chuyện với bạn để đấu lại.";

    public static RematchController Instance { get; private set; }

    /// <summary>True once the rematch was won (this play session; there is no save/load yet).</summary>
    public static bool RematchWon { get; private set; }
    /// <summary>True while the friend is out of the story: from the end of the first rematch's victory talk until the
    /// final-boss gate opens (both tournament opponents beaten). While true he is invisible and cannot be talked to.</summary>
    public static bool FriendAway { get; private set; }

    bool fightActive;
    bool recovering;
    bool lostOnce;
    bool transformCommentShown;
    Transform player;
    bool pendingArrival;
    float nextGuidanceAt;
    float wonAt = -1f;
    int wonDialogueFrame;
    Renderer[] friendRenderers;
    QuestObjectiveTarget friendDeliveryTarget;
    bool wonDialogueShown;
    bool scriptedFight;       // this fight uses BattleManager's first-battle balance
    bool scriptedLossDone;    // the one scripted rematch has been lost
    float adviceAt = -1f;     // >= 0 while the friend's advice / completion is pending
    bool adviceShown;
    int adviceFrame;

    void Awake()
    {
        Instance = this;
        RematchWon = false;
        FriendAway = false;
    }

    void OnDestroy()
    {
        if (Instance == this) { Instance = null; RematchWon = false; FriendAway = false; }
    }

    void OnEnable()
    {
        if (chickenStats != null) chickenStats.Changed += OnStatsChanged;
    }

    void OnDisable()
    {
        if (chickenStats != null) chickenStats.Changed -= OnStatsChanged;
    }

    /// <summary>The quest's 'buổi tập' = the chicken's total training hours (ChickenStats.TrainingHours): a 2-hour session
    /// shows 2/N, 5 hours in total shows 5/N. Progress is raised to min(TrainingHours, required); it never goes down and
    /// stat-only changes (no new hours) add nothing. Only counts while the quest is Active.</summary>
    void OnStatsChanged()
    {
        if (questManager == null || trainingQuest == null || chickenStats == null) return;
        QuestStatus status = questManager.GetStatus(trainingQuest);
        if (status == null || status.State != QuestState.Active) return;
        int target = Mathf.Min(chickenStats.TrainingHours, trainingQuest.requiredAmount);
        int missing = target - status.Progress;
        if (missing > 0) questManager.ReportProgress(trainingQuest.objectiveKey, missing);
    }

    /// <summary>
    /// Called by the friend's dialogue once the first quest is done. Returns false when this controller has nothing to say
    /// yet (the chicken has not transformed), so the caller falls back to his idle line.
    /// </summary>
    public bool TryInteract()
    {
        if (dialogue == null || questManager == null || rematchQuest == null) return false;

        BattleManager bm = BattleManager.Instance;
        switch (questManager.GetState(rematchQuest.questId))
        {
            case QuestState.NotStarted:
                return false;

            case QuestState.Active:
            case QuestState.ReadyToTurnIn:
                if (fightActive || (bm != null && bm.IsBattleActive)) dialogue.Open("Bạn thân", fightingLines, null, null);
                else if (recovering) dialogue.Open("Bạn thân", recoveringLines, null, null);
                else
                {
                    string[] offer = lostOnce ? retryLines : readyLines;
                    if (!transformCommentShown && ChickenTransformationTrigger.Transformed)
                    {
                        transformCommentShown = true; // one-time reaction to the transformed chicken
                        offer = Concat(transformedLines, offer);
                    }
                    dialogue.Open("Bạn thân", offer, rematchLabel, StartRematch);
                }
                return true;

            default: // Completed: the lines follow the real tournament state, then M12 takes over once eligible
                if (FinalBossController.Instance != null && FinalBossController.Instance.TryInteract()) return true;
                dialogue.Open("Bạn thân", CompletedLines(), null, null);
                return true;
        }
    }

    /// <summary>The dialogue button. Public so a test can drive the same path. Does not start the fight
    /// immediately: if the Player or the chicken is not yet at the rooster, this only shows arena guidance and
    /// the fight begins once both actually arrive (see UpdatePendingArrival). If both are already there, it
    /// starts at once, same as before.</summary>
    public void StartRematch()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null || questManager == null || rematchQuest == null) return;
        if (fightActive || recovering || bm.IsBattleActive || adviceAt >= 0f) return; // adviceAt: the scripted loss is still being wrapped up
        QuestState rematchState = questManager.GetState(rematchQuest.questId);
        if (rematchState != QuestState.Active && rematchState != QuestState.ReadyToTurnIn) return; // quest not started yet / already won

        MainChickenAcquire mc = MainChickenAcquire.Instance;
        HealthSystem pet = bm.mainChickenHealth;
        HealthSystem foe = bm.enemyHealth;
        if (pet == null || foe == null) { Debug.LogError("[RematchController] BattleManager fighters are not assigned.", this); return; }
        if ((mc != null && !mc.IsAcquired) || pet.IsDead)
        {
            if (dialogue != null && !dialogue.IsOpen) dialogue.Open("Bạn thân", needChickenLines, null, null);
            return;
        }

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (player != null && !ArrivedAtArena(pet.transform.position, foe.transform.position))
        {
            RoosterCombatAI foeCombat0 = foe.GetComponent<RoosterCombatAI>();
            if (foeCombat0 != null) foeCombat0.autoEngage = false; // defensive: see TournamentManager's same fix
            pendingArrival = true;
            nextGuidanceAt = 0f;
            return;
        }

        BeginFight(pet, foe);
    }

    bool ArrivedAtArena(Vector3 petPos, Vector3 arenaCenter)
    {
        // the real arena zone (BattleArena); the foe-based check below is only the fallback when no arena exists
        if (BattleArena.Instance != null) return BattleArena.Instance.Arrived(petPos, player != null ? player.position : petPos);
        return WithinArena(petPos, arenaCenter) && (player == null || WithinArena(player.position, arenaCenter));
    }

    bool WithinArena(Vector3 pos, Vector3 arenaCenter)
    {
        Vector3 d = pos - arenaCenter;
        d.y = 0f;
        return d.sqrMagnitude <= arenaRadius * arenaRadius;
    }

    void UpdatePendingArrival()
    {
        BattleManager bm = BattleManager.Instance;
        HealthSystem pet = bm != null ? bm.mainChickenHealth : null;
        HealthSystem foe = bm != null ? bm.enemyHealth : null;
        if (bm == null || bm.IsBattleActive || pet == null || pet.IsDead || foe == null || player == null)
        {
            pendingArrival = false; return; // something changed under us; just cancel the guidance, no fight starts
        }

        if (ArrivedAtArena(pet.transform.position, foe.transform.position))
        {
            pendingArrival = false;
            BeginFight(pet, foe);
            return;
        }

        if (Time.time >= nextGuidanceAt)
        {
            nextGuidanceAt = Time.time + guidanceReminderSeconds;
            if (banner != null && !string.IsNullOrEmpty(guidanceMessage)) banner.Show(guidanceMessage, 4f);
            if (BattleArena.Instance != null) BattleArena.Instance.ShowMarker();
        }
    }

    /// <summary>The actual fight start (unchanged from before this feature, just extracted so it can be called
    /// either right away or once arrival is confirmed).</summary>
    void BeginFight(HealthSystem pet, HealthSystem foe)
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;

        // fresh fighters
        if (foe.IsDead) foe.Revive(1f);
        else if (foe.currentHealth < foe.maxHealth) foe.Heal(foe.maxHealth - foe.currentHealth);
        if (pet.currentHealth < pet.maxHealth) pet.Heal(pet.maxHealth - pet.currentHealth);

        if (BattleArena.Instance != null) BattleArena.Instance.BringFoe(foe);
        PlaceChickenAtArena(pet, foe);

        scriptedFight = scriptedLoss && !scriptedLossDone;
        bm.isFirstBattle = scriptedFight; // true: BattleManager's first-battle balance, the chicken loses by REAL damage; false: normal balance, trained stats decide
        fightActive = true;
        bm.StartBattle();
        if (!bm.IsBattleActive) { fightActive = false; return; }

        if (banner != null && !string.IsNullOrEmpty(startMessage)) banner.Show(startMessage, 3f);
    }

    void PlaceChickenAtArena(HealthSystem pet, HealthSystem foe)
    {
        Vector3 foePos = foe.transform.position;
        Vector3 away = pet.transform.position - foePos;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = Vector3.back;
        Vector3 spot = foePos + away.normalized * arenaStartDistance;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(spot, out hit, 3f, NavMesh.AllAreas)) spot = hit.position;

        NavMeshAgent agent = pet.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled) agent.Warp(spot);
        else pet.transform.position = spot;

        Vector3 look = foePos - spot;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) pet.transform.rotation = Quaternion.LookRotation(look);
    }

    void Update()
    {
        if (pendingArrival) UpdatePendingArrival();
        if (fightActive) PollFight();

        if (wonAt >= 0f && !wonDialogueShown && dialogue != null && !dialogue.IsOpen && Time.time - wonAt >= victoryDialogueDelay)
        {
            wonDialogueShown = true;
            wonDialogueFrame = Time.frameCount;
            dialogue.Open("Bạn thân", wonLines, null, null);
        }
        UpdateAdvice();
        UpdateFriendPresence();
        UpdateTrainingQuest();
    }

    /// <summary>The money / training quest exists only AFTER the rematch is won: it is opened once the victory talk is over,
    /// and completes by itself at the required training hours (the friend is away, so nobody can take the turn-in).
    /// Derived from the real quest states every frame (AcceptQuest / TurnIn only act on NotStarted / ReadyToTurnIn), so
    /// it can neither open early nor twice.</summary>
    void UpdateTrainingQuest()
    {
        if (questManager == null || rematchQuest == null || trainingQuest == null) return;
        if (questManager.GetState(rematchQuest.questId) != QuestState.Completed) return;
        if (VictoryTalkPending()) return;
        if (deliveryQuest != null && questManager.GetState(deliveryQuest.questId) != QuestState.Completed) return; // Bà must have paid the delivery first

        QuestState s = questManager.GetState(trainingQuest.questId);
        if (s == QuestState.NotStarted)
        {
            questManager.AcceptQuest(trainingQuest);
            OnStatsChanged(); // hours already trained count at once
        }
        else if (s == QuestState.ReadyToTurnIn)
        {
            questManager.TurnIn(trainingQuest); // opens the village tournament (TournamentManager watches this quest)
        }
    }

    bool VictoryTalkPending()
    {
        return wonAt >= 0f && !(wonDialogueShown && dialogue != null && Time.frameCount > wonDialogueFrame && !dialogue.IsOpen);
    }

    Renderer[] roosterRenderers;
    Collider[] roosterColliders;
    bool[] roosterColliderWasOn;

    /// <summary>Hides / shows the friend's rooster (renderers + colliders it had on). The GameObject stays active.</summary>
    void SetRoosterHidden(bool hidden)
    {
        if (friendRooster == null) return;
        if (roosterRenderers == null)
        {
            roosterRenderers = friendRooster.GetComponentsInChildren<Renderer>(true);
            roosterColliders = friendRooster.GetComponentsInChildren<Collider>(true);
            roosterColliderWasOn = new bool[roosterColliders.Length];
        }
        for (int i = 0; i < roosterRenderers.Length; i++) if (roosterRenderers[i] != null) roosterRenderers[i].enabled = !hidden;
        for (int i = 0; i < roosterColliders.Length; i++)
        {
            if (roosterColliders[i] == null) continue;
            if (hidden) { roosterColliderWasOn[i] = roosterColliders[i].enabled; roosterColliders[i].enabled = false; }
            else roosterColliders[i].enabled = roosterColliderWasOn[i] || roosterColliders[i].enabled;
        }
    }

    // After the first rematch is won (and its victory talk is over) the friend leaves; he comes back only when the
    // final-boss gate opens. Derived from the real quest/medal state every frame, so it cannot get out of step.
    void UpdateFriendPresence()
    {
        bool away = false;
        if (questManager != null && rematchQuest != null && questManager.GetState(rematchQuest.questId) == QuestState.Completed)
        {
            bool victoryTalkPending = wonAt >= 0f && !(wonDialogueShown && dialogue != null && Time.frameCount > wonDialogueFrame && !dialogue.IsOpen);
            FinalBossController boss = FinalBossController.Instance;
            bool gateOpen = boss != null && boss.GateOpen;
            away = !victoryTalkPending && !gateOpen;
        }
        if (away == FriendAway) return;
        FriendAway = away;
        if (friendRenderers == null) friendRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < friendRenderers.Length; i++) if (friendRenderers[i] != null) friendRenderers[i].enabled = !away;
        if (friendDeliveryTarget == null) friendDeliveryTarget = GetComponent<QuestObjectiveTarget>();
        if (friendDeliveryTarget != null) friendDeliveryTarget.enabled = !away; // the delivery point is paused while he is away
        SetRoosterHidden(away); // his chicken leaves with him
    }

    void PollFight()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) { fightActive = false; return; }
        if (bm.CurrentBattleState != BattleManager.BattleState.Completed) return;

        fightActive = false;
        bool wasScripted = scriptedFight;
        scriptedFight = false;
        bm.isFirstBattle = false; // BattleManager already restored the damage values; never leave the first-battle flag on for other fights
        bool foeDead = bm.enemyHealth != null && bm.enemyHealth.IsDead;
        if (foeDead) OnVictory();
        else if (wasScripted) OnScriptedLoss();
        else OnDefeat();
    }

    void OnVictory()
    {
        RematchWon = true;
        wonAt = Time.time;
        questManager.ReportProgress(rematchQuest.objectiveKey, 1); // the single objective of the rematch quest
        questManager.TurnIn(rematchQuest); // one-shot: the state flips before anything else happens
    }

    void OnDefeat()
    {
        lostOnce = true;
        if (!recovering) StartCoroutine(RecoverAfterLoss());
    }

    IEnumerator RecoverAfterLoss()
    {
        recovering = true;
        if (banner != null && !string.IsNullOrEmpty(lostMessage)) banner.Show(lostMessage, 5f);

        BattleManager bm = BattleManager.Instance;
        HealthSystem pet = bm != null ? bm.mainChickenHealth : null;
        HealthSystem foe = bm != null ? bm.enemyHealth : null;
        PetCombatAI combat = bm != null ? bm.mainChickenCombat : null;

        // PostLossSequence is one-shot (first loss only). If it has not run, it owns the recovery.
        PostLossSequence first = FindFirstObjectByType<PostLossSequence>();
        if (first != null && !first.HasStarted) { recovering = false; yield break; }

        yield return new WaitForSeconds(getUpDelay);

        if (foe != null && !foe.IsDead && foe.currentHealth < foe.maxHealth) foe.Heal(foe.maxHealth - foe.currentHealth);

        if (pet != null && pet.Revive(1f))
        {
            Animator anim = pet.animator != null ? pet.animator : pet.GetComponent<Animator>();
            if (anim != null)
            {
                if (HasParameter(anim, "isRunning", AnimatorControllerParameterType.Bool)) anim.SetBool("isRunning", false);
                if (HasParameter(anim, "IsPushing", AnimatorControllerParameterType.Bool)) anim.SetBool("IsPushing", false);
                if (HasParameter(anim, "WakeUp", AnimatorControllerParameterType.Trigger)) anim.SetTrigger("WakeUp");

                yield return null;
                float waited = 0f;
                while (waited < getUpTimeout && !anim.GetCurrentAnimatorStateInfo(0).IsName(idleStateName))
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
            }
        }

        if (combat != null) combat.ResumeAfterRecovery();
        recovering = false;
    }

    /// <summary>The first rematch is over (lost by script, with real damage): the chicken gets up as after any loss, then the friend advises training;
    /// only after his dialogue is closed does the rematch quest complete (that opens the tournament, and the training quest once Bà has paid).</summary>
    void OnScriptedLoss()
    {
        lostOnce = true;
        scriptedLossDone = true;
        adviceAt = Time.time;
        adviceShown = false;
        if (!recovering) StartCoroutine(RecoverAfterLoss());
    }

    void UpdateAdvice()
    {
        if (adviceAt < 0f || dialogue == null) return;
        if (!adviceShown)
        {
            if (dialogue.IsOpen || Time.time - adviceAt < victoryDialogueDelay) return;
            adviceShown = true;
            adviceFrame = Time.frameCount;
            dialogue.Open("Bạn thân", adviceLines, null, null);
            return;
        }
        if (Time.frameCount <= adviceFrame || dialogue.IsOpen) return;
        adviceAt = -1f; // once: the advice was shown and closed
        if (questManager == null || rematchQuest == null) return;
        questManager.ReportProgress(rematchQuest.objectiveKey, 1); // the single objective of the rematch quest
        questManager.TurnIn(rematchQuest); // one-shot; opens the tournament (TournamentManager)
    }

    string[] CompletedLines()
    {
        TournamentManager tm = TournamentManager.Instance;
        if (tm == null || tm.OpponentCount == 0) return wonLines;
        int beaten = 0;
        for (int i = 0; i < tm.OpponentCount; i++) if (tm.IsDefeated(i)) beaten++;
        if (beaten == 0) return tournamentOpenLines;
        if (beaten < tm.OpponentCount) return Format(tournamentPartLines, beaten, tm.OpponentCount);
        return Format(tournamentDoneLines, tm.Medals, tm.OpponentCount);
    }

    static string[] Concat(string[] a, string[] b)
    {
        string[] r = new string[a.Length + b.Length];
        a.CopyTo(r, 0);
        b.CopyTo(r, a.Length);
        return r;
    }

    static string[] Format(string[] lines, int done, int needed)
    {
        string[] result = new string[lines.Length];
        for (int i = 0; i < lines.Length; i++) result[i] = string.Format(lines[i], done, needed);
        return result;
    }

    static bool HasParameter(Animator anim, string parameterName, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.name == parameterName && p.type == type) return true;
        return false;
    }
}
