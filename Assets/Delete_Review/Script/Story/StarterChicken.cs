using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The starter "normal chicken" and the one-time swap to the old chicken_pet model.
///
/// Design: ChienKe_Pet stays the ONE gameplay chicken (NavMeshAgent, HealthSystem, PetCombatAI, MainChickenAcquire,
/// ChickenStats, quest targets, health bar ... all keep their references, spawn point and follow behaviour).
/// Only the VISUAL changes: until the transformation the old mixamo mesh is hidden and a normal_chicken prefab
/// instance (child) is shown; SwitchToPet() destroys that child and shows the old mesh again, so there is never
/// more than one chicken in the scene.
///
/// The two models do NOT share a rig (normal_chicken = generic chicken rig, ChienKe_Pet = humanoid mixamo), so the
/// normal chicken has its own Animator (Normal_Chicken.controller) that is only fed parameters from here:
///   isDown    = the pet Animator's isDead flag (lying in the yard / knocked out)
///   isRunning = the NavMeshAgent is moving
///   isScared  = fleeing / cowering in the first battle
///
/// First battle: no compatible fight animation exists for the normal chicken, so while it is NOT the pet it never
/// attacks and never deals damage (PetCombatAI is switched off for the battle). It runs toward the rooster so the
/// fight really starts, then runs away, then cowers, so the rooster (existing RoosterCombatAI, first-battle damage)
/// beats it and BattleManager reports the loss as usual. If the loss has not happened after failsafeSeconds the
/// chicken is knocked out through HealthSystem.TakeDamage (same path as a real death) so the battle can never hang.
/// </summary>
[RequireComponent(typeof(PetCombatAI))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(HealthSystem))]
public class StarterChicken : MonoBehaviour
{
    public static StarterChicken Instance { get; private set; }

    [Header("Visuals")]
    [Tooltip("Instance of the normal_chicken prefab (child of this object).")]
    [SerializeField] GameObject normalVisual;
    [SerializeField] Animator normalAnimator;
    [Tooltip("The old chicken_pet meshes. Hidden until SwitchToPet().")]
    [SerializeField] Renderer[] petRenderers;

    [Header("First battle (normal chicken only)")]
    [SerializeField] BattleManager battle;
    [SerializeField] float approachDistance = 4.5f;
    [SerializeField] float approachMaxSeconds = 15f;
    [SerializeField] float fleeSeconds = 7f;
    [SerializeField] float fleeStep = 6f;
    [SerializeField] float fleeSpeed = 3f;
    [SerializeField] float failsafeSeconds = 45f;
    [SerializeField] float repathInterval = 0.3f;

    public bool IsPet { get; private set; }

    enum Phase { None, Approach, Flee, Cower }

    NavMeshAgent agent;
    Animator petAnimator;
    PetCombatAI combat;
    HealthSystem health;

    Phase phase = Phase.None;
    float phaseStart;
    float battleStart;
    float nextRepath;
    float savedSpeed;
    float savedStopping;
    bool combatWasEnabled;

    void Awake()
    {
        Instance = this;
        agent = GetComponent<NavMeshAgent>();
        petAnimator = GetComponent<Animator>();
        combat = GetComponent<PetCombatAI>();
        health = GetComponent<HealthSystem>();
        if (normalAnimator == null && normalVisual != null) normalAnimator = normalVisual.GetComponentInChildren<Animator>();
        ShowVisual(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void ShowVisual(bool pet)
    {
        if (normalVisual != null) normalVisual.SetActive(!pet);
        if (petRenderers != null)
            foreach (Renderer r in petRenderers)
                if (r != null) r.enabled = pet;
    }

    void Update()
    {
        if (IsPet) return;

        BattleManager bm = battle != null ? battle : BattleManager.Instance;
        bool inBattle = bm != null && bm.IsBattleActive;
        if (phase == Phase.None && !inBattle)
        {
            // before the fight the normal chicken must not brawl on its own: it never attacks, and the rooster waits for StartBattle
            combat.autoEngage = false;
            if (bm != null && bm.enemyCombat != null && bm.CurrentBattleState == BattleManager.BattleState.None) bm.enemyCombat.autoEngage = false;
        }
        if (inBattle && phase == Phase.None) BeginBattleBehaviour();
        else if (!inBattle && phase != Phase.None) EndBattleBehaviour();
        if (phase != Phase.None) TickBattleBehaviour();

        FeedAnimator();
    }

    void FeedAnimator()
    {
        if (normalAnimator == null || !normalAnimator.isActiveAndEnabled) return;
        bool down = petAnimator != null && HasBool(petAnimator, "isDead") && petAnimator.GetBool("isDead");
        bool moving = agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped && agent.velocity.sqrMagnitude > 0.01f;
        normalAnimator.SetBool("isDown", down);
        normalAnimator.SetBool("isRunning", moving && !down);
        normalAnimator.SetBool("isScared", phase != Phase.None && !down);
    }

    static bool HasBool(Animator a, string name)
    {
        foreach (AnimatorControllerParameter p in a.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Bool) return true;
        return false;
    }

    // ---------------------------------------------------------------- first battle

    void BeginBattleBehaviour()
    {
        battleStart = Time.time;
        savedSpeed = agent.speed;
        savedStopping = agent.stoppingDistance;
        combatWasEnabled = combat.enabled;
        combat.enabled = false; // no kicks, no damage: the normal chicken cannot fight
        agent.stoppingDistance = 0f; // normal speed while approaching, slower than the rooster only once it flees
        SetPhase(Phase.Approach);
        Debug.Log("[StarterChicken] First battle: the normal chicken cannot fight - it will run away.");
    }

    void EndBattleBehaviour()
    {
        phase = Phase.None;
        agent.speed = savedSpeed;
        agent.stoppingDistance = savedStopping;
        if (combatWasEnabled) combat.enabled = true; // PostLossSequence.ResumeAfterRecovery still does the rest
        if (agent.enabled && agent.isOnNavMesh && !health.IsDead) agent.isStopped = false;
    }

    void SetPhase(Phase p)
    {
        phase = p;
        phaseStart = Time.time;
        nextRepath = 0f;
        if (p == Phase.Flee) agent.speed = fleeSpeed; // slower than the rooster (3.5) so it gets caught
    }

    void TickBattleBehaviour()
    {
        if (health.IsDead) return;

        // the battle must never hang: knock the chicken out through the normal health path
        if (Time.time - battleStart > failsafeSeconds)
        {
            Debug.LogWarning("[StarterChicken] Failsafe: the rooster did not finish the fight in time - knocking the chicken out.");
            health.TakeDamage(health.maxHealth);
            return;
        }

        BattleManager bm = battle != null ? battle : BattleManager.Instance;
        Transform enemy = bm != null && bm.enemyCombat != null ? bm.enemyCombat.transform : null;
        if (enemy == null || !agent.enabled || !agent.isOnNavMesh) return;

        Vector3 toEnemy = enemy.position - transform.position;
        toEnemy.y = 0f;

        switch (phase)
        {
            case Phase.Approach:
                // get close enough that the rooster notices and engages (its detection radius is small)
                if (toEnemy.magnitude <= approachDistance || Time.time - phaseStart > approachMaxSeconds) { SetPhase(Phase.Flee); break; }
                agent.isStopped = false;
                Repath(enemy.position);
                break;

            case Phase.Flee:
                if (Time.time - phaseStart > fleeSeconds) { SetPhase(Phase.Cower); break; }
                agent.isStopped = false;
                Vector3 away = toEnemy.sqrMagnitude > 0.01f ? -toEnemy.normalized : -transform.forward;
                Repath(transform.position + away * fleeStep);
                break;

            case Phase.Cower:
                agent.isStopped = true; // frozen with fear: the rooster catches up and finishes the scripted loss
                break;
        }
    }

    void Repath(Vector3 target)
    {
        if (Time.time < nextRepath) return;
        nextRepath = Time.time + repathInterval;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(target, out hit, 4f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }

    // ---------------------------------------------------------------- transformation

    /// <summary>One-time swap normal_chicken -> old chicken_pet model. Same GameObject, same position, same follow state. Safe to call twice.</summary>
    public bool SwitchToPet()
    {
        if (IsPet) return false;
        if (phase != Phase.None) EndBattleBehaviour();
        IsPet = true;

        if (normalVisual != null) Destroy(normalVisual); // never two chickens
        normalVisual = null;
        normalAnimator = null;
        ShowVisual(true);
        Debug.Log("[StarterChicken] Transformed: normal_chicken replaced by the old chicken_pet model.");
        return true;
    }
}
