using UnityEngine;

/// <summary>
/// Lightweight higher-level battle flow. Does NOT touch damage, animation or NavMesh logic
/// itself - it only tracks state, reacts to HealthSystem.OnDeath, and (for the story-mandated
/// First Battle only) temporarily overrides the two fighters' existing public `damage` fields,
/// restoring the originals the instant the battle ends. PetCombatAI/RoosterCombatAI are never
/// modified - only their already-serialized damage value is read/written from outside.
/// </summary>
public class BattleManager : MonoBehaviour
{
    public enum BattleState { None, Preparing, InBattle, PlayerWon, PlayerLost, Completed }

    public static BattleManager Instance { get; private set; }

    [Header("Fighters")]
    public HealthSystem mainChickenHealth;
    public HealthSystem enemyHealth;
    public PetCombatAI mainChickenCombat;
    public RoosterCombatAI enemyCombat;

    [Header("First Battle Balance (applied only while isFirstBattle is true, restored on end)")]
    public bool isFirstBattle = true;
    public float firstBattlePlayerDamage = 1f;
    public float firstBattleEnemyDamage = 15f; // within the requested 10-20 range

    public BattleState CurrentBattleState { get; private set; } = BattleState.None;
    public bool IsBattleActive => CurrentBattleState == BattleState.InBattle;

    /// <summary>Set once the first story battle has resolved to a loss. Runtime-only, no persistence this milestone.</summary>
    public static bool firstBattleCompleted;

    float originalPlayerDamage;
    float originalEnemyDamage;
    bool balanceApplied;

    void Awake()
    {
        Instance = this;
    }

    public void StartBattle()
    {
        if (IsBattleActive)
        {
            Debug.LogWarning("[BattleManager] StartBattle called while a battle is already active - ignored.");
            return;
        }

        if (mainChickenHealth == null || enemyHealth == null)
        {
            Debug.LogError("[BattleManager] Cannot start battle - mainChickenHealth or enemyHealth is not assigned.");
            return;
        }

        if (mainChickenHealth.IsDead || enemyHealth.IsDead)
        {
            Debug.LogWarning("[BattleManager] Cannot start battle - one of the fighters is already dead.");
            return;
        }

        CurrentBattleState = BattleState.InBattle;

        if (isFirstBattle)
            ApplyFirstBattleBalance();

        // Listen for whichever fighter dies first. The existing combat AI/HealthSystem does
        // all the actual fighting - we only react to the outcome.
        mainChickenHealth.OnDeath.AddListener(HandlePlayerChickenDeath);
        enemyHealth.OnDeath.AddListener(HandleEnemyChickenDeath);

        Debug.Log("[BattleManager] Battle Started");
    }

    void ApplyFirstBattleBalance()
    {
        if (balanceApplied) return; // safety: never double-capture "original" values

        if (mainChickenCombat != null)
        {
            originalPlayerDamage = mainChickenCombat.damage;
            mainChickenCombat.damage = firstBattlePlayerDamage;
        }
        if (enemyCombat != null)
        {
            originalEnemyDamage = enemyCombat.damage;
            enemyCombat.damage = firstBattleEnemyDamage;
        }

        balanceApplied = true;
        Debug.Log($"[BattleManager] First Battle balance applied: Player damage {originalPlayerDamage}->{firstBattlePlayerDamage}, Enemy damage {originalEnemyDamage}->{firstBattleEnemyDamage}");
    }

    void RestoreBalance()
    {
        if (!balanceApplied) return;

        if (mainChickenCombat != null) mainChickenCombat.damage = originalPlayerDamage;
        if (enemyCombat != null) enemyCombat.damage = originalEnemyDamage;

        balanceApplied = false;
        Debug.Log($"[BattleManager] First Battle balance restored: Player damage -> {originalPlayerDamage}, Enemy damage -> {originalEnemyDamage}");
    }

    public void HandlePlayerChickenDeath()
    {
        if (!IsBattleActive) return; // already ended (e.g. both died same frame) - ignore duplicate event

        CurrentBattleState = BattleState.PlayerLost;
        Debug.Log("[BattleManager] Player Lost");
        EndBattle();
    }

    public void HandleEnemyChickenDeath()
    {
        if (!IsBattleActive) return;

        CurrentBattleState = BattleState.PlayerWon;
        Debug.Log("[BattleManager] Player Won");
        EndBattle();
    }

    void EndBattle()
    {
        // Unsubscribe so a later battle (Week 2+) doesn't stack duplicate listeners,
        // and so this can't fire twice for the same death.
        if (mainChickenHealth != null) mainChickenHealth.OnDeath.RemoveListener(HandlePlayerChickenDeath);
        if (enemyHealth != null) enemyHealth.OnDeath.RemoveListener(HandleEnemyChickenDeath);

        RestoreBalance();

        // Note: we don't need to manually stop the surviving fighter's combat AI here -
        // PetCombatAI/RoosterCombatAI already detect the opponent's HealthSystem.IsDead
        // every frame and return themselves to Follow/Idle on their own.

        if (CurrentBattleState == BattleState.PlayerLost)
        {
            if (isFirstBattle) firstBattleCompleted = true;
            CurrentBattleState = BattleState.Completed;
            Debug.Log("[BattleManager] Battle Completed - first loss recorded. Main Chicken is NOT destroyed; game returns to normal gameplay. (Week 2: Quest system picks up from here.)");
        }
        else
        {
            CurrentBattleState = BattleState.Completed;
            Debug.Log("[BattleManager] Battle Completed");
        }
    }
}
