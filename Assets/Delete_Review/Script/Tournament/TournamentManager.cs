using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// M9 - NPC tournament. Reuses BattleManager (its public enemy references are pointed at the opponent's rooster for
/// the duration of one fight and restored afterwards), QuestManager (quest "giai_dau_lang"), CurrencyWallet, DialogueUI
/// and MessageBannerUI. No combat script is edited.
///
/// Rules:
///   - The tournament opens when the rematch quest (chuan_bi_tai_dau) is Completed (assumption: GAME_DESIGN calls the
///     rematch the final boss, but the implemented rematch comes first; see PROJECT_STATUS).
///   - Opponents are fought in array order; opponent i is unlocked only after opponent i-1 was beaten.
///   - A win pays the money + medals ONCE per opponent (the 'defeated' flag flips before anything is paid).
///     A loss pays nothing, the chicken is revived and the same opponent can be challenged again.
///   - Beating every opponent turns the tournament quest in once.
/// Medals are only counted here (Medals). Exchanging them is the Reward NPC (M10). No save/load: resets with Play.
/// </summary>
public class TournamentManager : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public OpponentDefinition definition;
        public HealthSystem rooster;
        public RoosterCombatAI roosterCombat;
    }

    [Header("Scene references")]
    [SerializeField] QuestManager questManager;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] MessageBannerUI banner;
    [SerializeField] CurrencyWallet wallet;
    [SerializeField] QuestDefinition rematchQuest;     // chuan_bi_tai_dau - opens the tournament
    [SerializeField] QuestDefinition tournamentQuest;  // giai_dau_lang - objective key counts won matches

    [Header("Opponents (fight order)")]
    [SerializeField] Entry[] opponents;

    [Header("Fight")]
    [SerializeField, Min(1f)] float arenaStartDistance = 4f;
    [SerializeField, Min(0f)] float getUpDelay = 3f;
    [SerializeField, Min(1f)] float getUpTimeout = 8f;
    [SerializeField] string idleStateName = "ChickenIdle";

    [Header("Arrival gate (new): combat does not start until Player AND chicken are both inside this radius of the opponent")]
    [SerializeField, Min(1f)] float arenaRadius = 6f;
    [SerializeField, Min(1f)] float guidanceReminderSeconds = 6f;
    [SerializeField] string guidanceMessage = "Hãy đến chỗ đối thủ để bắt đầu trận đấu!";

    [Header("Messages")]
    [SerializeField] string startMessage = "Trận đấu giải làng bắt đầu!";
    [SerializeField] string lostMessage = "Thua rồi! Luyện thêm rồi thách đấu lại.";
    [SerializeField] string winMessage = "Thắng! Nhận {0} huy chương và {1} tiền";
    [SerializeField] string needChickenMessage = "Gà của bạn chưa sẵn sàng để thi đấu";

    public static TournamentManager Instance { get; private set; }

    public event Action Changed;

    public int Medals { get; private set; }
    /// <summary>M10: medals already exchanged at the Reward NPC. Medals stays the total EARNED (so a later final-boss gate can read it).</summary>
    public int MedalsSpent { get; private set; }
    /// <summary>M10: medals the player can still exchange (Medals - MedalsSpent).</summary>
    public int AvailableMedals { get { return Medals - MedalsSpent; } }
    public int OpponentCount { get { return opponents != null ? opponents.Length : 0; } }
    public bool IsBusy { get { return fightActive || recovering; } }

    bool[] defeated;
    bool[] lost;
    int activeIndex = -1;
    bool fightActive, recovering;
    Transform player;
    bool pendingArrival;
    int pendingIndex = -1;
    float nextGuidanceAt;
    HealthSystem savedEnemyHealth;
    RoosterCombatAI savedEnemyCombat;

    void Awake()
    {
        Instance = this;
        defeated = new bool[OpponentCount];
        lost = new bool[OpponentCount];
        Medals = 0;
        MedalsSpent = 0;

        // Defensive: every opponent's rooster defaults to autoEngage=true in the scene (it is only the
        // currently-default BattleManager.enemyCombat target that anything else quiets at startup), so
        // without this, Chi Ba / Chu Tu would attack the player's chicken on sight, before any challenge.
        for (int i = 0; i < OpponentCount; i++)
            if (opponents[i].roosterCombat != null) opponents[i].roosterCombat.autoEngage = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
        if (status.Definition == rematchQuest) EnsureTournamentQuest();
    }

    void EnsureTournamentQuest()
    {
        if (questManager == null || tournamentQuest == null) return;
        if (questManager.GetState(tournamentQuest.questId) == QuestState.NotStarted) questManager.AcceptQuest(tournamentQuest);
    }

    /// <summary>The tournament is open once the rematch was won.</summary>
    public bool IsOpen
    {
        get { return questManager != null && rematchQuest != null && questManager.GetState(rematchQuest.questId) == QuestState.Completed; }
    }

    /// <summary>M10: spends medals for the Reward NPC. All-or-nothing; false (nothing changes) if amount is not positive or more than AvailableMedals.</summary>
    public bool TrySpendMedals(int amount)
    {
        if (amount <= 0 || amount > AvailableMedals) return false;
        MedalsSpent += amount;
        if (Changed != null) Changed();
        return true;
    }

    /// <summary>M10: gives back medals that were spent but whose reward could not be granted. Never refunds more than was spent.</summary>
    public void RefundMedals(int amount)
    {
        if (amount <= 0) return;
        MedalsSpent = Mathf.Max(0, MedalsSpent - amount);
        if (Changed != null) Changed();
    }

    public bool IsDefeated(int index) { return index >= 0 && index < OpponentCount && defeated[index]; }

    public bool HasLost(int index) { return index >= 0 && index < OpponentCount && lost[index]; }

    /// <summary>Opponent i can be challenged: the tournament is open, it is not beaten yet and the previous one was beaten.</summary>
    public bool IsUnlocked(int index)
    {
        if (index < 0 || index >= OpponentCount || !IsOpen) return false;
        if (defeated[index]) return false;
        return index == 0 || defeated[index - 1];
    }

    public OpponentDefinition GetDefinition(int index)
    {
        return index >= 0 && index < OpponentCount ? opponents[index].definition : null;
    }

    /// <summary>Called by the dialogue button of an opponent NPC. Does not start the fight immediately: if the
    /// Player or the chicken is not yet at the opponent's spot, this only shows arena guidance and the fight begins
    /// once both actually arrive (see UpdatePendingArrival). If both are already there, it starts at once, same as
    /// before.</summary>
    public void StartChallenge(int index)
    {
        if (!IsUnlocked(index) || IsBusy) return;

        BattleManager bm = BattleManager.Instance;
        if (bm == null || bm.IsBattleActive) return;

        Entry entry = opponents[index];
        if (entry.rooster == null || entry.roosterCombat == null || entry.definition == null)
        {
            Debug.LogError("[TournamentManager] Opponent " + index + " is not fully assigned.", this);
            return;
        }

        HealthSystem pet = bm.mainChickenHealth;
        MainChickenAcquire mc = MainChickenAcquire.Instance;
        if (pet == null || pet.IsDead || (mc != null && !mc.IsAcquired))
        {
            if (banner != null) banner.Show(needChickenMessage, 3f);
            return;
        }

        EnsureTournamentQuest();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (player != null && !ArrivedAtArena(pet.transform.position, entry.rooster.transform.position))
        {
            entry.roosterCombat.autoEngage = false; // defensive: don't let a stale flag attack the chicken while it travels
            pendingIndex = index;
            pendingArrival = true;
            nextGuidanceAt = 0f; // show the guidance banner immediately
            return;
        }

        BeginFight(index, pet, entry);
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
        if (pendingIndex < 0 || pendingIndex >= OpponentCount) { pendingArrival = false; pendingIndex = -1; return; }

        BattleManager bm = BattleManager.Instance;
        Entry entry = opponents[pendingIndex];
        HealthSystem pet = bm != null ? bm.mainChickenHealth : null;
        if (bm == null || bm.IsBattleActive || pet == null || pet.IsDead || entry.rooster == null || player == null)
        {
            pendingArrival = false; pendingIndex = -1; return; // something changed under us; just cancel the guidance, no fight starts
        }

        if (ArrivedAtArena(pet.transform.position, entry.rooster.transform.position))
        {
            int index = pendingIndex;
            pendingArrival = false;
            pendingIndex = -1;
            BeginFight(index, pet, entry);
            return;
        }

        if (Time.time >= nextGuidanceAt)
        {
            nextGuidanceAt = Time.time + guidanceReminderSeconds;
            if (banner != null && !string.IsNullOrEmpty(guidanceMessage)) banner.Show(guidanceMessage, 4f);
            if (BattleArena.Instance != null) BattleArena.Instance.ShowMarker();
        }
    }

    /// <summary>The actual fight start (unchanged from before this feature, just extracted into its own method so it
    /// can be called either right away or once arrival is confirmed).</summary>
    void BeginFight(int index, HealthSystem pet, Entry entry)
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;

        // opponent stats from its definition, fresh health
        OpponentDefinition def = entry.definition;
        entry.rooster.maxHealth = def.maxHealth;
        if (entry.rooster.IsDead) entry.rooster.Revive(1f);
        else entry.rooster.Heal(entry.rooster.maxHealth);
        entry.roosterCombat.damage = def.damage;
        entry.roosterCombat.attackCooldown = def.attackCooldown;
        if (pet.currentHealth < pet.maxHealth) pet.Heal(pet.maxHealth - pet.currentHealth);

        if (BattleArena.Instance != null) BattleArena.Instance.BringFoe(entry.rooster);
        PlaceChicken(pet, entry.rooster);

        // point the (stable) BattleManager at this opponent for one fight
        savedEnemyHealth = bm.enemyHealth;
        savedEnemyCombat = bm.enemyCombat;
        bm.enemyHealth = entry.rooster;
        bm.enemyCombat = entry.roosterCombat;
        bm.isFirstBattle = false;

        activeIndex = index;
        fightActive = true;
        bm.StartBattle();
        if (!bm.IsBattleActive)
        {
            fightActive = false;
            RestoreBattleManager(bm);
            activeIndex = -1;
            return;
        }

        if (banner != null && !string.IsNullOrEmpty(startMessage)) banner.Show(startMessage, 3f);
    }

    void PlaceChicken(HealthSystem pet, HealthSystem foe)
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

    void RestoreBattleManager(BattleManager bm)
    {
        if (savedEnemyHealth != null) bm.enemyHealth = savedEnemyHealth;
        if (savedEnemyCombat != null) bm.enemyCombat = savedEnemyCombat;
        savedEnemyHealth = null;
        savedEnemyCombat = null;
    }

    void Update()
    {
        if (pendingArrival) { UpdatePendingArrival(); return; }
        if (!fightActive) return;
        BattleManager bm = BattleManager.Instance;
        if (bm == null) { fightActive = false; return; }
        if (bm.CurrentBattleState != BattleManager.BattleState.Completed) return;

        fightActive = false;
        bool won = bm.enemyHealth != null && bm.enemyHealth.IsDead;
        int index = activeIndex;
        if (index >= 0 && index < OpponentCount && opponents[index].roosterCombat != null)
            opponents[index].roosterCombat.autoEngage = false; // defensive, see StartChallenge's pending-arrival branch
        RestoreBattleManager(bm);
        activeIndex = -1;

        if (won) OnWin(index);
        else OnLoss(index);
    }

    void OnWin(int index)
    {
        if (index < 0 || index >= OpponentCount || defeated[index]) return; // never pays twice
        defeated[index] = true; // flipped BEFORE paying

        OpponentDefinition def = opponents[index].definition;
        Medals += def.medals;
        if (def.rewardMoney > 0 && wallet != null) wallet.Add(def.rewardMoney);
        if (banner != null) banner.Show(string.Format(winMessage, def.medals, def.rewardMoney), 5f);

        if (questManager != null && tournamentQuest != null)
        {
            questManager.ReportProgress(tournamentQuest.objectiveKey, 1);
            if (questManager.GetState(tournamentQuest.questId) == QuestState.ReadyToTurnIn) questManager.TurnIn(tournamentQuest);
        }

        if (Changed != null) Changed();
    }

    void OnLoss(int index)
    {
        if (index >= 0 && index < OpponentCount) lost[index] = true;
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

        // every opponent rooster that is hurt but alive goes back to full health
        for (int i = 0; i < OpponentCount; i++)
        {
            HealthSystem r = opponents[i].rooster;
            if (r != null && !r.IsDead && r.currentHealth < r.maxHealth) r.Heal(r.maxHealth - r.currentHealth);
        }

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
