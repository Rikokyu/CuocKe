using System;
using UnityEngine;

/// <summary>
/// Unlock state of the chicken's special skill "Hurricane" (the spinning Hurricane Kick, Animator AttackIndex 6).
///
///   Locked   (default, from the chicken's transformation until the reunion with Zoro Tsunami):
///            PetCombatAI never plays the Hurricane Kick - it only uses the other kicks.
///   Unlocked (ZoroReunionStory.CompleteReunion calls Unlock() exactly once):
///            PetCombatAI.TryPlayHurricane works, and HurricaneFinisher may play the finishing move of the final rematch.
///
/// Design: the unlock is a one-way flag (no lock-again path), like every other story flag in the project; it lives only in
/// this play session (there is no save/load) and is reset together with the scene's other story singletons.
/// Without a HurricaneSkill in the scene nothing is gated (Unlocked is false, so the Hurricane simply never plays).
/// </summary>
[DisallowMultipleComponent]
public class HurricaneSkill : MonoBehaviour
{
    public static HurricaneSkill Instance { get; private set; }

    /// <summary>True once the chicken learned Hurricane. Read by PetCombatAI, HurricaneFinisher and FinalBossController.</summary>
    public static bool Unlocked { get; private set; }

    /// <summary>Raised once, when the skill becomes available.</summary>
    public static event Action SkillUnlocked;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[HurricaneSkill] A second instance was found - this one is ignored.", this);
            enabled = false;
            return;
        }
        Instance = this;
        Unlocked = false;
    }

    void OnDestroy()
    {
        if (Instance == this) { Instance = null; Unlocked = false; }
    }

    /// <summary>Unlocks the skill. Returns true only the first time (flip first, so a second call can never fire the event twice).</summary>
    public bool Unlock()
    {
        if (Unlocked) return false;
        Unlocked = true;
        Debug.Log("[HurricaneSkill] Hurricane unlocked.");
        Action handler = SkillUnlocked;
        if (handler != null) handler();
        return true;
    }
}
