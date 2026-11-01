using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// After the village tournament: rest -> the chicken disappears again -> it is found watching Zoro Tsunami -> Hurricane is unlocked.
/// Only glue: it reuses QuestManager / QuestDefinition, StoryBed (second sleep), DialogueUI, MessageBannerUI, the existing
/// ChienKe_Pet (the ONE chicken: hidden during the night, shown again near Zoro) and HurricaneSkill (the unlock flag).
/// It never creates a second chicken and never touches combat, health or the BattleManager.
///
/// Order (every step fires once and is blocked until the one before it happened):
///   1. giai_dau_lang Completed (Chị Ba AND Chú Tư beaten)  -> quest "ve_nha_nghi_ngoi" starts: go home and sleep in the bed.
///   2. The player sleeps (StoryBed.TrySleepAgain, only possible now) -> the chicken leaves (hidden) -> on waking a banner says it is gone.
///   3. The player talks to Ông or Bà (their scripts call TryHandleGrandparent) -> question, "Nó đi theo thằng tóc xanh lá rồi!",
///      "Ơ, thằng mù đường mà?" -> button "Đi tìm con gà" accepts "tim_ga_zoro".
///   4. Accepting puts the chicken (still the transformed one) at 'watchSpot', far from the Zoro Tsunami NPC, facing him, AI off.
///   5. The player gets in view of it (banner), then close to it -> the reunion dialogue opens by itself.
///   6. When that dialogue is closed: the chicken follows again, "tim_ga_zoro" completes and HurricaneSkill.Unlock() runs (once).
/// The final rematch (FinalBossController) stays closed until step 6 (HurricaneSkill.Unlocked).
/// State lives only in this play session (no save system), like the quests themselves.
/// </summary>
public class ZoroReunionStory : MonoBehaviour
{
    public enum Stage { Waiting, Resting, ChickenAway, Searching, Found, Reunion, Recovered }

    public static ZoroReunionStory Instance { get; private set; }

    [Header("Quests")]
    [SerializeField] QuestManager questManager;
    [Tooltip("giai_dau_lang: completing it (both opponents beaten) starts this story.")]
    [SerializeField] QuestDefinition tournamentQuest;
    [Tooltip("ve_nha_nghi_ngoi: go home and sleep. Completed by sleeping.")]
    [SerializeField] QuestDefinition restQuest;
    [Tooltip("tim_ga_zoro: find the chicken near Zoro Tsunami. Completed by the reunion.")]
    [SerializeField] QuestDefinition findQuest;

    [Header("Scene references")]
    [SerializeField] Transform chicken;             // ChienKe_Pet
    [Tooltip("The NPC Zoro Tsunami (NPCs_FromVillageMap/NPC_BacHai/Zoro). The chicken looks at him.")]
    [SerializeField] Transform zoro;
    [Tooltip("Where the chicken stands and watches Zoro (position only; its yaw is computed to face Zoro).")]
    [SerializeField] Transform watchSpot;
    [SerializeField] Transform player;
    [SerializeField] StoryBed bed;
    [Tooltip("The two grandparents. Their dialogue scripts call TryHandleGrandparent(transform).")]
    [SerializeField] Transform grandfather;
    [SerializeField] Transform grandmother;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] MessageBannerUI banner;
    [Tooltip("Optional. Empty = Camera.main.")]
    [SerializeField] Camera viewCamera;

    [Header("Which Zoro is visible: the training Zoro at the start, the Tsunami Zoro once the search quest is accepted")]
    [Tooltip("Hidden at the start and shown when the quest tim_ga_zoro is accepted (the Zoro Tsunami NPC).")]
    [SerializeField] GameObject[] showOnSearch;
    [Tooltip("Shown at the start and hidden when the quest tim_ga_zoro is accepted (the Zoro of the training area).")]
    [SerializeField] GameObject[] hideOnSearch;

    [Header("Finding the chicken")]
    [Tooltip("Within this distance (and with the chicken in view) the 'there it is' banner is shown.")]
    [SerializeField, Min(2f)] float discoverDistance = 22f;
    [Tooltip("The chicken must be within this angle of the camera's forward direction (3D).")]
    [SerializeField, Range(20f, 90f)] float viewHalfAngle = 60f;
    [Tooltip("Closer than this (flat distance) the reunion dialogue opens by itself.")]
    [SerializeField, Min(1f)] float talkDistance = 5.5f;

    [Header("Texts")]
    [SerializeField] string zoroSpeaker = "Zoro";
    [SerializeField, TextArea(1, 3)] string restMessage = "Hạ cả Chị Ba lẫn Chú Tư rồi! Về nhà ông bà nằm ngủ một giấc cho khoẻ.";
    [SerializeField, TextArea(1, 3)] string wakeMessage = "Sáng dậy... con gà lại biến mất rồi! Hỏi ông bà xem sao.";
    [SerializeField] string askButtonLabel = "Đi tìm con gà";
    [SerializeField, TextArea(2, 4)] string[] askGrandfatherLines =
    {
        "[Player] Ông ơi, sáng dậy cháu lại không thấy con gà đâu cả. Ông có thấy nó không ạ?",
        "Nó đi theo thằng tóc xanh lá rồi!",
        "[Player] Ơ, thằng mù đường mà?"
    };
    [SerializeField, TextArea(2, 4)] string[] askGrandmotherLines =
    {
        "[Player] Bà ơi, con gà của cháu lại biến mất rồi, bà có thấy nó đâu không ạ?",
        "Nó đi theo thằng tóc xanh lá rồi!",
        "[Player] Ơ, thằng mù đường mà?"
    };
    [SerializeField, TextArea(2, 4)] string[] restHintLines =
    {
        "Đánh xong hai trận mệt lắm rồi hả cháu? Vào nhà nằm lên giường ngủ một giấc cho lại sức đi."
    };
    [SerializeField, TextArea(2, 4)] string[] remindLines =
    {
        "Con gà đi theo thằng tóc xanh lá đấy. Cháu ra tìm nó đi, chắc nó đứng đâu đó gần thằng đó thôi."
    };
    [SerializeField, TextArea(2, 4)] string[] remindFoundLines =
    {
        "Thấy nó rồi thì lại gần, dắt nó về nhé."
    };
    [SerializeField, TextArea(1, 3)] string searchMessage = "Đã nhận nhiệm vụ: Tìm con gà đang đi theo Zoro tóc xanh lá.";
    [SerializeField, TextArea(1, 3)] string foundHint = "Con gà kia rồi! Nó đứng từ xa nhìn Zoro. Lại gần nó đi.";
    [SerializeField, TextArea(2, 6)] string[] reunionLines =
    {
        "[Player] Con gà đây rồi! Mày đứng nhìn ông tóc xanh lá kia từ nãy đến giờ à?",
        "Hửm? Con gà của chú mày à? Nó lẽo đẽo theo tôi từ tờ mờ sáng đấy.",
        "[Player] Hèn gì... Mà sao ông đứng đây? Khu huấn luyện đâu có xa.",
        "Tôi đâu có lạc! Chỉ là con đường nó cứ tự đổi hướng thôi.",
        "Nhưng con gà này thú vị lắm. Nó đứng xem tôi quay cơn lốc cả buổi mà không chớp mắt.",
        "Cơn lốc đó gọi là Hurricane. Giờ nó nhớ hết rồi. Dắt nó về đi, rồi cho thằng bạn của chú mày thấy.",
        "[Player] Hurricane á? Gà mình học lỏm được tuyệt chiêu rồi sao?"
    };
    [SerializeField, TextArea(1, 3)] string unlockMessage = "Con gà đã học được kỹ năng HURRICANE! Dùng nó trong trận tái đấu cuối cùng.";

    public Stage CurrentStage { get { return stage; } }
    /// <summary>The player may sleep (the tournament is won and the chicken has not left yet). Read by StoryBed.</summary>
    public bool CanSleep { get { return stage == Stage.Resting; } }
    /// <summary>The chicken is away (hidden, or standing near Zoro and not yet taken back).</summary>
    public bool ChickenIsAway { get { return stage >= Stage.ChickenAway && stage < Stage.Recovered; } }
    /// <summary>The reunion happened and Hurricane was unlocked.</summary>
    public bool Completed { get { return stage == Stage.Recovered; } }

    Stage stage = Stage.Waiting;
    bool wakeShown;
    bool chickenHidden;
    bool chickenHeld;           // the pet AI / agent are switched off by this script (never leave them off)
    bool combatWasEnabled;
    bool rootMotionWas;
    bool keepStateWas;
    int reunionFrame;

    PetCombatAI combat;
    NavMeshAgent agent;
    Animator anim;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ZoroReunionStory] A second instance was found - this one is ignored.", this);
            enabled = false;
            return;
        }
        Instance = this;
        ApplyZoroVisibility(false);
    }

    void OnEnable()
    {
        if (Instance == null) Instance = this;
        if (questManager != null) questManager.QuestCompleted += OnQuestCompleted;
    }

    void OnDisable()
    {
        if (questManager != null) questManager.QuestCompleted -= OnQuestCompleted;
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
        if (player == null || questManager == null || tournamentQuest == null || restQuest == null || findQuest == null
            || chicken == null || zoro == null || watchSpot == null || bed == null || dialogue == null)
            Debug.LogError("[ZoroReunionStory] A required reference is missing.", this);
        CacheChickenParts();
    }

    // ---------------------------------------------------------------- step 1: the tournament is done

    void OnQuestCompleted(QuestStatus status)
    {
        if (status.Definition == tournamentQuest && stage == Stage.Waiting) BeginResting();
    }

    void BeginResting()
    {
        stage = Stage.Resting; // flip first: nothing below can run twice
        if (questManager != null && restQuest != null) questManager.AcceptQuest(restQuest);
        if (banner != null && !string.IsNullOrEmpty(restMessage)) banner.Show(restMessage, 6f);
        Debug.Log("[ZoroReunionStory] Tournament won - the player must sleep now.");
    }

    // ---------------------------------------------------------------- step 2: the night

    /// <summary>
    /// Called by StoryBed while its screen is black (the second sleep). Completes the rest quest and the chicken leaves.
    /// Returns false (nothing happens) unless the player is really in the Resting stage, so a stray call can never skip ahead.
    /// </summary>
    public bool OnPlayerSlept()
    {
        if (stage != Stage.Resting) return false;
        stage = Stage.ChickenAway; // flip first
        if (questManager != null && restQuest != null)
        {
            questManager.ReportProgress(restQuest.objectiveKey, 1);
            questManager.TurnIn(restQuest);
        }
        ChickenLeaves();
        return true;
    }

    void ChickenLeaves()
    {
        CacheChickenParts();
        if (chicken == null) return;

        HoldChicken();
        if (anim != null) { keepStateWas = anim.keepAnimatorStateOnDisable; anim.keepAnimatorStateOnDisable = true; }
        chicken.gameObject.SetActive(false);
        chickenHidden = true;
        Debug.Log("[ZoroReunionStory] The chicken left during the night.");
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
            case Stage.Resting:
                dialogue.Open(speaker, restHintLines, null, null);
                return true;
            case Stage.ChickenAway:
                dialogue.Open(speaker, isGrandfather ? askGrandfatherLines : askGrandmotherLines, askButtonLabel, AcceptSearch);
                return true;
            case Stage.Searching:
                dialogue.Open(speaker, remindLines, null, null);
                return true;
            case Stage.Found:
            case Stage.Reunion:
                dialogue.Open(speaker, remindFoundLines, null, null);
                return true;
            default:
                return false;
        }
    }

    /// <summary>The dialogue button: accepts "tim_ga_zoro" and puts the chicken near Zoro. Works once (only from ChickenAway).</summary>
    public void AcceptSearch()
    {
        if (stage != Stage.ChickenAway) return;
        stage = Stage.Searching; // flip first: a second press cannot spawn a second time

        if (questManager != null && findQuest != null) questManager.AcceptQuest(findQuest);
        ApplyZoroVisibility(true); // Tsunami Zoro appears, the training Zoro goes
        PlaceChickenAtWatchSpot();
        if (banner != null && !string.IsNullOrEmpty(searchMessage)) banner.Show(searchMessage, 5f);
    }

    // ---------------------------------------------------------------- step 4: the chicken watches Zoro

    void PlaceChickenAtWatchSpot()
    {
        if (chicken == null || watchSpot == null) return;
        CacheChickenParts();

        Vector3 pos = watchSpot.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(pos, out hit, 3f, NavMesh.AllAreas)) pos = hit.position;

        chicken.position = pos;
        Vector3 look = zoro != null ? zoro.position - pos : watchSpot.forward;
        look.y = 0f;
        chicken.rotation = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.Euler(0f, watchSpot.eulerAngles.y, 0f);

        // still the transformed model; make sure (never two models)
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
            anim.applyRootMotion = false; // stands in place, watching
            anim.SetBool("isRunning", false);
            anim.SetBool("IsPushing", false);
        }
        Debug.Log("[ZoroReunionStory] The chicken stands at " + pos.ToString("F1") + ", watching Zoro.");
    }

    // ---------------------------------------------------------------- steps 5 and 6

    void Update()
    {
        switch (stage)
        {
            case Stage.ChickenAway: UpdateWake(); break;
            case Stage.Searching:
            case Stage.Found: UpdateApproach(); break;
            case Stage.Reunion: UpdateReunion(); break;
        }
    }

    void UpdateWake()
    {
        if (wakeShown) return;
        if (bed != null && bed.IsBusy) return; // the sleep sequence (dialogue, video, wake line) is still running
        wakeShown = true;
        if (banner != null && !string.IsNullOrEmpty(wakeMessage)) banner.Show(wakeMessage, 6f);
    }

    bool UiBlocked()
    {
        if (dialogue != null && dialogue.IsOpen) return true;
        if (bed != null && bed.IsSleeping) return true;
        if (InventorySystem.Instance != null && InventorySystem.Instance.isOpen) return true;
        BattleManager bm = BattleManager.Instance;
        return bm != null && bm.IsBattleActive;
    }

    void UpdateApproach()
    {
        if (player == null || chicken == null || UiBlocked()) return;
        float d = Flat(player.position, chicken.position);

        if (stage == Stage.Searching && d <= discoverDistance && CanSeeChicken())
        {
            stage = Stage.Found; // flip first: the hint is shown only once
            if (banner != null && !string.IsNullOrEmpty(foundHint)) banner.Show(foundHint, 5f);
        }

        if (d <= talkDistance) BeginReunion();
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

    void BeginReunion()
    {
        if (stage != Stage.Searching && stage != Stage.Found) return;
        if (dialogue == null || dialogue.IsOpen) return;
        stage = Stage.Reunion; // flip first: the dialogue is opened only once
        reunionFrame = Time.frameCount;
        dialogue.Open(zoroSpeaker, reunionLines, null, null);
    }

    void UpdateReunion()
    {
        // the recovery happens when the dialogue is closed (read to the end, or closed early): it can never hang the story
        if (dialogue == null || Time.frameCount <= reunionFrame || dialogue.IsOpen) return;
        CompleteReunion();
    }

    /// <summary>Public so a test can drive the same path. Only from the Reunion stage, only once.</summary>
    public bool CompleteReunion()
    {
        if (stage != Stage.Reunion) return false;
        stage = Stage.Recovered; // flip first: a second call cannot unlock twice
        ReleaseChicken();

        if (questManager != null && findQuest != null && questManager.GetState(findQuest.questId) == QuestState.Active)
        {
            questManager.ReportProgress(findQuest.objectiveKey, 1);
            questManager.TurnIn(findQuest);
        }

        if (HurricaneSkill.Instance != null) HurricaneSkill.Instance.Unlock();
        else Debug.LogWarning("[ZoroReunionStory] No HurricaneSkill in the scene - the skill could not be unlocked.", this);

        if (HurricaneFinisher.Instance != null && chicken != null) HurricaneFinisher.Instance.PlayLearnEffect(chicken.position + Vector3.up * 0.6f);
        if (banner != null && !string.IsNullOrEmpty(unlockMessage)) banner.Show(unlockMessage, 7f);
        return true;
    }

    /// <summary>false = start state (training Zoro visible, Tsunami Zoro hidden). true = the search quest was accepted (the other way round).</summary>
    void ApplyZoroVisibility(bool searchStarted)
    {
        if (showOnSearch != null)
            for (int i = 0; i < showOnSearch.Length; i++) if (showOnSearch[i] != null) showOnSearch[i].SetActive(searchStarted);
        if (hideOnSearch != null)
            for (int i = 0; i < hideOnSearch.Length; i++) if (hideOnSearch[i] != null) hideOnSearch[i].SetActive(!searchStarted);
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

    /// <summary>Gives the chicken back to the existing AI: normal root motion, plain Follow.</summary>
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
            combat.ResumeAfterRecovery(); // clean Follow state
        }
    }

    static float Flat(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void OnDrawGizmosSelected()
    {
        if (watchSpot == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(watchSpot.position, talkDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(watchSpot.position, discoverDistance);
        if (zoro != null) Gizmos.DrawLine(watchSpot.position, zoro.position);
    }
}
