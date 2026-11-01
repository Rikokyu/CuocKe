using UnityEngine;

/// <summary>
/// CJ (the childhood friend NPC) reacts to losing the FINAL rematch: both hands on the head / behind the ears, head tilted back,
/// stunned. It reuses the existing rig and the shared NPC_UAL controller: its "Reaction" layer (upper-body mask: head, arms, fingers)
/// holds the HandsOnHead state, switched by the bool "isShocked". This script only sets that bool and the layer weight.
///
///   - Plays ONLY when FinalBossController.FinalBossDefeated is true, i.e. after the final fight's win was confirmed
///     (OnVictory). A loss, the first rematch, the tournament fights and the first battle never set it.
///   - Never moves or replaces the NPC: the layer is masked to the upper body, root motion is off on this model, and nothing
///     here touches the transform, the model, the quests or the dialogue. The Base layer (idle / talk / gestures) keeps running.
///   - Once the pose started it is held for the rest of the game (the ending takes the player home right after).
/// Put it on the object that holds the CJ Animator (the Humanoid model under NPC_ChildhoodFriend).
/// </summary>
[RequireComponent(typeof(Animator))]
public class FriendDefeatReaction : MonoBehaviour
{
    [SerializeField] string shockedParameter = "isShocked";
    [SerializeField] string reactionLayerName = "Reaction";
    [Tooltip("Seconds after the confirmed victory before CJ reacts (the opponent rooster is still flying away at that moment).")]
    [SerializeField, Min(0f)] float delaySeconds = 0.25f;
    [SerializeField, Min(0.05f)] float blendSeconds = 0.25f;

    /// <summary>True while the shocked pose is on (diagnostics for tests).</summary>
    public bool IsShocked { get; private set; }

    Animator animator;
    int shockedHash;
    int layerIndex = -1;
    bool hasParameter;
    float reactAt = -1f;
    float weight;

    void Awake()
    {
        animator = GetComponent<Animator>();
        shockedHash = Animator.StringToHash(shockedParameter);
    }

    void OnEnable()
    {
        hasParameter = false;
        layerIndex = -1;
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.nameHash == shockedHash && p.type == AnimatorControllerParameterType.Bool) hasParameter = true;
        layerIndex = animator.GetLayerIndex(reactionLayerName);
        if (!hasParameter || layerIndex < 0)
            Debug.LogWarning("[FriendDefeatReaction] The Animator needs the bool '" + shockedParameter + "' and the layer '" + reactionLayerName + "' (NPC_UAL controller) - no reaction will play.", this);
    }

    void OnDisable()
    {
        Apply(false, 0f);
    }

    void Update()
    {
        if (!hasParameter || layerIndex < 0 || animator == null) return;

        if (!FinalBossController.FinalBossDefeated)
        {
            // never beaten (or the scene restarted): make sure the pose is off
            if (IsShocked || weight > 0f) Apply(false, 0f);
            reactAt = -1f;
            return;
        }

        if (reactAt < 0f) reactAt = Time.time + delaySeconds;
        if (Time.time < reactAt) return;

        weight = Mathf.MoveTowards(weight, 1f, Time.deltaTime / blendSeconds);
        Apply(true, weight);
    }

    void Apply(bool shocked, float w)
    {
        weight = w;
        IsShocked = shocked;
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null || layerIndex < 0) return;
        animator.SetBool(shockedHash, shocked);
        animator.SetLayerWeight(layerIndex, w);
    }
}
