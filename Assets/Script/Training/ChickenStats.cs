using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Trainable stats of the main chicken (put on ChienKe_Pet) and their link to the EXISTING combat:
///   Attack   -> PetCombatAI.damage            (+ damagePerAttack per point above the start value)
///   Strength -> PetCombatAI.damage            (+ damagePerStrength per point) and HealthSystem.maxHealth (+ maxHpPerStrength per point)
///   Speed    -> NavMeshAgent.speed and PetCombatAI.attackCooldown (+speedBonusPerPoint, e.g. 3 % per point: faster moving AND attacking)
///   MaxHP    -> derived: the chicken's base max health + the Strength bonus; written to HealthSystem.maxHealth
///
/// An untrained chicken (all stats at the start value) changes NOTHING in combat. The base values (damage, cooldown,
/// speed, max health) are read from the existing components once in Awake, so the combat scripts stay untouched.
/// While a battle is active the damage value is NOT written, because BattleManager overrides and later restores it
/// (first-battle balance); it is applied again on the next ApplyToCombat. Training is blocked during a battle anyway.
/// Stats are NOT saved: the project has no save system yet, so they reset when Play Mode ends.
/// </summary>
[DisallowMultipleComponent]
public class ChickenStats : MonoBehaviour
{
    public enum StatType { Strength, Attack, Speed, MaxHP }

    [Header("Start values (untrained chicken = unchanged combat)")]
    [SerializeField, Min(1)] int startStrength = 10;
    [SerializeField, Min(1)] int startAttack = 10;
    [SerializeField, Min(1)] int startSpeed = 10;
    [Tooltip("Strength / Attack / Speed cannot be trained above this value.")]
    [SerializeField, Min(1)] int maxStatValue = 40;

    [Header("Combat effect per point above the start value")]
    [SerializeField] float damagePerAttack = 1f;
    [SerializeField] float damagePerStrength = 0.5f;
    [SerializeField] float maxHpPerStrength = 5f;
    [Tooltip("0.03 = +3 % move speed and +3 % attack rate per Speed point.")]
    [SerializeField, Range(0f, 0.2f)] float speedBonusPerPoint = 0.03f;

    [Header("Trainer NPC (prepaid hours)")]
    [Tooltip("Damage added by EACH completed training hour (owner requirement: +20).")]
    [SerializeField, Min(0f)] float damagePerTrainingHour = 20f;

    public int Strength { get; private set; }
    public int Attack { get; private set; }
    public int Speed { get; private set; }

    /// <summary>Total completed training hours bought from the trainer NPC (this play session; there is no save system).</summary>
    public int TrainingHours { get; private set; }
    public float DamagePerTrainingHour => damagePerTrainingHour;

    /// <summary>Raised after any stat changed (already applied to combat).</summary>
    public event Action Changed;

    PetCombatAI combat;
    HealthSystem health;
    NavMeshAgent agent;

    float baseDamage;
    float baseCooldown;
    float baseAgentSpeed;
    float baseMaxHealth;

    public float SpeedFactor => 1f + (Speed - startSpeed) * speedBonusPerPoint;
    public float Damage => baseDamage + (Attack - startAttack) * damagePerAttack + (Strength - startStrength) * damagePerStrength + TrainingHours * damagePerTrainingHour;
    public float MaxHP => baseMaxHealth + (Strength - startStrength) * maxHpPerStrength;
    public int MaxStatValue => maxStatValue;

    void Awake()
    {
        combat = GetComponent<PetCombatAI>();
        health = GetComponent<HealthSystem>();
        agent = GetComponent<NavMeshAgent>();

        baseDamage = combat != null ? combat.damage : 0f;
        baseCooldown = combat != null ? combat.attackCooldown : 0f;
        baseAgentSpeed = agent != null ? agent.speed : 0f;
        baseMaxHealth = health != null ? health.maxHealth : 100f;

        Strength = startStrength;
        Attack = startAttack;
        Speed = startSpeed;
    }

    /// <summary>The displayed value of a stat (MaxHP is rounded).</summary>
    public int Get(StatType type)
    {
        switch (type)
        {
            case StatType.Strength: return Strength;
            case StatType.Attack: return Attack;
            case StatType.Speed: return Speed;
            default: return Mathf.RoundToInt(MaxHP);
        }
    }

    /// <summary>True if the stat cannot be trained further. MaxHP follows Strength.</summary>
    public bool IsMaxed(StatType type)
    {
        StatType t = type == StatType.MaxHP ? StatType.Strength : type; // MaxHP follows Strength
        return Get(t) >= maxStatValue;
    }

    /// <summary>Raises a trainable stat by 'amount' (clamped to the cap) and applies it to combat. False (nothing changed) if invalid or already maxed.</summary>
    public bool Train(StatType type, int amount)
    {
        if (amount <= 0 || type == StatType.MaxHP) return false;
        if (Get(type) >= maxStatValue) return false;

        switch (type)
        {
            case StatType.Strength: Strength = Mathf.Min(maxStatValue, Strength + amount); break;
            case StatType.Attack: Attack = Mathf.Min(maxStatValue, Attack + amount); break;
            case StatType.Speed: Speed = Mathf.Min(maxStatValue, Speed + amount); break;
        }

        ApplyToCombat();
        if (Changed != null) Changed();
        return true;
    }

    /// <summary>
    /// Adds completed training hours (each one = +damagePerTrainingHour damage), applies them to combat and raises Changed ONCE
    /// (one trainer session = one 'buổi tập' for the rematch quest). False (nothing changed) for hours <= 0.
    /// </summary>
    public bool AddTrainingHours(int hours)
    {
        if (hours <= 0) return false;
        TrainingHours += hours;
        ApplyToCombat();
        if (Changed != null) Changed();
        return true;
    }

    /// <summary>Writes the stat-derived values into the existing combat components.</summary>
    public void ApplyToCombat()
    {
        if (agent != null) agent.speed = baseAgentSpeed * SpeedFactor;

        if (combat != null)
        {
            combat.attackCooldown = baseCooldown / SpeedFactor;

            bool battleActive = BattleManager.Instance != null && BattleManager.Instance.IsBattleActive;
            if (!battleActive) combat.damage = Damage; // during a battle BattleManager owns the damage value
        }

        if (health != null)
        {
            float newMax = MaxHP;
            float delta = newMax - health.maxHealth;
            if (Mathf.Approximately(delta, 0f)) return;

            health.maxHealth = newMax;
            if (health.IsDead)
            {
                health.currentHealth = Mathf.Min(health.currentHealth, newMax);
            }
            else if (delta > 0f)
            {
                health.Heal(delta); // a bigger health pool is filled by the same amount; also refreshes the health bar
            }
            else
            {
                health.currentHealth = Mathf.Min(health.currentHealth, newMax);
                if (health.OnHealthChanged != null) health.OnHealthChanged.Invoke(health.currentHealth, newMax);
            }
        }
    }
}
