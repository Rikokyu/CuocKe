using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Rooster brain: mirror of PetCombatAI. Idles near its spawn point until ChienKe_Pet
/// enters detection range, then fights back with the same random Kick1-5 / Hurricane
/// Kick attacks. Returns toward its spawn point if the Pet dies or leashes away.
///
/// Same fixes as PetCombatAI: explicit agent.isStopped control, an attack lock via
/// the Animator state Tag ("Attack" on every Kick/HurricaneKick state) so the cooldown
/// can't re-trigger a new attack mid-animation, and an immediate reset the instant the
/// current target's HealthSystem reports dead (instead of waiting for the leash distance).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class RoosterCombatAI : MonoBehaviour, IStopOnDeath
{
    public enum State { Idle, Combat }

    [Header("State (read-only, for debugging)")]
    public State currentState = State.Idle;

    [Header("Target Detection")]
    [Tooltip("Tag on the Pet GameObject to fight (ChienKe_Pet is already tagged 'Pet').")]
    public string enemyTag = "Pet";
    public float detectionRadius = 6f;
    public float leashRadius = 12f;
    public LayerMask enemyLayerMask = ~0;
    [Tooltip("When false the rooster never switches Idle -> Combat on its own. BattleManager turns this off when a battle ends and on again when the next one starts.")]
    public bool autoEngage = true;

    [Header("Combat")]
    public float attackRange = 1.8f;
    public float attackCooldown = 1.5f;
    public float rotateSpeed = 8f;
    [Tooltip("Name given to every Kick/HurricaneKick state's 'Tag' field in the Animator (Inspector, not an Animation Event).")]
    public string attackStateTag = "Attack";
    [Tooltip("AttackIndex values mapping to Kick1-5 / Hurricane Kick states - match the Animator's transition conditions.")]
    public int[] attackIndices = { 1, 2, 3, 4, 5, 6 };

    [Header("Hitbox")]
    public Transform attackPoint;
    public float attackPointRadius = 0.6f;
    public float damage = 10f;

    NavMeshAgent agent;
    Animator anim;
    Transform currentTarget;
    Vector3 spawnPoint;
    float cooldownTimer;
    bool isDead;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        agent.isStopped = false;
        spawnPoint = transform.position;
    }

    void Update()
    {
        if (isDead) return;

        switch (currentState)
        {
            case State.Idle: TickIdle(); break;
            case State.Combat: TickCombat(); break;
        }

        anim.SetBool("isRunning", agent.velocity.magnitude > 0.1f);
    }

    void TickIdle()
    {
        agent.isStopped = false;

        // Walk back to spawn (original idle/guard position) if it drifted away, otherwise stand still
        if (agent.isOnNavMesh)
        {
            float distToSpawn = Vector3.Distance(transform.position, spawnPoint);
            agent.SetDestination(distToSpawn > 0.5f ? spawnPoint : transform.position);
        }

        Transform found = autoEngage ? FindEnemyInRange(detectionRadius) : null;
        if (found != null)
        {
            currentTarget = found;
            currentState = State.Combat;
        }
    }

    void TickCombat()
    {
        if (currentTarget == null)
        {
            currentState = State.Idle;
            agent.isStopped = false;
            return;
        }

        // If the target already died, drop it immediately and head back to the guard position
        // instead of waiting for the leash distance.
        var targetHealth = currentTarget.GetComponentInParent<HealthSystem>();
        if (targetHealth != null && targetHealth.IsDead)
        {
            currentTarget = null;
            currentState = State.Idle;
            agent.isStopped = false;
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.position);

        if (dist > leashRadius)
        {
            currentTarget = null;
            currentState = State.Idle;
            agent.isStopped = false;
            return;
        }

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
            agent.isStopped = false;
            if (agent.isOnNavMesh) agent.SetDestination(currentTarget.position);
        }
        else
        {
            agent.isStopped = true;

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
        int idx = attackIndices[Random.Range(0, attackIndices.Length)];
        anim.SetInteger("AttackIndex", idx);
        anim.SetTrigger("Attack");
    }

    /// <summary>Call from an Animation Event on the connect frame of each attack clip. Function field must be "OnAttackHit".</summary>
    public void OnAttackHit()
    {
        if (attackPoint == null) return;

        Collider[] hits = Physics.OverlapSphere(attackPoint.position, attackPointRadius, enemyLayerMask);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag(enemyTag)) continue;
            if (!HasClearLineOfSight(hit)) continue; // a wall/obstacle stands between the attack point and the target

            var hp = hit.GetComponentInParent<HealthSystem>();
            if (hp != null) hp.TakeDamage(damage);
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
        isDead = true;
        currentTarget = null;
        if (agent != null && agent.enabled) agent.isStopped = true;
    }

    /// <summary>
    /// Counterpart of StopCombat() for a rooster that was killed and then revived by HealthSystem.Revive (the final boss
    /// reuses the rooster that died in the earlier rematch). Without it StopCombat's private isDead flag stays set and the
    /// Animator stays in 'Dead' (that state only leaves through the WakeUp trigger), so the rooster lay there forever.
    /// Call it only after Revive() returned true.
    /// </summary>
    public void ResumeAfterRevive()
    {
        isDead = false;
        currentTarget = null;
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            foreach (AnimatorControllerParameter prm in anim.parameters)
            {
                if (prm.name == "isRunning" && prm.type == AnimatorControllerParameterType.Bool) anim.SetBool("isRunning", false);
                if (prm.name == "WakeUp" && prm.type == AnimatorControllerParameterType.Trigger) anim.SetTrigger("WakeUp");
            }
        }
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
