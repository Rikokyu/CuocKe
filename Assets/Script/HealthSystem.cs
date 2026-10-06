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
        foreach (var col in colliders)
            if (col != null) col.enabled = false;

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
