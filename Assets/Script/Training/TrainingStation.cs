using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// One training device (push-up mat, treadmill or punching bag). The player presses E next to it; the main chicken walks to
/// the device's ChickenSpot and really performs the exercise with the EXISTING animator parameters
/// (IsPushing = push-up, isRunning = run in place, Attack + AttackIndex = kicks at the bag), then the matching stat rises.
///
/// Rules (all checked in TryStart, in this order; every refusal charges nothing and changes nothing):
///   - only one session can run at a time (static flag) and a short cooldown follows each session = no E spam
///   - not during a dialogue, the shop, the old inventory screen or a battle
///   - the chicken must be awake and alive (MainChickenAcquire) and the stat not maxed
///   - if requiredItem is set the player must own it in InventorySystem (it is checked, not consumed)
///   - the CurrencyWallet must cover the cost; money is spent ONCE with TrySpend at the start
/// The stat rises once, when the session finishes. If the session is cut short (chicken dies, battle starts, device
/// disabled) the money is refunded and no stat changes.
/// While a session runs PetCombatAI is switched off and Animator root motion is off (so it exercises in place);
/// at the end PetCombatAI.ResumeAfterRecovery() hands control back.
/// Keep stations about 4 m apart (and away from NPCs) so one E press cannot start two things.
/// </summary>
public class TrainingStation : MonoBehaviour
{
    public enum Activity { PushUp, Treadmill, PunchingBag }

    [Header("Activity")]
    [SerializeField] Activity activity = Activity.PushUp;
    [SerializeField] string displayName = "Chống đẩy";
    [SerializeField, Min(1)] int cost = 40;
    [SerializeField, Min(1)] int statGain = 2;
    [SerializeField, Min(0.5f)] float sessionSeconds = 4f;
    [Tooltip("Optional. If set, the player must own this item (checked, not consumed). Leave empty for no requirement.")]
    [SerializeField] ItemDefinition requiredItem;
    [Tooltip("ON: requiredItem must be PLACED at this station first (ItemUseController consumes it and calls Place()); 'placedVisual' stays hidden until then. OFF: the item only has to be in the bag.")]
    [SerializeField] bool requiresPlacement;
    [Tooltip("The prop that appears when the item is placed (e.g. the training mat).")]
    [SerializeField] GameObject placedVisual;

    [Header("Where the chicken trains")]
    [Tooltip("The chicken is placed here and takes this transform's rotation (forward = facing direction).")]
    [SerializeField] Transform chickenSpot;
    [Tooltip("Punching bag only: the part that swings when hit (a pivot at the top of the bag).")]
    [SerializeField] Transform swingPivot;
    [SerializeField, Min(0.3f)] float hitInterval = 1.1f;

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] float interactionDistance = 1.8f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [Tooltip("True = the trainer NPC (TrainerNPC) pays for and drives this station: no prompt, no E, no charge here.")]
    [SerializeField] bool paidByTrainer = true;
    [Tooltip("Seconds after a session ends (or after a refusal message) before E does anything again.")]
    [SerializeField, Min(0f)] float cooldownSeconds = 0.8f;

    [Header("Scene references")]
    [SerializeField] ChickenStats stats;
    [SerializeField] CurrencyWallet wallet;
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] MessageBannerUI banner;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] ShopUI shop;
    [Tooltip("Optional. If empty, the GameObject tagged 'Player' is used.")]
    [SerializeField] Transform player;

    static bool busy; // one training session at a time, across all stations

    float nextAllowedTime;
    bool promptShown;
    string shownPromptText;
    bool sessionActive;
    // shared on purpose: there is ONE chicken and ONE session at a time (the trainer walks it through three stations)
    static bool prevRootMotion;
    int paidCost;
    float swingAmplitude;
    float swingClock;
    static float prevStopping = -1f;
    static float prevBaseOffset = float.NaN;
    Coroutine routine;

    PetCombatAI combat;
    HealthSystem health;
    Animator anim;
    NavMeshAgent agent;

    public bool IsSessionActive => sessionActive;
    public ChickenStats.StatType TrainedStat
    {
        get
        {
            switch (activity)
            {
                case Activity.Treadmill: return ChickenStats.StatType.Speed;
                case Activity.PunchingBag: return ChickenStats.StatType.Attack;
                default: return ChickenStats.StatType.Strength;
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { busy = false; prevStopping = -1f; prevBaseOffset = float.NaN; prevRootMotion = false; }

    void Start()
    {
        if (requiresPlacement && placedVisual != null) placedVisual.SetActive(IsPlaced); // the mat appears only after the player placed it
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (stats != null)
        {
            combat = stats.GetComponent<PetCombatAI>();
            health = stats.GetComponent<HealthSystem>();
            anim = stats.GetComponent<Animator>();
            agent = stats.GetComponent<NavMeshAgent>();
        }

        if (player == null || stats == null || wallet == null || chickenSpot == null)
            Debug.LogError("[TrainingStation] Player / ChickenStats / CurrencyWallet / ChickenSpot reference missing on " + name, this);
    }

    void Update()
    {
        UpdateSwing();
        if (paidByTrainer) { SetPrompt(false); return; } // passive prop: the trainer NPC runs it
        if (player == null || stats == null) return;

        bool canInteract = IsPlayerInRange() && !busy && !IsBlockedByUI();
        SetPrompt(canInteract);

        if (canInteract && Input.GetKeyDown(interactKey)) TryStart();
    }

    /// <summary>Tries to start a session. Returns true only if money was spent and the session began.</summary>
    public bool TryStart()
    {
        if (busy || sessionActive) return false;
        if (Time.unscaledTime < nextAllowedTime) return false;
        if (stats == null || wallet == null || chickenSpot == null) return false;
        if (IsBlockedByUI()) return false;

        TrainingRefusal(out string why);
        if (why != null) { Refuse(why); return false; }

        if (!wallet.TrySpend(cost)) { Refuse(string.Format("Không đủ tiền: cần {0}, đang có {1}", cost, wallet.Balance)); return false; }

        paidCost = cost;
        busy = true;
        sessionActive = true;
        SetPrompt(false);
        routine = StartCoroutine(RunSession());
        return true;
    }

    /// <summary>The reason a session cannot start (null if it can). Money is checked last.</summary>
    void TrainingRefusal(out string why)
    {
        why = null;
        if (!ChickenTransformationTrigger.TrainingUnlocked) { why = ChickenTransformationTrigger.TrainingLockedMessage; return; } // story gate
        if (BattleManager.Instance != null && BattleManager.Instance.IsBattleActive) { why = "Đang trong trận đấu, chưa thể tập"; return; }

        MainChickenAcquire acquire = MainChickenAcquire.Instance;
        if (acquire != null && !acquire.IsAcquired) { why = "Gà chưa tỉnh dậy, chưa thể tập"; return; }
        if (health != null && health.IsDead) { why = "Gà đang gục, chưa thể tập"; return; }

        if (stats.IsMaxed(TrainedStat)) { why = "Chỉ số này đã đạt tối đa"; return; }

        string requirement = RequirementProblem();
        if (requirement != null) { why = requirement; return; }

        if (!wallet.CanAfford(cost)) why = string.Format("Không đủ tiền: cần {0}, đang có {1}", cost, wallet.Balance);
    }

    void Refuse(string message)
    {
        nextAllowedTime = Time.unscaledTime + cooldownSeconds; // a refusal never repeats its message every frame
        if (banner != null) banner.Show(message);
    }

    IEnumerator RunSession()
    {
        int before = stats.Get(TrainedStat);
        int hpBefore = stats.Get(ChickenStats.StatType.MaxHP);

        BeginTakeover();
        yield return WalkAndExercise(sessionSeconds);
        bool completed = exerciseCompleted;
        string message;
        if (completed && stats.Train(TrainedStat, statGain))
        {
            int after = stats.Get(TrainedStat);
            message = string.Format("{0} xong! {1} {2} → {3}", displayName, StatLabel(TrainedStat), before, after);
            if (TrainedStat == ChickenStats.StatType.Strength)
                message += string.Format("  (Máu tối đa {0} → {1})", hpBefore, stats.Get(ChickenStats.StatType.MaxHP));
            message += string.Format("  (-{0} tiền)", paidCost);
            paidCost = 0; // the money stays spent
            Debug.Log("[Training] " + message);
        }
        else
        {
            if (paidCost > 0) wallet.Add(paidCost); // cut short: give the money back
            message = displayName + " bị gián đoạn, đã hoàn tiền";
            paidCost = 0;
        }

        FinishSession(message);
    }

    /// <summary>Takes over the chicken (combat off, in-place animation). Shared by the old per-station session and the trainer.</summary>
    void BeginTakeover()
    {
        if (combat != null) combat.enabled = false;
        if (anim != null)
        {
            prevRootMotion = anim.applyRootMotion;
            anim.applyRootMotion = false; // exercise in place (also avoids the feet sinking noted in section 15)
            anim.SetBool("IsPushing", false);
        }

    }

    bool exerciseCompleted;

    /// <summary>Walks to the spot and does the exercise for 'seconds'. Sets exerciseCompleted (false if aborted). No money, no stats.</summary>
    IEnumerator WalkAndExercise(float seconds)
    {
        // walk to the spot (agent, with the normal run animation), then snap exactly onto it
        if (anim != null) anim.SetBool("IsPushing", false); // a push-up of the previous exercise must not stay on while walking / in the next exercise
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (prevStopping < 0f) prevStopping = agent.stoppingDistance; // save once per block (several stations in a row)
            if (!float.IsNaN(prevBaseOffset)) agent.baseOffset = prevBaseOffset; // undo the lift of the previous station
            agent.stoppingDistance = 0.05f; // the scene value (10) would stop the chicken far from the spot
            agent.isStopped = false;
            agent.SetDestination(chickenSpot.position);
            Vector3 toSpot = chickenSpot.position - agent.transform.position;
            toSpot.y = 0f;
            float walkLimit = Mathf.Clamp(toSpot.magnitude / Mathf.Max(0.5f, agent.speed) + 3f, 5f, 20f);
            while (walkLimit > 0f && !AbortNeeded())
            {
                Vector3 offset = chickenSpot.position - agent.transform.position;
                offset.y = 0f;
                if (!agent.pathPending && offset.magnitude <= 0.25f) break;
                // arrived at the closest reachable NavMesh point (e.g. on a treadmill belt the exact spot may be a bit off): stop waiting
                if (!agent.pathPending && agent.remainingDistance <= 0.3f) break;
                if (!agent.pathPending && offset.magnitude <= 1.0f && agent.velocity.sqrMagnitude < 0.01f) break;
                if (anim != null) anim.SetBool("isRunning", agent.velocity.magnitude > 0.1f);
                walkLimit -= Time.deltaTime;
                yield return null;
            }
            agent.isStopped = true;
            agent.Warp(chickenSpot.position);
            // a spot above the ground (e.g. on a treadmill belt): raise the model with the agent's base offset, the agent stays on the NavMesh
            float lift = chickenSpot.position.y - agent.nextPosition.y;
            if (lift > 0.02f) { if (float.IsNaN(prevBaseOffset)) prevBaseOffset = agent.baseOffset; agent.baseOffset = prevBaseOffset + lift; }
        }
        if (anim != null) anim.SetBool("isRunning", false);
        stats.transform.rotation = Quaternion.Euler(0f, chickenSpot.eulerAngles.y, 0f);
        yield return null; // let the animator settle in idle before the exercise starts

        // the exercise
        float remaining = seconds;
        float hitTimer = 0f;
        float pendingSwing = -1f;
        while (remaining > 0f && !AbortNeeded())
        {
            switch (activity)
            {
                case Activity.PushUp:
                    if (anim != null) anim.SetBool("IsPushing", true);
                    break;
                case Activity.Treadmill:
                    if (anim != null) anim.SetBool("isRunning", true);
                    break;
                case Activity.PunchingBag:
                    hitTimer -= Time.deltaTime;
                    if (hitTimer <= 0f && anim != null)
                    {
                        int[] indices = combat != null && combat.attackIndices != null && combat.attackIndices.Length > 0 ? combat.attackIndices : new[] { 1 };
                        anim.SetInteger("AttackIndex", indices[Random.Range(0, indices.Length)]);
                        anim.SetTrigger("Attack");
                        hitTimer = hitInterval;
                        pendingSwing = 0.35f; // the strike lands a moment after the kick starts
                    }
                    if (pendingSwing >= 0f)
                    {
                        pendingSwing -= Time.deltaTime;
                        if (pendingSwing < 0f) { swingAmplitude = 28f; swingClock = 0f; BagImpact(); }
                    }
                    break;
            }
            remaining -= Time.deltaTime;
            yield return null;
        }

        if (anim != null)
        {
            anim.SetBool("IsPushing", false); // the exercise is over: back to normal idle / walk
            anim.SetBool("isRunning", false);
        }
        exerciseCompleted = remaining <= 0f && !AbortNeeded();
    }

    // ---------------------------------------------------------------- driven by the trainer NPC (TrainerNPC)

    /// <summary>True when a trainer NPC pays for and drives this station (the station itself shows no prompt and charges nothing).</summary>
    public bool PaidByTrainer => paidByTrainer;
    public ItemDefinition RequiredItem => requiredItem;
    public bool RequiresPlacement => requiresPlacement;
    /// <summary>True once the required item was placed here (only meaningful when requiresPlacement is on).</summary>
    public bool IsPlaced { get; private set; }
    public bool CanPlace => requiresPlacement && !IsPlaced;

    /// <summary>Marks the item as placed and shows the prop. False if there is nothing to place or it is already placed (once only).</summary>
    public bool Place()
    {
        if (!CanPlace) return false;
        IsPlaced = true;
        if (placedVisual != null) placedVisual.SetActive(true);
        return true;
    }

    /// <summary>Why the required item is missing (null = nothing is missing). Used by this station and by TrainerNPC.</summary>
    public string RequirementProblem()
    {
        if (requiredItem == null) return null;
        if (requiresPlacement)
            return IsPlaced ? null : string.Format("Chưa có {0} ở khu tập. Mua ở chợ, chọn trên thanh nhanh rồi nhấn F cạnh chỗ {1}", requiredItem.displayName.ToLower(), displayName.ToLower());
        InventorySystem inv = InventorySystem.Instance;
        return inv != null && inv.Has(requiredItem) ? null : string.Format("Cần có {0} (mua ở chợ) để tập {1}", requiredItem.displayName, displayName.ToLower());
    }
    public string DisplayName => displayName;
    public bool LastExerciseCompleted => exerciseCompleted;
    public static bool IsBusy => busy;

    /// <summary>Starts a trainer-driven block: takes over the chicken once for several exercises. False if another session runs.</summary>
    public bool BeginDriven()
    {
        if (busy || sessionActive) return false;
        busy = true;
        sessionActive = true;
        SetPrompt(false);
        BeginTakeover();
        return true;
    }

    /// <summary>One exercise of a trainer-driven block (walk to the spot, exercise for 'seconds'). Check LastExerciseCompleted afterwards.</summary>
    public IEnumerator PerformDriven(float seconds)
    {
        return WalkAndExercise(seconds);
    }

    /// <summary>Ends the trainer-driven block: gives the chicken back to PetCombatAI.</summary>
    public void EndDriven()
    {
        FinishSession(null);
    }

    bool AbortNeeded()
    {
        if (health != null && health.IsDead) return true;
        if (BattleManager.Instance != null && BattleManager.Instance.IsBattleActive) return true;
        return false;
    }

    void FinishSession(string message)
    {
        if (!sessionActive) return;
        sessionActive = false;
        routine = null;

        if (anim != null)
        {
            anim.SetBool("IsPushing", false);
            anim.SetBool("isRunning", false);
            anim.ResetTrigger("Attack");
            anim.applyRootMotion = prevRootMotion;
        }

        if (agent != null && prevStopping >= 0f) { agent.stoppingDistance = prevStopping; prevStopping = -1f; }
        if (agent != null && !float.IsNaN(prevBaseOffset)) { agent.baseOffset = prevBaseOffset; prevBaseOffset = float.NaN; }

        bool dead = health != null && health.IsDead;
        if (combat != null && !dead)
        {
            combat.enabled = true;
            combat.ResumeAfterRecovery(); // back to Follow with a clean state (also clears push-up)
        }
        else if (agent != null && agent.enabled && agent.isOnNavMesh && !dead)
        {
            agent.isStopped = false;
        }

        busy = false;
        nextAllowedTime = Time.unscaledTime + cooldownSeconds;
        if (banner != null && !string.IsNullOrEmpty(message)) banner.Show(message);
    }

    void OnDisable()
    {
        SetPrompt(false);
        if (sessionActive)
        {
            if (routine != null) StopCoroutine(routine);
            if (paidCost > 0) { wallet.Add(paidCost); paidCost = 0; }
            FinishSession(null);
        }
    }

    /// <summary>Visual only: a small spark burst on the bag when the strike lands (rate-limited inside GameVfx).</summary>
    void BagImpact()
    {
        if (swingPivot == null) return;
        Renderer r = swingPivot.GetComponentInChildren<Renderer>();
        GameVfx.BagHit(r != null ? r.bounds.center : swingPivot.position);
    }

    void UpdateSwing()
    {
        if (swingPivot == null || chickenSpot == null) return;
        if (swingAmplitude <= 0.05f) { swingAmplitude = 0f; return; }

        swingClock += Time.deltaTime;
        swingAmplitude = Mathf.Max(0f, swingAmplitude - 22f * Time.deltaTime);
        float angle = swingAmplitude * Mathf.Cos(swingClock * 9f);
        swingPivot.localRotation = Quaternion.AngleAxis(angle, swingPivot.parent != null ? swingPivot.parent.InverseTransformDirection(chickenSpot.right) : chickenSpot.right);
    }

    bool IsPlayerInRange()
    {
        Vector3 offset = player.position - transform.position;
        offset.y = 0f; // the player's pivot is at the CharacterController centre
        return offset.sqrMagnitude <= interactionDistance * interactionDistance;
    }

    bool IsBlockedByUI()
    {
        if (dialogue != null && dialogue.IsOpen) return true;
        if (shop != null && shop.IsOpen) return true;
        return InventorySystem.Instance != null && InventorySystem.Instance.isOpen;
    }

    string PromptText()
    {
        string tool = requiredItem != null ? "  (cần " + requiredItem.displayName + ")" : "";
        return string.Format("Nhấn E: {0} (-{1} tiền)  |  {2} {3}{4}", displayName, cost, StatLabel(TrainedStat), stats.Get(TrainedStat), tool);
    }

    void SetPrompt(bool visible)
    {
        if (prompt == null) return;

        if (!visible)
        {
            if (promptShown) prompt.Hide(this);
            promptShown = false;
            shownPromptText = null;
            return;
        }

        string text = PromptText();
        if (promptShown && text == shownPromptText) return;
        prompt.Show(this, text);
        promptShown = true;
        shownPromptText = text;
    }

    public static string StatLabel(ChickenStats.StatType type)
    {
        switch (type)
        {
            case ChickenStats.StatType.Strength: return "Sức mạnh";
            case ChickenStats.StatType.Attack: return "Tấn công";
            case ChickenStats.StatType.Speed: return "Tốc độ";
            default: return "Máu tối đa";
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
        if (chickenSpot != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(chickenSpot.position, 0.3f);
            Gizmos.DrawLine(chickenSpot.position, chickenSpot.position + chickenSpot.forward);
        }
    }
}
