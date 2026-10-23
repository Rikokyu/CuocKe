using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// Modular health component. Attach to any combatant (ChienKe_Pet, Rooster, GuguGaga, ...).
/// Other scripts call TakeDamage(); UI and combat AI react through UnityEvents/IsDead so
/// nothing needs a hard reference to specific script types.
///
/// HealthSystem is the single authority on whether an entity is dead - combat AI scripts
/// read IsDead, they don't decide it themselves. Die() is idempotent: calling TakeDamage
/// repeatedly after death does nothing further.
/// </summary>
[DisallowMultipleComponent]
public class HealthSystem : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("References (auto-found on this GameObject if left empty)")]
    public Animator animator;
    public NavMeshAgent agent;
    [Tooltip("Colliders to disable on death (hitbox/body colliders, NOT the ground).")]
    public Collider[] colliders;

    [Header("Events")]
    public UnityEvent<float, float> OnHealthChanged; // (current, max) - hook the HealthBarUI here
    public UnityEvent OnDeath;                        // combat AI scripts (IStopOnDeath) react to this

    public bool IsDead { get; private set; }

    bool[] colliderWasEnabled; // captured in Die() so Revive() restores exactly what was on

    void Awake()
    {
        currentHealth = maxHealth;

        if (animator == null) animator = GetComponent<Animator>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (colliders == null || colliders.Length == 0) colliders = GetComponentsInChildren<Collider>();
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Brings a dead entity back (used by the post-loss recovery sequence). Does nothing and returns false
    /// unless IsDead. Restores HP to healthFraction of max (at least 1), re-enables exactly the colliders and
    /// the NavMeshAgent that Die() switched off, clears the Animator isDead flag and refreshes health bars.
    /// It does NOT play the WakeUp animation or resume combat AI - the caller owns that.
    /// </summary>
    public bool Revive(float healthFraction = 1f)
    {
        if (!IsDead) return false;
        IsDead = false;

        currentHealth = Mathf.Clamp(maxHealth * Mathf.Clamp01(healthFraction), 1f, maxHealth);

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                bool wasOn = colliderWasEnabled == null || i >= colliderWasEnabled.Length || colliderWasEnabled[i];
                if (colliders[i] != null && wasOn) colliders[i].enabled = true;
            }
        }

        if (agent != null)
        {
            agent.enabled = true;
            NavMeshHit hit;
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out hit, 3f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            if (agent.isOnNavMesh) agent.isStopped = true; // stays put until the caller resumes the AI
        }

        if (animator != null)
            animator.SetBool("isDead", false);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        return true;
    }

    void Die()
    {
        if (IsDead) return; // idempotent - Die() can only ever run once
        IsDead = true;

        // Play the Dead animation (Animator Controller has isDead -> AnyState -> Dead,
        // and the Dead clip's Loop Time is off, so it freezes on the last frame on its own).
        if (animator != null)
            animator.SetBool("isDead", true);

        // Stop and disable movement so the body doesn't slide around
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
        }

        // Disable colliders so it lies flat and can't be hit or block pathing anymore
        colliderWasEnabled = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null) continue;
            colliderWasEnabled[i] = colliders[i].enabled;
            colliders[i].enabled = false;
        }

        // Tell any combat AI on this object to stop fighting (decoupled via interface,
        // not a direct reference to PetCombatAI/RoosterCombatAI - avoids a circular dependency)
        foreach (var mb in GetComponents<MonoBehaviour>())
            if (mb is IStopOnDeath stoppable)
                stoppable.StopCombat();

        OnDeath?.Invoke();
    }
}

/// <summary>Implemented by combat AI scripts so HealthSystem can halt them on death without a hard reference.</summary>
public interface IStopOnDeath
{
    void StopCombat();
}
