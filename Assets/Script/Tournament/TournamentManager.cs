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
    [Tooltip("tai_dau_ban_than: the friend's rematch. Its completion (the scripted loss + his advice) opens the tournament right away; the training quest above then only prepares the chicken.")]
    [SerializeField] QuestDefinition friendRematchQuest;
    [SerializeField] QuestDefinition tournamentQuest;  // giai_dau_lang - objective key counts won matches

    [Header("Training gate (OpponentDefinition.minTrainingHours)")]
    [Tooltip("Optional. If empty, the ChickenStats in the scene is found at runtime.")]
    [SerializeField] ChickenStats chickenStats;
    [SerializeField] string notTrainedBanner = "Gà chưa đủ giờ tập! Cần tối thiểu {0} giờ tập (hiện có {1}).";

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
    [SerializeField] string winMessage = "Thắng! Nhận {0} huy chương và {1} VNĐ";
    [SerializeField] string needChickenMessage = "Gà của bạn chưa sẵn sàng để thi đấu";

    public static TournamentManager Instance { get; private set; }

    public event Action Changed;

    public int Medals { get; private set; }
    /// <summary>M10: medals already exchanged at the Reward NPC. Medals stays the total EARNED (so a later final-boss gate can read it).</summary>
    public int MedalsSpent { get; private set; }
    /// <summary>M10: medals the player can still exchange (Medals - MedalsSpent).</summary>
    public int AvailableMedals { get { return Medals - MedalsSpent; } }
    public int OpponentCount { get { return opponents != null ? opponents.Length : 0; } }
    public bool IsBusy { get { return fightActive || recovering || winSequence; } }

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

    [Header("Opponent NPC stands near the arena during the fight (Chi Ba / Chu Tu), then goes back to its place")]
    [SerializeField] float npcBehindFoeDistance = 2.5f;
    [SerializeField] float npcSideOffset = 1.8f;
    [SerializeField, Min(0f)] float winDialogueDelay = 1.5f;
    [SerializeField, Min(0f)] float npcReturnDelay = 1f;
    [SerializeField] string winContinueLabel = "Nhận huy chương";

    class NpcHome
    {
        public TournamentOpponentNPC npc;
        public Vector3 pos;
        public Quaternion rot;
        public float yOffset; // npc.y - rooster.y at home, so the NPC keeps the same ground relation near the arena
    }

    NpcHome[] npcHomes;
    bool winSequence;


    void Awake()
    {
        Instance = this;
        defeated = new bool[OpponentCount];
        lost = new bool[OpponentCount];
        CaptureNpcHomes();

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
        if (status.Definition == rematchQuest || (friendRematchQuest != null && status.Definition == friendRematchQuest)) EnsureTournamentQuest();
    }

    void EnsureTournamentQuest()
    {
        if (questManager == null || tournamentQuest == null) return;
        if (questManager.GetState(tournamentQuest.questId) == QuestState.NotStarted) questManager.AcceptQuest(tournamentQuest);
    }

    /// <summary>The tournament is open once the rematch was won.</summary>
    public bool IsOpen
    {
        get
        {
            if (questManager == null) return false;
            if (rematchQuest != null && questManager.GetState(rematchQuest.questId) == QuestState.Completed) return true;
            return friendRematchQuest != null && questManager.GetState(friendRematchQuest.questId) == QuestState.Completed;
        }
    }

    ChickenStats Stats
    {
        get
        {
            if (chickenStats == null) chickenStats = FindFirstObjectByType<ChickenStats>();
            return chickenStats;
        }
    }

    /// <summary>The chicken's total training hours (0 if there is no ChickenStats).</summary>
    public int TrainingHours { get { return Stats != null ? Stats.TrainingHours : 0; } }

    /// <summary>Training hours opponent i demands before a challenge (OpponentDefinition.minTrainingHours).</summary>
    public int RequiredTrainingHours(int index)
    {
        OpponentDefinition def = GetDefinition(index);
        return def != null ? def.minTrainingHours : 0;
    }

    public bool HasEnoughTraining(int index) { return TrainingHours >= RequiredTrainingHours(index); }

    /// <summary>The opponent's 'train more first' lines with {0} = hours needed and {1} = hours trained filled in.</summary>
    public string[] NeedTrainingLines(int index)
    {
        OpponentDefinition def = GetDefinition(index);
        if (def == null || def.notTrainedLines == null) return new string[0];
        string[] result = new string[def.notTrainedLines.Length];
        for (int i = 0; i < result.Length; i++) result[i] = string.Format(def.notTrainedLines[i], def.minTrainingHours, TrainingHours);
        return result;
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
        if (!HasEnoughTraining(index))
        {
            if (banner != null) banner.Show(string.Format(notTrainedBanner, RequiredTrainingHours(index), TrainingHours), 4f);
            return;
        }

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

        MoveNpcToArena(index, entry);

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
        defeated[index] = true; // flipped BEFORE anything else, so the sequence below can only ever run once
        StartCoroutine(WinSequence(index));
    }

    /// <summary>Win flow: the beaten NPC says its defeat lines FIRST, only then the medals/money/quest progress are paid,
    /// and then the NPC goes back to its usual place.</summary>
    IEnumerator WinSequence(int index)
    {
        winSequence = true;
        OpponentDefinition def = opponents[index].definition;

        yield return new WaitForSeconds(winDialogueDelay);

        float waited = 0f;
        while (dialogue != null && dialogue.IsOpen && waited < 10f) { waited += Time.deltaTime; yield return null; }

        bool paid = false;
        System.Action pay = () => { if (paid) return; paid = true; PayWin(index); };

        if (dialogue != null && def != null && def.defeatedLines != null && def.defeatedLines.Length > 0)
        {
            TournamentOpponentNPC npc = GetNpc(index);
            if (npc != null) npc.FacePlayer();
            dialogue.Open(def.displayName, def.defeatedLines, winContinueLabel, pay);
            yield return null;
            while (dialogue.IsOpen) yield return null;
        }
        pay(); // also covers a dialogue closed with Esc or one that could not open

        yield return new WaitForSeconds(npcReturnDelay);
        ReturnNpcHome(index);
        winSequence = false;
    }

    void PayWin(int index)
    {
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
        StartCoroutine(ReturnNpcAfterRecovery(index));
    }

    IEnumerator ReturnNpcAfterRecovery(int index)
    {
        while (recovering) yield return null;
        yield return new WaitForSeconds(npcReturnDelay);
        ReturnNpcHome(index);
    }

    void CaptureNpcHomes()
    {
        npcHomes = new NpcHome[OpponentCount];
        TournamentOpponentNPC[] all = FindObjectsByType<TournamentOpponentNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            int idx = all[i].OpponentIndex;
            if (idx < 0 || idx >= OpponentCount) continue;
            Transform t = all[i].transform;
            Transform r = opponents[idx].rooster != null ? opponents[idx].rooster.transform : null;
            npcHomes[idx] = new NpcHome { npc = all[i], pos = t.position, rot = t.rotation, yOffset = r != null ? t.position.y - r.position.y : 0f };
        }
    }

    TournamentOpponentNPC GetNpc(int index)
    {
        return npcHomes != null && index >= 0 && index < npcHomes.Length && npcHomes[index] != null ? npcHomes[index].npc : null;
    }

    /// <summary>Puts the opponent's villager next to the arena (behind its rooster, a bit to the side), facing the fight.</summary>
    void MoveNpcToArena(int index, Entry entry)
    {
        if (npcHomes == null || index < 0 || index >= npcHomes.Length || npcHomes[index] == null || npcHomes[index].npc == null) return;
        NpcHome home = npcHomes[index];

        Vector3 foePos = entry.rooster.transform.position;
        Vector3 center = BattleArena.Instance != null ? BattleArena.Instance.Center : foePos + Vector3.left;
        Vector3 toCenter = center - foePos;
        toCenter.y = 0f;
        if (toCenter.sqrMagnitude < 0.01f) toCenter = Vector3.left;
        toCenter.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, toCenter);

        Vector3 spot = foePos - toCenter * npcBehindFoeDistance + side * npcSideOffset;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(spot, out hit, 3f, NavMesh.AllAreas)) spot = hit.position;
        else spot.y = foePos.y;
        spot.y += home.yOffset;

        Transform t = home.npc.transform;
        t.position = spot;
        Vector3 look = center - spot;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(look);
    }

    void ReturnNpcHome(int index)
    {
        if (npcHomes == null || index < 0 || index >= npcHomes.Length || npcHomes[index] == null || npcHomes[index].npc == null) return;
        Transform t = npcHomes[index].npc.transform;
        t.position = npcHomes[index].pos;
        t.rotation = npcHomes[index].rot;
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
