using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ChienKe_Pet brain: Follow the Player until a Rooster enters the detection radius,
/// then fight it (random Kick1-5 / Hurricane Kick attacks with a cooldown) until it
/// dies or wanders past the leash radius, then return to following the Player.
/// Also handles the idle push-up routine while waiting on an unmoving Player.
///
/// Animator Controller (Assets/Animations/Chicken.controller) parameters used:
///   isRunning  (bool)   - true while the NavMeshAgent is actually moving
///   isDead     (bool)   - set by HealthSystem on death (Dead state has Loop Time OFF so it freezes on last frame)
///   Attack     (trigger)- fires a transition out of AnyState into a Kick/HurricaneKick state
///   AttackIndex(int)    - each Kick1..Kick5 / HurricaneKick state's transition checks this value
///   IsPushing  (bool)   - drives ChickenIdle -> IdleToPushUp -> PushUp (loop), and
///                         PushUp -> PushUpToIdle -> ChickenIdle/Run when cleared
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class PetCombatAI : MonoBehaviour, IStopOnDeath
{
    public enum State { Follow, Combat }

    [Header("State (read-only, for debugging)")]
    public State currentState = State.Follow;

    [Header("Follow")]
    public Transform player;

    [Header("Follow speed boost (so the pet keeps up while the Player runs)")]
    [Tooltip("Speed multiplier applied while following the Player (on top of the trained speed).")]
    public float followSpeedMultiplier = 1.4f;
    [Tooltip("Extra catch-up: the multiplier rises smoothly to catchUpSpeedMultiplier as the distance to the Player grows from catchUpStartDistance to catchUpFullDistance.")]
    public float catchUpSpeedMultiplier = 2.2f;
    public float catchUpStartDistance = 8f;
    public float catchUpFullDistance = 16f;

    [Header("Target Detection")]
    [Tooltip("Tag on the enemy Rooster GameObject. Set the Rooster's tag to this value.")]
    public string enemyTag = "Rooster";
    public float detectionRadius = 6f;   // aggro range - switches Follow -> Combat
    public float leashRadius = 12f;      // give-up range - switches Combat -> Follow
    public LayerMask enemyLayerMask = ~0;
    [Tooltip("When false the pet never switches Follow -> Combat on its own. BattleManager turns this off when a battle ends and on again when the next one starts.")]
    public bool autoEngage = true;

    [Header("Combat")]
    public float attackRange = 1.8f;
    public float attackCooldown = 1.5f;
    public float rotateSpeed = 8f;
    [Tooltip("Name given to every Kick/HurricaneKick state's 'Tag' field in the Animator (Inspector, not an Animation Event). Used to lock movement/attacks while the clip plays.")]
    public string attackStateTag = "Attack";
    [Tooltip("AttackIndex values that map to Kick1-5 / Hurricane Kick states in the Animator. Check each state's transition condition in the Animator window and edit this list to match.")]
    public int[] attackIndices = { 1, 2, 3, 4, 5, 6 };
    [Tooltip("AttackIndex of the Hurricane Kick state. It is NEVER part of the random attack choice: it is the chicken's special skill (HurricaneSkill), only played by TryPlayHurricane and only once the skill is unlocked.")]
    public int hurricaneIndex = 6;

    [Header("Sword skill (unlocked by buying 'Tam kiếm Zoro' - Animator state 3122_Low)")]
    [Tooltip("AttackIndex of the 3122_Low state (Animator: AnyState -> 3122_Low when Attack + AttackIndex == this value).")]
    public int swordSkillIndex = 7;
    [Tooltip("Chance that a normal attack is the sword skill, once it is unlocked.")]
    [Range(0f, 1f)] public float swordSkillChance = 0.35f;
    [Tooltip("Seconds after the skill starts at which a slash connects (the 3122_Low clip is 1.1 s long). The clip has no Animation Events because it is shared with Zoro Tsunami.")]
    public float[] swordHitTimes = { 0.35f, 0.6f, 0.85f };
    [Tooltip("Damage of one sword slash = damage x this (3122_Low has three slashes). 3.6 = 3 x the old 1.2.")]
    public float swordHitDamageMultiplier = 3.6f;


    [Header("Hitbox")]
    [Tooltip("Empty child transform placed near the foot/beak, used as the OverlapSphere origin for hit detection.")]
    public Transform attackPoint;
    public float attackPointRadius = 0.6f;
    public float damage = 10f;

    [Header("Idle Push-Up Behavior")]
    [Tooltip("How far the Player has to move (per frame delta, in world units) before it counts as 'moving' and cancels the idle timer / push-up routine.")]
    public float idleMoveThreshold = 0.05f;
    [Tooltip("Seconds the Player must stay still before the push-up routine starts.")]
    public float idlePushUpDelay = 10f;
    [Tooltip("ON: the idle push-up routine never starts (and a running one stops). FinalVictoryCelebration turns it on once the final fight was won, so the chicken celebrates with Sui instead of doing push-ups.")]
    public bool suppressIdlePushUp;


    NavMeshAgent agent;
    Animator anim;
    Transform currentTarget;

    // follow speed boost bookkeeping: baseSpeed = the speed set by ChickenStats / the scene, without our boost
    float baseSpeed;
    float baseAcceleration;
    float lastWrittenSpeed = -1f;
    bool speedBoosted;
    float cooldownTimer;
    bool isDead;

    Vector3 lastPlayerPosition;
    float playerIdleTimer;
    bool isPushingUp;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        agent.isStopped = false;

        if (player != null)
            lastPlayerPosition = player.position;
    }

    void Update()
    {
        if (isDead) return;

        switch (currentState)
        {
            case State.Follow: TickFollow(); break;
            case State.Combat: RestoreSpeed(); TickCombat(); break;
        }

        anim.SetBool("isRunning", agent.velocity.magnitude > 0.1f);
    }

    /// <summary>Boosts the agent's speed / acceleration while following; the further behind the pet is, the faster it runs.</summary>
    void UpdateFollowSpeed()
    {
        if (agent == null || !agent.enabled || player == null) return;

        // someone else (ChickenStats after training, a story script) changed the speed: take that as the new base
        if (!speedBoosted || !Mathf.Approximately(agent.speed, lastWrittenSpeed))
        {
            baseSpeed = agent.speed;
            if (!speedBoosted) baseAcceleration = agent.acceleration;
        }

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        float t = Mathf.InverseLerp(catchUpStartDistance, Mathf.Max(catchUpStartDistance + 0.01f, catchUpFullDistance), offset.magnitude);
        float factor = Mathf.Lerp(followSpeedMultiplier, catchUpSpeedMultiplier, t);

        agent.speed = baseSpeed * factor;
        agent.acceleration = Mathf.Max(baseAcceleration, baseAcceleration * factor);
        lastWrittenSpeed = agent.speed;
        speedBoosted = true;
    }

    /// <summary>Back to the normal (trained) speed; does nothing if no boost is active.</summary>
    void RestoreSpeed()
    {
        if (!speedBoosted) return;
        speedBoosted = false;
        if (agent == null || !agent.enabled) return;
        if (Mathf.Approximately(agent.speed, lastWrittenSpeed)) agent.speed = baseSpeed;
        agent.acceleration = baseAcceleration;
    }

    void OnDisable()
    {
        RestoreSpeed(); // training / cutscenes switch this script off: they must see the normal speed
    }

    void TickFollow()
    {
        agent.isStopped = isPushingUp; // stand still while doing push-ups, otherwise keep moving
        UpdateFollowSpeed();

        if (player != null && agent.isOnNavMesh && !isPushingUp)
            agent.SetDestination(player.position);

        HandleIdlePushUp();

        Transform found = autoEngage ? FindEnemyInRange(detectionRadius) : null;
        if (found != null)
        {
            // A Rooster showed up - drop everything (including a push-up in progress) and fight
            if (isPushingUp) CancelPushUp();
            currentTarget = found;
            currentState = State.Combat;
        }
    }

    /// <summary>
    /// Starts the push-up routine after the Player has been still for idlePushUpDelay seconds,
    /// and immediately cancels it (mid IdleToPushUp OR mid looping PushUp) the instant the
    /// Player starts moving again - no waiting for the animation to finish.
    /// </summary>
void HandleIdlePushUp()
    {
        if (player == null) return;

        if (suppressIdlePushUp)
        {
            if (isPushingUp) CancelPushUp();
            playerIdleTimer = 0f;
            lastPlayerPosition = player.position;
            return;
        }

        float playerMoved = Vector3.Distance(player.position, lastPlayerPosition);
        lastPlayerPosition = player.position;

        if (playerMoved > idleMoveThreshold)
        {
            playerIdleTimer = 0f;

            if (isPushingUp)
                CancelPushUp(); // Player moved mid-routine: interrupt immediately

            return;
        }

        // Player hasn't moved. Only start counting once the pet itself has actually arrived
        // and come to a stop next to them (not mid-chase).
        if (!isPushingUp && agent.velocity.magnitude < 0.1f)
        {
            playerIdleTimer += Time.deltaTime;
            if (playerIdleTimer >= idlePushUpDelay)
                StartPushUp();
        }
    }

    void StartPushUp()
    {
        isPushingUp = true;
        agent.isStopped = true;
        anim.SetBool("IsPushing", true); // Animator: ChickenIdle -> IdleToPushUp -> PushUp (loop)
    }

    void CancelPushUp()
    {
        isPushingUp = false;
        playerIdleTimer = 0f;
        anim.SetBool("IsPushing", false); // Animator: PushUp -> PushUpToIdle -> ChickenIdle/Run
        agent.isStopped = false;          // resume chasing the Player immediately, don't wait on the animation
    }

    void TickCombat()
    {
        if (currentTarget == null)
        {
            currentState = State.Follow;
            agent.isStopped = false;
            return;
        }

        // If the target already died, drop it immediately and reset instead of waiting for the leash distance
        var targetHealth = currentTarget.GetComponentInParent<HealthSystem>();
        if (targetHealth != null && targetHealth.IsDead)
        {
            currentTarget = null;
            currentState = State.Follow;
            agent.isStopped = false;
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.position);

        // Rooster wandered too far away: give up and go back to following the Player
        if (dist > leashRadius)
        {
            currentTarget = null;
            currentState = State.Follow;
            agent.isStopped = false;
            return;
        }

        // Face the enemy at all times while in combat
        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, rotateSpeed * Time.deltaTime);
        }

        bool attackAnimPlaying = anim.GetCurrentAnimatorStateInfo(0).IsTag(attackStateTag);

        if (dist > attackRange && !attackAnimPlaying)
        {
            // Out of range: actually resume chasing
            agent.isStopped = false;
            if (agent.isOnNavMesh) agent.SetDestination(currentTarget.position);
        }
        else
        {
            // In range, or mid-attack: stand completely still (isStopped=true halts the agent's
            // internal steering, unlike re-issuing SetDestination(transform.position) which
            // still lets avoidance push it around).
            agent.isStopped = true;

            // Don't tick the cooldown or allow a new attack while one is already playing,
            // so Attack can't be re-triggered mid-animation.
            if (!attackAnimPlaying)
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0f && dist <= attackRange)
                {
                    DoAttack();
                    cooldownTimer = attackCooldown;
                }
            }
        }
    }

void DoAttack()
    {
        if (TryPlaySwordSkill(swordSkillChance)) return;

        int idx = PickRandomAttackIndex();
        anim.SetInteger("AttackIndex", idx);
        anim.SetTrigger("Attack");
    }

    /// <summary>
    /// Plays the sword skill (Animator state 3122_Low): three slashes while the chicken holds the Zoro swords (see PetEquipment).
    /// Refused (returns false, nothing is set on the Animator) while the swords are not bought, the chicken is dead, or the chance roll fails.
    /// </summary>
    public bool TryPlaySwordSkill(float chance = 1f)
    {
        if (!PetEquipment.SwordSkillUnlocked || isDead || anim == null) return false;
        if (chance < 1f && Random.value > chance) return false;
        anim.SetInteger("AttackIndex", swordSkillIndex);
        anim.SetTrigger("Attack");
        StartCoroutine(SwordSkillHits());
        return true;
    }

    IEnumerator SwordSkillHits()
    {
        float elapsed = 0f;
        for (int i = 0; i < swordHitTimes.Length; i++)
        {
            while (elapsed < swordHitTimes[i])
            {
                if (isDead) yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (isDead) yield break;
            HitTargets(damage * swordHitDamageMultiplier);
        }
    }

    /// <summary>Random attack among attackIndices WITHOUT the Hurricane Kick (a locked / special skill, see HurricaneSkill).</summary>
    int PickRandomAttackIndex()
    {
        int candidates = 0;
        for (int i = 0; i < attackIndices.Length; i++)
            if (attackIndices[i] != hurricaneIndex) candidates++;
        if (candidates == 0) return 1; // misconfigured list: fall back to Kick1 instead of Hurricane

        int pick = Random.Range(0, candidates);
        for (int i = 0; i < attackIndices.Length; i++)
        {
            if (attackIndices[i] == hurricaneIndex) continue;
            if (pick == 0) return attackIndices[i];
            pick--;
        }
        return attackIndices[0];
    }

    /// <summary>
    /// Plays the Hurricane Kick (the special skill). Refused (returns false, nothing is set on the Animator) while the skill is
    /// still locked or the chicken is dead. The only caller is HurricaneFinisher; the random combat never uses it.
    /// </summary>
    public bool TryPlayHurricane()
    {
        if (!HurricaneSkill.Unlocked || isDead || anim == null) return false;
        anim.SetInteger("AttackIndex", hurricaneIndex);
        anim.SetTrigger("Attack");
        return true;
    }

    /// <summary>
    /// Call this from an Animation Event on each Kick/HurricaneKick clip, placed on the
    /// frame where the strike actually connects. Always set the Function field to
    /// "OnAttackHit" - a blank function name is what causes console spam.
    /// </summary>
public void OnAttackHit()
    {
        HitTargets(damage);
    }

    void HitTargets(float dealt)
    {
        if (attackPoint == null) return;

        Collider[] hits = Physics.OverlapSphere(attackPoint.position, attackPointRadius, enemyLayerMask);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag(enemyTag)) continue;
            if (!HasClearLineOfSight(hit)) continue; // a wall/obstacle stands between the attack point and the target

            var hp = hit.GetComponentInParent<HealthSystem>();
            if (hp != null) hp.TakeDamage(dealt);
        }
    }

    /// <summary>True unless something solid (not the target itself, not a trigger) sits between the attack point
    /// and the target's collider - i.e. the hit does not reach through a wall. Everything in this project shares
    /// one physics layer, so this checks WHAT was hit rather than filtering by layer.</summary>
    bool HasClearLineOfSight(Collider target)
    {
        Vector3 from = attackPoint.position;
        Vector3 to = target.bounds.center;
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 0.05f) return true;

        RaycastHit hit;
        if (Physics.Raycast(from, delta / dist, out hit, dist, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider == target || hit.transform.IsChildOf(target.transform) || target.transform.IsChildOf(hit.transform);
        return true; // nothing solid in the way
    }

    Transform FindEnemyInRange(float radius)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, enemyLayerMask);
        foreach (var hit in hits)
            if (hit.CompareTag(enemyTag))
                return hit.transform;
        return null;
    }

    public void StopCombat()
    {
        RestoreSpeed();
        isDead = true;
        currentTarget = null;
        isPushingUp = false;
        if (agent != null && agent.enabled) agent.isStopped = true;
    }

    /// <summary>
    /// Undoes StopCombat() once the pet has been revived (HealthSystem.Revive) and has finished getting up:
    /// back to plain Follow with all combat/push-up state cleared.
    /// </summary>
    public void ResumeAfterRecovery()
    {
        isDead = false;
        currentTarget = null;
        currentState = State.Follow;
        cooldownTimer = 0f;
        isPushingUp = false;
        playerIdleTimer = 0f;
        if (player != null) lastPlayerPosition = player.position;
        if (anim != null) anim.SetBool("IsPushing", false);
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, leashRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        if (attackPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(attackPoint.position, attackPointRadius);
        }
    }
}
