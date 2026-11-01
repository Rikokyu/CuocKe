using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The Player's chicken celebrates the FINAL victory with the "Sui" animation (Animator trigger "Sui" on Chicken.controller),
/// not with push-ups.
///
///   - Starts only when FinalBossController.FinalBossDefeated is true (the final fight's win was confirmed in OnVictory):
///     never on a loss, never in the first battle, the first rematch or the tournament fights.
///   - From that moment the idle push-up routine of PetCombatAI is switched off (suppressIdlePushUp), so standing still during the
///     victory dialogue can no longer start push-ups.
///   - If HurricaneFinisher is still playing (the opponent is being thrown) Sui waits for it to finish.
///   - The clip has root motion (jump + turn): the chicken's spot and facing are put back when it ends, like EndingSequence does.
///   - Plays once. EndingSequence is untouched and still plays its own celebration before the fade (the ending flow and its
///     rewards are not changed); if the ending already started this script leaves the chicken to it.
/// Put it on ChienKe_Pet.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PetCombatAI))]
public class FinalVictoryCelebration : MonoBehaviour
{
    [SerializeField] string suiTrigger = "Sui";
    [Tooltip("Seconds Sui is allowed to play before the chicken is given back to its AI (the clip is about 3 s).")]
    [SerializeField, Min(0.5f)] float celebrateSeconds = 3.2f;

    /// <summary>True once Sui was started (this play session). Diagnostics for tests.</summary>
    public bool Played { get; private set; }

    Animator anim;
    PetCombatAI combat;
    NavMeshAgent agent;
    HealthSystem health;
    bool pushUpSuppressed;

    void Awake()
    {
        anim = GetComponent<Animator>();
        combat = GetComponent<PetCombatAI>();
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<HealthSystem>();
    }

    void OnDisable()
    {
        if (pushUpSuppressed && combat != null) combat.suppressIdlePushUp = false;
        pushUpSuppressed = false;
    }

    void Update()
    {
        if (!FinalBossController.FinalBossDefeated)
        {
            // not beaten (or the scene restarted): everything back to normal
            if (pushUpSuppressed && combat != null) combat.suppressIdlePushUp = false;
            pushUpSuppressed = false;
            Played = false;
            return;
        }

        // the win is confirmed: no push-up from now on
        if (!pushUpSuppressed && combat != null)
        {
            combat.suppressIdlePushUp = true;
            pushUpSuppressed = true;
            if (anim != null && HasBool("IsPushing")) anim.SetBool("IsPushing", false);
        }

        // With an EndingSequence in the scene the celebration is played there, AFTER the friend's talk (camera: friend, then the chicken).
        if (EndingSequence.Instance != null) return;
        if (Played || EndingSequence.Started) return;
        HurricaneFinisher finisher = HurricaneFinisher.Instance;
        if (finisher != null && finisher.Playing) return; // let the finishing move end first
        if (health != null && health.IsDead) return;
        if (anim == null || !HasTrigger(suiTrigger)) return;

        Played = true; // flip first: Sui is started only once
        StartCoroutine(Celebrate());
    }

    IEnumerator Celebrate()
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        bool combatWasOn = combat != null && combat.enabled;

        if (combat != null) combat.enabled = false; // the follow AI would walk the chicken away from its spot
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        if (HasBool("isRunning")) anim.SetBool("isRunning", false);
        if (HasBool("IsPushing")) anim.SetBool("IsPushing", false);
        anim.SetTrigger(suiTrigger);

        float t = 0f;
        while (t < celebrateSeconds && !EndingSequence.Started)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (EndingSequence.Started) yield break; // the ending owns the chicken now (it restores the AI itself)

        anim.ResetTrigger(suiTrigger);
        if (agent != null && agent.enabled)
        {
            if ((transform.position - startPosition).sqrMagnitude > 0.0025f) agent.Warp(startPosition);
        }
        else transform.position = startPosition;
        transform.rotation = startRotation;

        if (combat != null && combatWasOn) { combat.enabled = true; combat.ResumeAfterRecovery(); }
        else if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
    }

    bool HasBool(string name)
    {
        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Bool) return true;
        return false;
    }

    bool HasTrigger(string name)
    {
        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Trigger) return true;
        return false;
    }
}
