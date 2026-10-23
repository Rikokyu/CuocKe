using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Step 3 of the garden quest ("garden_protein"): the player puts the harvested vegetables on a plate at ONE spot, the chicken
/// walks there and eats, and only when it has really finished eating does the step count.
/// Reuses: QuestManager (progress), InventorySystem (the vegetables are an ItemDefinition: Has / Remove), InteractionPromptUI,
/// MessageBannerUI, and the chicken's NavMeshAgent / PetCombatAI (switched off only while it eats, then restored).
///
/// Rules: only while the quest is Active with progress == minProgress (steps 1 and 2 done); needs the vegetables in the bag;
/// the chicken must be within maxChickenDistance (it follows the player, so normally it is); placing flips IsPlaced BEFORE the
/// vegetables are removed, so a second press cannot place twice; the progress is reported exactly once, after the meal.
/// </summary>
public class FeedingSpot : MonoBehaviour
{
    [Header("Quest")]
    [SerializeField] QuestDefinition quest;
    [SerializeField] QuestManager questManager;
    [Tooltip("Steps that must be done before this one (garden: fertilise + harvest = 2).")]
    [SerializeField, Min(0)] int minProgress = 2;

    [Header("Item (existing inventory)")]
    [SerializeField] ItemDefinition vegetable;

    [Header("Scene")]
    [SerializeField] GameObject plate;      // hidden until the vegetables are placed
    [SerializeField] Transform eatPoint;    // where the chicken stands to eat
    [SerializeField] Transform chicken;     // ChienKe_Pet
    [SerializeField] Transform player;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] MessageBannerUI banner;

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] float interactionDistance = 2f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string promptText = "Nhấn E để đặt rau cho gà ăn";
    [SerializeField] string needVegetableText = "Cần rau đã hái (hái ở luống rau)";

    [Header("Eating")]
    [SerializeField, Min(1f)] float maxChickenDistance = 15f;
    [SerializeField, Min(0.1f)] float arriveDistance = 1.2f;
    [SerializeField, Min(1f)] float walkTimeout = 12f;
    [SerializeField, Min(0.5f)] float eatSeconds = 3f;
    [SerializeField] string chickenFarMessage = "Gà ở xa quá. Đợi gà lại gần rồi đặt rau nhé.";
    [SerializeField] string placedMessage = "Đã đặt rau. Gà đang chạy tới...";
    [SerializeField] string ateMessage = "Gà ăn sạch rau rồi! Trông nó hơi lạ lạ...";

    bool promptShown;
    string shownText;
    Coroutine routine;
    PetCombatAI combat;
    bool combatWasEnabled;
    NavMeshAgent agent;
    float oldStoppingDistance;

    /// <summary>The vegetables are on the plate (set the moment they are placed).</summary>
    public bool IsPlaced { get; private set; }
    /// <summary>The chicken finished eating and the step was reported.</summary>
    public bool HasEaten { get; private set; }

    void Start()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null || quest == null || questManager == null || vegetable == null || chicken == null || prompt == null)
            Debug.LogError("[FeedingSpot] A required reference is missing.", this);
        if (plate != null) plate.SetActive(false);
    }

    void Update()
    {
        if (player == null || questManager == null || quest == null) return;

        bool stepPending = StepPending();
        bool inRange = InRange() && (dialogue == null || !dialogue.IsOpen) && !IsInventoryOpen();
        bool hasVeg = HasVegetable();

        SetPrompt(inRange && stepPending, hasVeg ? promptText : needVegetableText);
        if (inRange && stepPending && hasVeg && Input.GetKeyDown(interactKey)) TryPlace();
    }

    bool StepPending()
    {
        if (IsPlaced) return false;
        QuestStatus s = questManager.GetStatus(quest);
        return s.State == QuestState.Active && s.Progress >= minProgress;
    }

    bool HasVegetable()
    {
        InventorySystem bag = InventorySystem.Instance;
        return bag != null && vegetable != null && bag.Has(vegetable, 1);
    }

    /// <summary>E action. Public so a test can drive the same path. Returns true only if the vegetables were placed.</summary>
    public bool TryPlace()
    {
        if (!StepPending() || !HasVegetable()) return false;

        Vector3 d = chicken.position - transform.position;
        d.y = 0f;
        if (d.magnitude > maxChickenDistance)
        {
            if (banner != null) banner.Show(chickenFarMessage, 3f);
            return false;
        }

        IsPlaced = true; // flip first: a second press (same frame or later) can never place twice
        InventorySystem.Instance.Remove(vegetable, 1);
        if (plate != null) plate.SetActive(true);
        SetPrompt(false, null);
        if (banner != null) banner.Show(placedMessage, 3f);

        routine = StartCoroutine(EatRoutine());
        return true;
    }

    IEnumerator EatRoutine()
    {
        agent = chicken.GetComponent<NavMeshAgent>();
        combat = chicken.GetComponent<PetCombatAI>();
        combatWasEnabled = combat != null && combat.enabled;
        if (combat != null) combat.enabled = false; // the pet AI would keep walking back to the player

        Vector3 target = eatPoint != null ? eatPoint.position : transform.position;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            oldStoppingDistance = agent.stoppingDistance;
            agent.stoppingDistance = 0.3f;
            agent.isStopped = false;
        }

        float t = 0f;
        while (t < walkTimeout && Flat(chicken.position, target) > arriveDistance)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.SetDestination(target);
            t += Time.deltaTime;
            yield return null;
        }
        if (Flat(chicken.position, target) > arriveDistance && agent != null && agent.enabled && agent.isOnNavMesh)
            agent.Warp(target); // failsafe: the meal can never hang

        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        Vector3 look = transform.position - chicken.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) chicken.rotation = Quaternion.LookRotation(look);

        yield return new WaitForSeconds(eatSeconds);

        FinishMeal();
    }

    void FinishMeal()
    {
        if (plate != null) plate.SetActive(false);
        RestoreChicken();
        routine = null;
        if (HasEaten) return;
        HasEaten = true;

        if (banner != null) banner.Show(ateMessage, 4f);
        questManager.ReportProgress(quest.objectiveKey, 1); // the confirmation: only now does step 3 count
    }

    void RestoreChicken()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.stoppingDistance = oldStoppingDistance;
            agent.isStopped = false;
        }
        if (combat != null && combatWasEnabled)
        {
            combat.enabled = true;
            combat.ResumeAfterRecovery(); // back to plain Follow
        }
    }

    static float Flat(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    bool InRange()
    {
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= interactionDistance * interactionDistance;
    }

    static bool IsInventoryOpen()
    {
        return InventorySystem.Instance != null && InventorySystem.Instance.isOpen;
    }

    void SetPrompt(bool visible, string text)
    {
        if (prompt == null) return;
        if (!visible)
        {
            if (!promptShown) return;
            promptShown = false; shownText = null;
            prompt.Hide(this);
            return;
        }
        if (promptShown && shownText == text) return;
        promptShown = true; shownText = text;
        prompt.Show(this, text);
    }

    void OnDisable()
    {
        SetPrompt(false, null);
        if (routine != null) { StopCoroutine(routine); routine = null; RestoreChicken(); } // never leave the pet AI off
    }
}
