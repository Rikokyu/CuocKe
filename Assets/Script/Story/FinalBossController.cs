using System.Collections;
using UnityEngine;

/// <summary>
/// M12 - the final boss: a second, stronger fight against the childhood friend (same NPC and the same rooster,
/// GuguGaga, as the earlier rematch - no clone, no second BattleManager). Reuses QuestManager (quest
/// "phuc_thu_cuoi_cung", one-shot completion = the finalBossDefeated flag), DialogueUI, MessageBannerUI,
/// BattleManager (StartBattle / CurrentBattleState, its existing fighter references) and TournamentManager
/// (read-only: Medals gates the unlock). No combat script is edited.
///
/// Gate (per the owner's confirmed decision): unlocked once TournamentManager.Medals >= requiredMedals (2,
/// the total EARNED - spending medals at the Reward NPC only changes MedalsSpent, never Medals, so redeeming
/// rewards cannot revoke eligibility). Below that, this controller declines to handle the conversation
/// (TryInteract returns false) so RematchController's own tournament-progress lines keep explaining what is
/// still missing - no duplicate "locked" message is introduced.
///
/// The friend's E key is owned by ChildhoodFriendBattleTaunt -> RematchController.TryInteract(), which now
/// falls through to this controller once the rematch is Completed. This script must not be reached before that.
/// </summary>
public class FinalBossController : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] QuestManager questManager;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] MessageBannerUI banner;
    [SerializeField] QuestDefinition finalBossQuest; // phuc_thu_cuoi_cung
    [SerializeField] TournamentManager tournamentManager;

    [Header("Gate")]
    [SerializeField, Min(1)] int requiredMedals = 2;

    [Header("Stronger opponent (GAME_DESIGN 4.9: HP 300-360, dmg 30-38; restored after the fight either way)")]
    [SerializeField, Min(1f)] float bossMaxHealth = 330f;
    [SerializeField, Min(1f)] float bossDamage = 34f;

    [Header("Fight")]
    [SerializeField, Min(1f)] float arenaStartDistance = 4f;
    [SerializeField, Min(0f)] float victoryDialogueDelay = 2.5f;
    [SerializeField, Min(0f)] float getUpDelay = 3f;
    [SerializeField, Min(1f)] float getUpTimeout = 8f;
    [SerializeField] string idleStateName = "ChickenIdle";

    [Header("Arrival gate (new): combat does not start until Player AND chicken are both inside this radius of the opponent")]
    [SerializeField, Min(1f)] float arenaRadius = 6f;
    [SerializeField, Min(1f)] float guidanceReminderSeconds = 6f;
    [SerializeField] string guidanceMessage = "Hãy đến chỗ bạn thân để bắt đầu trận phục thù!";

    [Header("Dialogue")]
    [SerializeField, TextArea(2, 4)] string[] challengeLines =
    {
        "Mày hạ được cả chị Ba lẫn chú Tư rồi à? Không ngờ đấy.",
        "Nhưng đừng tưởng thế là xong. Lần này tao sẽ không nhẹ tay đâu.",
        "MÀY SỢ À? Đấu thật một trận cho ra lẽ đi!"
    };
    [SerializeField] string acceptLabel = "Phục thù!";
    [SerializeField, TextArea(2, 4)] string[] retryLines =
    {
        "Hả, chưa phục à? Lại đây, tao chờ sẵn rồi."
    };
    [SerializeField, TextArea(2, 4)] string[] fightingLines = { "Đang đấu rồi, nhìn đi đâu đấy?!" };
    [SerializeField, TextArea(2, 4)] string[] recoveringLines = { "Đợi gà mày đứng dậy đã rồi hẵng nói chuyện." };
    [SerializeField, TextArea(2, 4)] string[] needChickenLines = { "Gà của mày đâu? Dắt nó theo rồi hẵng đấu!" };
    [SerializeField, TextArea(2, 4)] string[] wonLines =
    {
        "...Tao thua thật rồi. Gà của mày giờ không còn gì để chê nữa.",
        "Thôi được, tao công nhận mày ngon. Dắt gà về nhà ông bà mà khoe đi, đáng lắm.",
    };
    [SerializeField, TextArea(2, 4)] string[] afterWonIdleLines =
    {
        "Haha, nghĩ lại vẫn phục mày với con gà đó đấy."
    };

    [Header("Messages")]
    [SerializeField] string startMessage = "Trận phục thù cuối cùng bắt đầu!";
    [SerializeField] string lostMessage = "Thua rồi! Luyện thêm rồi quay lại đấu tiếp.";
    [SerializeField] string winMessage = "Chiến thắng! Bạn đã đánh bại bạn thân cũ!";

    public static FinalBossController Instance { get; private set; }

    /// <summary>True once the final boss was beaten (this play session only - there is no save/load yet).</summary>
    public static bool FinalBossDefeated { get; private set; }

    /// <summary>M13: true once the victory is won AND the friend's concession dialogue (wonLines) was shown and closed. EndingSequence waits for this.</summary>
    public static bool VictoryStoryDone { get; private set; }

    bool fightActive;
    bool recovering;
    bool lostOnce;
    Transform player;
    bool pendingArrival;
    float nextGuidanceAt;
    float wonAt = -1f;
    bool wonDialogueShown;
    int wonDialogueFrame;
    float savedFoeMaxHealth = -1f;
    float savedFoeDamage = -1f;

    void Awake()
    {
        Instance = this;
        FinalBossDefeated = false;
        VictoryStoryDone = false;
    }

    void OnDestroy()
    {
        if (Instance == this) { Instance = null; FinalBossDefeated = false; VictoryStoryDone = false; }
    }

    /// <summary>Read-only view of the existing gate (earned medals >= requiredMedals). Used by RematchController so the
    /// friend is back exactly when the final boss can be started.</summary>
    public bool GateOpen { get { return IsUnlocked(); } }

    bool IsUnlocked()
    {
        return tournamentManager != null && tournamentManager.Medals >= requiredMedals;
    }

    /// <summary>
    /// Called by RematchController once the rematch is Completed. Returns false (handles nothing) while the
    /// medal gate is not met, so the caller's own tournament-progress lines keep explaining what is missing.
    /// </summary>
    public bool TryInteract()
    {
        if (dialogue == null || questManager == null || finalBossQuest == null) return false;
        if (!IsUnlocked()) return false;

        switch (questManager.GetState(finalBossQuest.questId))
        {
            case QuestState.NotStarted:
                questManager.AcceptQuest(finalBossQuest); // eligible: open the quest the moment he is first asked
                dialogue.Open("Bạn thân", challengeLines, acceptLabel, StartFinalBattle);
                return true;

            case QuestState.Active:
                if (fightActive || (BattleManager.Instance != null && BattleManager.Instance.IsBattleActive))
                    dialogue.Open("Bạn thân", fightingLines, null, null);
                else if (recovering)
                    dialogue.Open("Bạn thân", recoveringLines, null, null);
                else
                    dialogue.Open("Bạn thân", lostOnce ? retryLines : challengeLines, acceptLabel, StartFinalBattle);
                return true;

            default: // Completed
                dialogue.Open("Bạn thân", afterWonIdleLines, null, null);
                return true;
        }
    }

    /// <summary>The dialogue button. Public so a test can drive the same path. Does not start the fight
    /// immediately: if the Player or the chicken is not yet at the rooster, this only shows arena guidance and
    /// the fight begins once both actually arrive (see UpdatePendingArrival). If both are already there, it
    /// starts at once, same as before.</summary>
    public void StartFinalBattle()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null || questManager == null || finalBossQuest == null) return;
        if (fightActive || recovering || bm.IsBattleActive) return;
        if (!IsUnlocked() || questManager.GetState(finalBossQuest.questId) != QuestState.Active) return;

        MainChickenAcquire mc = MainChickenAcquire.Instance;
        HealthSystem pet = bm.mainChickenHealth;
        HealthSystem foe = bm.enemyHealth;       // the same GuguGaga used by the earlier rematch
        RoosterCombatAI foeCombat = bm.enemyCombat;
        if (pet == null || foe == null || foeCombat == null)
        {
            Debug.LogError("[FinalBossController] BattleManager fighters are not assigned.", this);
            return;
        }
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
            foeCombat.autoEngage = false; // defensive: see TournamentManager's/RematchController's same fix
            pendingArrival = true;
            nextGuidanceAt = 0f;
            return;
        }

        BeginFight(pet, foe, foeCombat);
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
        RoosterCombatAI foeCombat = bm != null ? bm.enemyCombat : null;
        if (bm == null || bm.IsBattleActive || pet == null || pet.IsDead || foe == null || foeCombat == null || player == null)
        {
            pendingArrival = false; return; // something changed under us; just cancel the guidance, no fight starts
        }

        if (ArrivedAtArena(pet.transform.position, foe.transform.position))
        {
            pendingArrival = false;
            BeginFight(pet, foe, foeCombat);
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
    void BeginFight(HealthSystem pet, HealthSystem foe, RoosterCombatAI foeCombat)
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;

        // fresh fighters, then boost the foe to the stronger final-boss configuration (restored after the fight)
        if (pet.currentHealth < pet.maxHealth) pet.Heal(pet.maxHealth - pet.currentHealth);
        if (foe.IsDead && foe.Revive(1f)) foeCombat.ResumeAfterRevive(); // the rooster of the earlier rematch lay dead: stand it up

        savedFoeMaxHealth = foe.maxHealth;
        savedFoeDamage = foeCombat.damage;
        foe.maxHealth = bossMaxHealth;
        if (foe.currentHealth < bossMaxHealth) foe.Heal(bossMaxHealth - foe.currentHealth);
        foeCombat.damage = bossDamage;

        if (BattleArena.Instance != null) BattleArena.Instance.BringFoe(foe);
        PlaceChickenAtArena(pet, foe);

        bm.isFirstBattle = false;
        fightActive = true;
        bm.StartBattle();
        if (!bm.IsBattleActive)
        {
            fightActive = false;
            RestoreFoeStats(foe, foeCombat);
            return;
        }

        if (banner != null && !string.IsNullOrEmpty(startMessage)) banner.Show(startMessage, 3f);
    }

    void RestoreFoeStats(HealthSystem foe, RoosterCombatAI foeCombat)
    {
        if (savedFoeMaxHealth < 0f) return; // nothing was boosted (e.g. the fight never actually started)
        if (foe != null) foe.maxHealth = savedFoeMaxHealth;
        if (foeCombat != null) foeCombat.damage = savedFoeDamage;
        savedFoeMaxHealth = -1f;
        savedFoeDamage = -1f;
    }

    void PlaceChickenAtArena(HealthSystem pet, HealthSystem foe)
    {
        Vector3 foePos = foe.transform.position;
        Vector3 away = pet.transform.position - foePos;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = Vector3.back;
        Vector3 spot = foePos + away.normalized * arenaStartDistance;

        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(spot, out hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) spot = hit.position;

        UnityEngine.AI.NavMeshAgent agent = pet.GetComponent<UnityEngine.AI.NavMeshAgent>();
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

        if (wonDialogueShown && !VictoryStoryDone && dialogue != null && Time.frameCount > wonDialogueFrame && !dialogue.IsOpen) VictoryStoryDone = true;
    }

    void PollFight()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) { fightActive = false; return; }
        if (bm.CurrentBattleState != BattleManager.BattleState.Completed) return;

        fightActive = false;
        bool won = bm.enemyHealth != null && bm.enemyHealth.IsDead;
        if (bm.enemyCombat != null) bm.enemyCombat.autoEngage = false; // defensive, see StartFinalBattle's pending-arrival branch
        RestoreFoeStats(bm.enemyHealth, bm.enemyCombat); // the opponent goes back to its normal stats either way

        if (won) OnVictory();
        else OnDefeat();
    }

    void OnVictory()
    {
        // one-shot: TurnIn only succeeds once (quest is already Active, never re-entered after Completed since
        // TryInteract's switch routes Completed to the idle branch, which never calls StartFinalBattle again)
        if (questManager.GetState(finalBossQuest.questId) != QuestState.Active) return;
        FinalBossDefeated = true;
        questManager.ReportProgress(finalBossQuest.objectiveKey, 1);
        questManager.TurnIn(finalBossQuest);
        if (banner != null && !string.IsNullOrEmpty(winMessage)) banner.Show(winMessage, 6f);
        wonAt = Time.time;
    }

    void OnDefeat()
    {
        // explicitly no reward, no quest-state change, no completion flag - just recovery, matching the rematch's own loss handling
        lostOnce = true;
        if (!recovering) StartCoroutine(RecoverAfterLoss());
    }

    IEnumerator RecoverAfterLoss()
    {
        recovering = true;
        if (banner != null && !string.IsNullOrEmpty(lostMessage)) banner.Show(lostMessage, 5f);

        BattleManager bm = BattleManager.Instance;
        HealthSystem pet = bm != null ? bm.mainChickenHealth : null;
        PetCombatAI combat = bm != null ? bm.mainChickenCombat : null;

        yield return new WaitForSeconds(getUpDelay);

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

    static bool HasParameter(Animator anim, string parameterName, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.name == parameterName && p.type == type) return true;
        return false;
    }
}
