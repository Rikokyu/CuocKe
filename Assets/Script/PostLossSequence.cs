using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// What happens after the first battle is lost. Listens to BattleManager.PlayerLostBattle and, ONCE:
///   1. shows the loss message,
///   2. lets the chicken lie down for a moment (BattleManager already stood both fighters down:
///      damage restored, autoEngage off, listeners removed),
///   3. waits until no attack animation is still playing,
///   4. revives the chicken through HealthSystem.Revive (only acts if it is really dead),
///   5. plays the existing WakeUp animation and waits for the Animator to reach ChickenIdle (polled, not a timer,
///      same approach as MainChickenAcquire),
///   6. hands control back to PetCombatAI (Follow). Auto-engage stays OFF until the next StartBattle.
/// No scene reload, no Destroy, no fake HP: the chicken's HP is only touched by Revive().
/// Player controls are never disabled by combat, so there is nothing to restore here.
/// </summary>
public class PostLossSequence : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] BattleManager battleManager;
    [SerializeField] HealthSystem chickenHealth;
    [SerializeField] PetCombatAI chickenCombat;
    [Tooltip("Optional: only used to wait for the enemy's last attack animation to finish.")]
    [SerializeField] RoosterCombatAI enemyCombat;
    [SerializeField] MessageBannerUI messageBanner;

    [Header("Message")]
    [SerializeField] string lossMessage = "Bạn đã thua trận! Hãy tìm cách huấn luyện gà mạnh hơn.";
    [SerializeField, Min(1f)] float messageSeconds = 5f;

    [Header("Recovery")]
    [Tooltip("Seconds the chicken stays knocked down before it starts to get up.")]
    [SerializeField, Min(0f)] float getUpDelay = 3f;
    [Tooltip("Share of max HP the chicken has after getting up.")]
    [SerializeField, Range(0.1f, 1f)] float recoveredHealthFraction = 1f;
    [Tooltip("Give up waiting for the get-up animation after this many seconds (logged as a warning).")]
    [SerializeField, Min(1f)] float getUpTimeout = 8f;
    [SerializeField] string idleStateName = "ChickenIdle";

    [Header("Events")]
    public UnityEvent onSequenceStarted;
    public UnityEvent onChickenRecovered;

    public bool HasStarted { get; private set; }
    public bool IsComplete { get; private set; }

    void OnEnable()
    {
        if (battleManager != null) battleManager.PlayerLostBattle += OnPlayerLostBattle;
    }

    void OnDisable()
    {
        if (battleManager != null) battleManager.PlayerLostBattle -= OnPlayerLostBattle;
    }

    void OnPlayerLostBattle()
    {
        if (HasStarted) return; // one loss, one sequence - never twice
        HasStarted = true;
        StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        Debug.Log("[PostLoss] Sequence started");
        onSequenceStarted?.Invoke();
        if (messageBanner != null) messageBanner.Show(lossMessage, messageSeconds);

        // Stay down for a moment, and never get up while a battle is still considered active.
        yield return new WaitForSeconds(getUpDelay);
        while (battleManager != null && battleManager.IsBattleActive) yield return null;

        // Don't get up while the enemy is still in the middle of a kick.
        yield return WaitForEnemyAttackToEnd(3f);

        if (chickenHealth == null || chickenCombat == null)
        {
            Debug.LogError("[PostLoss] Chicken references missing - cannot recover.", this);
            yield break;
        }

        if (chickenHealth.Revive(recoveredHealthFraction))
        {
            Animator anim = chickenHealth.animator != null ? chickenHealth.animator : chickenHealth.GetComponent<Animator>();
            if (anim != null)
            {
                if (HasParameter(anim, "isRunning", AnimatorControllerParameterType.Bool)) anim.SetBool("isRunning", false);
                if (HasParameter(anim, "IsPushing", AnimatorControllerParameterType.Bool)) anim.SetBool("IsPushing", false);
                if (HasParameter(anim, "WakeUp", AnimatorControllerParameterType.Trigger)) anim.SetTrigger("WakeUp");
                else Debug.LogWarning("[PostLoss] Animator has no 'WakeUp' trigger - skipping the get-up animation.", this);

                yield return WaitForIdle(anim);
            }
        }
        else
        {
            Debug.LogWarning("[PostLoss] Chicken was not dead - nothing to revive, resuming follow only.", this);
        }

        chickenCombat.ResumeAfterRecovery();
        IsComplete = true;
        Debug.Log("[PostLoss] Chicken recovered - following the player again");
        onChickenRecovered?.Invoke();
    }

    IEnumerator WaitForEnemyAttackToEnd(float maxSeconds)
    {
        if (enemyCombat == null) yield break;
        Animator enemyAnim = enemyCombat.GetComponent<Animator>();
        if (enemyAnim == null) yield break;

        float waited = 0f;
        while (waited < maxSeconds && enemyAnim.GetCurrentAnimatorStateInfo(0).IsTag(enemyCombat.attackStateTag))
        {
            waited += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator WaitForIdle(Animator anim)
    {
        yield return null; // let the Animator consume the WakeUp trigger first

        float waited = 0f;
        while (waited < getUpTimeout)
        {
            if (anim.GetCurrentAnimatorStateInfo(0).IsName(idleStateName)) yield break;
            waited += Time.deltaTime;
            yield return null;
        }
        Debug.LogWarning("[PostLoss] Timed out waiting for '" + idleStateName + "' - resuming anyway.", this);
    }

    static bool HasParameter(Animator anim, string parameterName, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.name == parameterName && p.type == type) return true;
        return false;
    }
}
