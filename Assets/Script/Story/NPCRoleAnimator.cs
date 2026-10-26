using UnityEngine;

/// <summary>
/// Gives one NPC a role-based life on the shared NPC_UAL controller (no new controller):
///  - every few seconds, while the NPC is idle and no dialogue is open, it plays one gesture from its own list
///    (picking things up, tinkering, shadow boxing, tidying ...). The gesture ids are the ones the controller defines:
///    1 PickUp_Table, 2 Interact, 3 Punch_Jab, 4 Punch_Cross, 5 Fixing_Kneeling, 6 Idle_Torch_Loop, 7 Dance_Loop.
///  - if a speaker name is set, the Talk pose is shown while that NPC speaks (same rule as NPCTalkAnimator, which stays
///    on the childhood friend; leave the name empty there).
/// It only sets Animator parameters. It never opens/closes dialogue and never touches quests, shops, rewards, colliders.
/// Walking: the controller also has isWalking; set it from a script that really moves the NPC (none does yet).
/// </summary>
[RequireComponent(typeof(Animator))]
public class NPCRoleAnimator : MonoBehaviour
{
    [Tooltip("Gesture ids to cycle through (see the summary). Empty = idle / talk only.")]
    [SerializeField] int[] gestureIds;
    [SerializeField] Vector2 secondsBetweenGestures = new Vector2(8f, 16f);
    [Tooltip("Speaker names this NPC's scripts pass to DialogueUI.Open (an NPC can have several scripts with different names). Empty = no talk pose from this script.")]
    [SerializeField] string[] speakerNames;
    [SerializeField] DialogueUI dialogue;
    [Tooltip("The talk pose only shows while the player is this close (several villagers share the speaker name 'Dân làng').")]
    [SerializeField, Min(1f)] float talkRange = 7f;
    Transform player;

    Animator animator;
    int gestureHash, doGestureHash, talkingHash;
    bool hasGesture, hasTalking;
    bool playerLine, talking;
    float nextGesture;
    public int GesturesPlayed { get; private set; }

    void Awake()
    {
        animator = GetComponent<Animator>();
        gestureHash = Animator.StringToHash("gesture");
        doGestureHash = Animator.StringToHash("doGesture");
        talkingHash = Animator.StringToHash("isTalking");
    }

    void OnEnable()
    {
        if (dialogue == null) dialogue = FindFirstObjectByType<DialogueUI>(FindObjectsInactive.Include);
        if (dialogue != null) dialogue.LineShown += OnLineShown;
        hasGesture = Has("gesture", AnimatorControllerParameterType.Int) && Has("doGesture", AnimatorControllerParameterType.Trigger);
        hasTalking = Has("isTalking", AnimatorControllerParameterType.Bool);
        nextGesture = Time.time + Random.Range(0f, secondsBetweenGestures.y); // NPCs do not move in sync
    }

    void OnDisable()
    {
        if (dialogue != null) dialogue.LineShown -= OnLineShown;
        SetTalking(false);
    }

    void OnLineShown(bool byPlayer) { playerLine = byPlayer; }

    bool PlayerNear()
    {
        if (player == null) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.transform; }
        return player == null || (player.position - transform.position).sqrMagnitude <= talkRange * talkRange;
    }

    bool Has(string n, AnimatorControllerParameterType t)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        foreach (AnimatorControllerParameter p in animator.parameters) if (p.name == n && p.type == t) return true;
        return false;
    }

    void SetTalking(bool v)
    {
        if (v == talking) return;
        talking = v;
        if (hasTalking && animator != null && animator.isActiveAndEnabled) animator.SetBool(talkingHash, v);
    }

    void Update()
    {
        bool dialogueOpen = dialogue != null && dialogue.IsOpen;
        if (speakerNames != null && speakerNames.Length > 0)
            SetTalking(dialogueOpen && !playerLine && System.Array.IndexOf(speakerNames, dialogue.CurrentSpeaker) >= 0 && PlayerNear());

        if (!hasGesture || gestureIds == null || gestureIds.Length == 0 || dialogueOpen) return;
        if (Time.time < nextGesture) return;
        nextGesture = Time.time + Random.Range(secondsBetweenGestures.x, secondsBetweenGestures.y);

        // only from the plain Idle state (never over Walk / Talk / another gesture)
        if (animator.IsInTransition(0) || !animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) return;
        PlayGesture(gestureIds[Random.Range(0, gestureIds.Length)]);
    }

    /// <summary>Plays one gesture now (also used by tests).</summary>
    public void PlayGesture(int id)
    {
        if (!hasGesture) return;
        animator.SetInteger(gestureHash, id);
        animator.SetTrigger(doGestureHash);
        GesturesPlayed++;
    }
}
