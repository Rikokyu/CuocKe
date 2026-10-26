using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Garden -> night -> missing chicken -> find it at the training area -> take it back (section 44 plan, checkpoints 2-4).
/// Only glue: it reuses QuestManager / QuestDefinition, DialogueUI, InteractionPromptUI, MessageBannerUI, PlayerMemeShot (camera),
/// the existing ChienKe_Pet (the ONE chicken: hidden at night, shown again at the training area) and its existing push-up animation
/// (Animator bool IsPushing). It never creates a second chicken.
///
/// Order (every step fires once; each one is blocked until the one before it happened):
///   1. garden_protein ReadyToTurnIn (the chicken ate) -> HUD/guide already say "go to bed" (existing quest data, not here).
///   2. The player sleeps (StoryBed completes garden_protein) -> the chicken leaves (hidden) -> on waking a banner says it is gone.
///   3. The player talks to Ông or Bà (their scripts call TryHandleGrandparent) -> question + answer -> button "Đi tìm con gà" accepts "tim_ga".
///   4. Accepting spawns the chicken (already in its transformed look) at the training spawn point, doing push-ups, AI off.
///   5. The player gets within 'discoverDistance' of it AND it is inside the camera view with a clear line of sight -> PlayerMemeShot.Play (meme).
///   6. After the shot: within 'takeDistance' (flat) and E -> the chicken is released (follows again), "tim_ga" completes,
///      ChickenTransformationTrigger.CompleteRecovery() unlocks training and starts chuan_bi_tai_dau.
/// The state lives only in this play session (no save system), like the quests themselves.
/// </summary>
public class MissingChickenStory : MonoBehaviour
{
    public enum Stage { Waiting, ChickenAway, Searching, Discovering, Found, Recovered }

    public static MissingChickenStory Instance { get; private set; }

    [Header("Quests")]
    [SerializeField] QuestManager questManager;
    [SerializeField] QuestDefinition gardenQuest;   // garden_protein: finished by sleeping
    [SerializeField] QuestDefinition findQuest;     // tim_ga

    [Header("Scene references")]
    [SerializeField] Transform chicken;             // ChienKe_Pet
    [SerializeField] Transform trainingSpawn;       // where the chicken is found (position + yaw)
    [SerializeField] Transform player;
    [SerializeField] StoryBed bed;
    [Tooltip("The two grandparents. Their dialogue scripts call TryHandleGrandparent(transform).")]
    [SerializeField] Transform grandfather;
    [SerializeField] Transform grandmother;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] MessageBannerUI banner;
    [SerializeField] PlayerMemeShot memeShot;
    [Tooltip("Optional. Empty = Camera.main.")]
    [SerializeField] Camera viewCamera;

    [Header("Discovery (edge of the training area)")]
    [SerializeField, Min(2f)] float discoverDistance = 10f;
    [Tooltip("The chicken must be within this angle of the camera's forward direction (3D).")]
    [SerializeField, Range(20f, 90f)] float viewHalfAngle = 60f;

    [Header("Take it back")]
    [SerializeField, Min(0.5f)] float takeDistance = 2.5f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string takePrompt = "Nhấn E để nhận lại con gà";

    [Header("Texts")]
    [SerializeField, TextArea(1, 3)] string wakeMessage = "Sáng dậy... con gà đâu rồi? Hỏi ông bà xem sao.";
    [SerializeField] string askButtonLabel = "Đi tìm con gà";
    [SerializeField, TextArea(2, 4)] string[] askGrandfatherLines =
    {
        "[Player] Ông ơi, sáng nay cháu dậy không thấy con gà đâu cả. Ông có thấy nó không ạ?",
        "Có, ông thấy nó chạy ra khỏi sân từ tờ mờ sáng, trông khoẻ re. Hình như nó chạy về phía khu huấn luyện.",
        "Cháu ra đó tìm thử xem."
    };
    [SerializeField, TextArea(2, 4)] string[] askGrandmotherLines =
    {
        "[Player] Bà ơi, bà có thấy con gà của cháu đâu không ạ? Sáng nay cháu dậy là mất tiêu rồi.",
        "Bà dậy tưới rau đã không thấy nó. Nghe người ta bảo thấy nó chạy về phía khu huấn luyện đấy.",
        "Cháu ra đó xem thử đi."
    };
    [SerializeField, TextArea(2, 4)] string[] remindLines =
    {
        "Cháu ra khu huấn luyện tìm con gà đi, chắc nó ở đó."
    };
    [SerializeField, TextArea(2, 4)] string[] remindFoundLines =
    {
        "Thấy nó rồi thì đến gần, dẫn nó về nhé."
    };
    [SerializeField, TextArea(1, 3)] string searchMessage = "Đã nhận nhiệm vụ: Tìm con gà ở khu huấn luyện.";
    [Tooltip("The Player's reaction line shown with the camera shot.")]
    [SerializeField, TextArea(1, 3)] string discoveryCaption = "Cái héo lá gì dậy fen. Con gà nhìn quen vậy";
    [SerializeField, TextArea(1, 3)] string foundHint = "Đến gần con gà và nhấn E để nhận lại nó.";

    public Stage CurrentStage { get { return stage; } }
    /// <summary>The chicken is away (hidden or at the training area and not yet taken back).</summary>
    public bool ChickenIsAway { get { return stage >= Stage.ChickenAway && stage < Stage.Recovered; } }

    Stage stage = Stage.Waiting;
    bool wakeShown;
    bool promptShown;
    bool chickenHidden;
    bool chickenHeld;           // AI/agent are switched off by this script (never leave them off)
    bool combatWasEnabled;
    bool rootMotionWas;
    bool keepStateWas;

    PetCombatAI combat;
    NavMeshAgent agent;
    Animator anim;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MissingChickenStory] A second instance was found - this one is ignored.", this);
            enabled = false;
            return;
        }
        Instance = this;
    }

    void OnEnable()
    {
        if (Instance == null) Instance = this;
        if (questManager != null) questManager.QuestCompleted += OnQuestCompleted;
    }

    void OnDisable()
    {
        if (questManager != null) questManager.QuestCompleted -= OnQuestCompleted;
        SetPrompt(false);
        if (!Application.isPlaying || !gameObject.scene.isLoaded) return;
        if (chickenHeld) ReleaseChicken(); // never leave the pet AI off
        if (chickenHidden && chicken != null) { chicken.gameObject.SetActive(true); chickenHidden = false; }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null || questManager == null || gardenQuest == null || findQuest == null || chicken == null || trainingSpawn == null || dialogue == null)
            Debug.LogError("[MissingChickenStory] A required reference is missing.", this);
        CacheChickenParts();
    }

    // ---------------------------------------------------------------- step 2: the night

    void OnQuestCompleted(QuestStatus status)
    {
        if (status.Definition == gardenQuest && stage == Stage.Waiting) ChickenLeaves();
    }

    /// <summary>The player slept: the chicken is no longer at home. Hidden (not destroyed) until the quest to find it is accepted.</summary>
    void ChickenLeaves()
    {
        stage = Stage.ChickenAway; // flip first: nothing below can run twice
        CacheChickenParts();
        if (chicken == null) return;

        HoldChicken();
        if (anim != null) { keepStateWas = anim.keepAnimatorStateOnDisable; anim.keepAnimatorStateOnDisable = true; }
        chicken.gameObject.SetActive(false);
        chickenHidden = true;
        Debug.Log("[MissingChickenStory] The chicken left during the night.");
    }

    // ---------------------------------------------------------------- step 3: asking Ông / Bà

    /// <summary>
    /// Called by the grandparents' own dialogue scripts at the start of their interaction. Returns true when this story handled
    /// the conversation (so the caller must not open its normal one). False for any other NPC or any other moment.
    /// </summary>
    public bool TryHandleGrandparent(Transform npc)
    {
        if (npc == null || dialogue == null || dialogue.IsOpen) return false;
        bool isGrandfather = npc == grandfather;
        bool isGrandmother = npc == grandmother;
        if (!isGrandfather && !isGrandmother) return false;

        string speaker = isGrandfather ? "Ông" : "Bà";
        switch (stage)
        {
            case Stage.ChickenAway:
                dialogue.Open(speaker, isGrandfather ? askGrandfatherLines : askGrandmotherLines, askButtonLabel, AcceptSearch);
                return true;
            case Stage.Searching:
            case Stage.Discovering:
                dialogue.Open(speaker, remindLines, null, null);
                return true;
            case Stage.Found:
                dialogue.Open(speaker, remindFoundLines, null, null);
                return true;
            default:
                return false;
        }
    }

    /// <summary>The dialogue button: accepts "tim_ga" and spawns the chicken at the training area. Works once (only from ChickenAway).</summary>
    public void AcceptSearch()
    {
        if (stage != Stage.ChickenAway) return;
        stage = Stage.Searching; // flip first: a second press cannot spawn a second time

        if (questManager != null && findQuest != null) questManager.AcceptQuest(findQuest);
        SpawnChickenAtTraining();
        if (banner != null && !string.IsNullOrEmpty(searchMessage)) banner.Show(searchMessage, 4f);
    }

    // ---------------------------------------------------------------- step 4: the chicken at the training area

    void SpawnChickenAtTraining()
    {
        if (chicken == null || trainingSpawn == null) return;
        CacheChickenParts();

        Vector3 pos = trainingSpawn.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(pos, out hit, 3f, NavMesh.AllAreas)) pos = hit.position;

        chicken.position = pos;
        chicken.rotation = Quaternion.Euler(0f, trainingSpawn.eulerAngles.y, 0f);

        // the transformed model: normally already swapped during the night (ChickenTransformationTrigger); make sure, never two models
        StarterChicken starter = chicken.GetComponent<StarterChicken>();
        if (starter != null && !starter.IsPet) starter.SwitchToPet();

        chicken.gameObject.SetActive(true);
        chickenHidden = false;
        if (anim != null) anim.keepAnimatorStateOnDisable = keepStateWas;

        HoldChicken();
        if (agent != null && agent.enabled)
        {
            agent.Warp(pos);
            if (agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
        }
        if (anim != null)
        {
            rootMotionWas = anim.applyRootMotion;
            anim.applyRootMotion = false; // exercise in place, like TrainingStation
            anim.SetBool("isRunning", false);
            anim.SetBool("IsPushing", true); // existing animation: ChickenIdle -> IdleToPushUp -> PushUp (loop)
        }
        Debug.Log("[MissingChickenStory] The chicken is doing push-ups at " + pos.ToString("F1"));
    }

    // ---------------------------------------------------------------- steps 5 and 6

    void Update()
    {
        switch (stage)
        {
            case Stage.ChickenAway: UpdateWake(); break;
            case Stage.Searching: UpdateSearching(); break;
            case Stage.Found: UpdateFound(); break;
        }
    }

    void UpdateWake()
    {
        if (wakeShown) return;
        if (bed != null && bed.IsSleeping) return; // the screen is still black
        wakeShown = true;
        if (banner != null && !string.IsNullOrEmpty(wakeMessage)) banner.Show(wakeMessage, 6f);
    }

    bool UiBlocked()
    {
        if (dialogue != null && dialogue.IsOpen) return true;
        if (memeShot != null && memeShot.IsPlaying) return true;
        if (bed != null && bed.IsSleeping) return true;
        if (InventorySystem.Instance != null && InventorySystem.Instance.isOpen) return true;
        BattleManager bm = BattleManager.Instance;
        return bm != null && bm.IsBattleActive;
    }

    void UpdateSearching()
    {
        if (player == null || chicken == null || UiBlocked()) return;
        if (Flat(player.position, chicken.position) > discoverDistance) return;
        if (!CanSeeChicken()) return;
        BeginDiscovery();
    }

    /// <summary>True when the chicken is inside the camera's view cone with nothing solid in between.</summary>
    public bool CanSeeChicken()
    {
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam == null || chicken == null) return false;

        Vector3 origin = cam.transform.position;
        Vector3 target = chicken.position + Vector3.up * 0.8f;
        Collider body = chicken.GetComponent<Collider>();
        if (body != null) target = body.bounds.center;

        Vector3 to = target - origin;
        float dist = to.magnitude;
        if (dist < 0.05f) return true;
        if (Vector3.Angle(cam.transform.forward, to) > viewHalfAngle) return false;

        RaycastHit[] hits = Physics.RaycastAll(origin, to / dist, dist + 0.5f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Transform t = hits[i].transform;
            if (player != null && t.IsChildOf(player)) continue; // the player's own capsule
            return t.IsChildOf(chicken);                          // first other solid thing: the chicken (visible) or a wall (blocked)
        }
        return true;
    }

    void BeginDiscovery()
    {
        stage = Stage.Discovering; // flip first: the shot is started only once
        bool started = memeShot != null && memeShot.Play(discoveryCaption, OnShotFinished);
        if (!started) // never block the story on a missing camera setup
        {
            if (banner != null && !string.IsNullOrEmpty(discoveryCaption)) banner.Show(discoveryCaption, 4f);
            OnShotFinished();
        }
    }

    void OnShotFinished()
    {
        if (stage != Stage.Discovering) return;
        stage = Stage.Found;
        if (banner != null && !string.IsNullOrEmpty(foundHint)) banner.Show(foundHint, 5f);
    }

    void UpdateFound()
    {
        if (player == null || chicken == null) { SetPrompt(false); return; }
        bool near = !UiBlocked() && Flat(player.position, chicken.position) <= takeDistance;
        SetPrompt(near);
        if (near && Input.GetKeyDown(interactKey)) TryTakeBack();
    }

    /// <summary>E action. Public so a test can drive the same path. Only after the camera shot, only close to the chicken, only once.</summary>
    public bool TryTakeBack()
    {
        if (stage != Stage.Found || player == null || chicken == null) return false;
        if (Flat(player.position, chicken.position) > takeDistance) return false; // never from afar

        stage = Stage.Recovered; // flip first: a second press cannot take it twice
        SetPrompt(false);
        ReleaseChicken();

        if (questManager != null && findQuest != null && questManager.GetState(findQuest.questId) == QuestState.Active)
        {
            questManager.ReportProgress(findQuest.objectiveKey, 1);
            questManager.TurnIn(findQuest);
        }
        if (ChickenTransformationTrigger.Instance != null) ChickenTransformationTrigger.Instance.CompleteRecovery();
        return true;
    }

    // ---------------------------------------------------------------- chicken control

    void CacheChickenParts()
    {
        if (chicken == null) return;
        if (combat == null) combat = chicken.GetComponent<PetCombatAI>();
        if (agent == null) agent = chicken.GetComponent<NavMeshAgent>();
        if (anim == null) anim = chicken.GetComponent<Animator>();
    }

    /// <summary>Switches the pet AI off and stops the agent (once). The mirror image of ReleaseChicken.</summary>
    void HoldChicken()
    {
        if (chickenHeld) return;
        chickenHeld = true;
        if (combat != null) { combatWasEnabled = combat.enabled; combat.enabled = false; }
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
    }

    /// <summary>Gives the chicken back to the existing AI: no push-up, normal root motion, plain Follow.</summary>
    void ReleaseChicken()
    {
        if (!chickenHeld) return;
        chickenHeld = false;
        if (anim != null)
        {
            anim.SetBool("IsPushing", false);
            anim.SetBool("isRunning", false);
            anim.applyRootMotion = rootMotionWas;
        }
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
        if (combat != null && combatWasEnabled)
        {
            combat.enabled = true;
            combat.ResumeAfterRecovery(); // clean Follow state (also clears push-up)
        }
    }

    static float Flat(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void SetPrompt(bool visible)
    {
        if (prompt == null) return;
        if (!visible)
        {
            if (!promptShown) return;
            promptShown = false;
            prompt.Hide(this);
            return;
        }
        if (promptShown) return;
        promptShown = true;
        prompt.Show(this, takePrompt);
    }

    void OnDrawGizmosSelected()
    {
        if (trainingSpawn == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(trainingSpawn.position, discoverDistance);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(trainingSpawn.position, takeDistance);
    }
}
