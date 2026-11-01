using UnityEngine;

/// <summary>
/// Gesture driver for ONE talking NPC (first user: the childhood friend). While the DialogueUI is open, the speaker is this NPC
/// and the shown line is not the player's, it sets the Animator bool (default "isTalking") so the shared NPC_UAL controller
/// plays its Talk state; otherwise it clears it. Read-only on the dialogue: it never opens, closes or changes a dialogue,
/// quest, HP or reward. Lives next to the Animator on the model object (Humanoid avatar root).
/// </summary>
[RequireComponent(typeof(Animator))]
public class NPCTalkAnimator : MonoBehaviour
{
    [Tooltip("Must equal the speaker name the NPC's scripts pass to DialogueUI.Open (the friend uses 'Bạn thân').")]
    [SerializeField] string speakerName = "Bạn thân";
    [SerializeField] string talkingParameter = "isTalking";
    [Tooltip("Optional. If empty, the scene's DialogueUI is found at start.")]
    [SerializeField] DialogueUI dialogue;

    Animator animator;
    int talkingHash;
    bool hasParameter;
    bool playerLine;
    bool talking;

    void Awake()
    {
        animator = GetComponent<Animator>();
        talkingHash = Animator.StringToHash(talkingParameter);
    }

    void OnEnable()
    {
        if (dialogue == null) dialogue = FindObjectOfType<DialogueUI>();
        if (dialogue != null) dialogue.LineShown += OnLineShown;
        hasParameter = HasParameter();
    }

    void OnDisable()
    {
        if (dialogue != null) dialogue.LineShown -= OnLineShown;
        SetTalking(false);
    }

    void OnLineShown(bool byPlayer)
    {
        playerLine = byPlayer;
    }

    void Update()
    {
        if (dialogue == null) return;
        bool shouldTalk = dialogue.IsOpen && !playerLine && dialogue.CurrentSpeaker == speakerName;
        SetTalking(shouldTalk);
    }

    void SetTalking(bool value)
    {
        if (value == talking) return;
        talking = value;
        if (hasParameter && animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            animator.SetBool(talkingHash, value);
    }

    bool HasParameter()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.nameHash == talkingHash && p.type == AnimatorControllerParameterType.Bool) return true;
        return false;
    }
}
