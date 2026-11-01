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
    [Tooltip("Steps that must be done before placing the vegetables (garden: harvest = 1).")]
    [SerializeField, Min(0)] int minProgress = 1;
    [Tooltip("The chicken starts eating once the quest has this much progress (garden: after the bran jar = 3). 0 = eat right after placing.")]
    [SerializeField, Min(0)] int mealStartsAtProgress = 3;

    [Header("Item (existing inventory)")]
    [SerializeField] ItemDefinition vegetable;

    [Header("Scene")]
    [Tooltip("Parent of all the food visuals (the 'Plate' object). With the three objects below assigned it STAYS active: the three states are made by switching its children.")]
    [SerializeField] GameObject plate;
    [Header("Food visuals (optional; all three together replace the old 'plate on / off')")]
    [Tooltip("State 0 and 1: the bare plate (the Sprinkle_Cake child that holds only the Plate mesh). Hidden in state 2 because the finished cake brings its own plate.")]
    [SerializeField] GameObject emptyPlate;
    [Tooltip("State 1: the vegetable leaves on the plate (Leaf0, Leaf1, Leaf2).")]
    [SerializeField] GameObject[] leaves;
    [Tooltip("State 2: the complete Sprinkle_Cake (cake + its own plate + sprinkles) the chicken eats.")]
    [SerializeField] GameObject finishedCake;
    [SerializeField] Transform eatPoint;    // where the chicken stands to eat
    [SerializeField] Transform chicken;     // ChienKe_Pet
    [SerializeField] Transform player;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] MessageBannerUI banner;

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] float interactionDistance = 3f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string promptText = "Nhấn E để đặt rau cho gà ăn";
    [SerializeField] string needVegetableText = "Cần rau đã hái (hái ở luống rau)";
    [Tooltip("Shown at the plate after the bran / protein jar was used: the Player must press E on the plate to mix the powder in. Only that press turns the plate into the finished cake and sends the chicken to eat.")]
    [SerializeField] string mixPromptText = "Nhấn E để trộn bột vào rau cho gà ăn";

    [Header("Eating")]
    [SerializeField, Min(1f)] float maxChickenDistance = 15f;
    [SerializeField, Min(0.1f)] float arriveDistance = 1.2f;
    [SerializeField, Min(1f)] float walkTimeout = 12f;
    [SerializeField, Min(0.5f)] float eatSeconds = 3f;
    [SerializeField] string chickenFarMessage = "Gà ở xa quá. Đợi gà lại gần rồi đặt rau nhé.";
    [SerializeField] string chickenFarMixMessage = "Gà ở xa quá. Đợi gà lại gần rồi trộn bột cho gà ăn nhé.";
    [SerializeField] string placedMessage = "Đã đặt rau lên đĩa. Giờ lấy thêm bột cám trong hũ để trộn vào nhé.";
    [SerializeField] string ateMessage = "Gà ăn sạch rau rồi! Trông nó hơi lạ lạ...";

    bool promptShown;
    string shownText;
    int mealPendingFrame = -1; // frame in which the plate started waiting for the mixing press (that frame's E belongs to the jar)
    Coroutine routine;
    PetCombatAI combat;
    bool combatWasEnabled;
    NavMeshAgent agent;
    float oldStoppingDistance;

    /// <summary>The vegetables are on the plate (set the moment they are placed).</summary>
    public bool IsPlaced { get; private set; }
    /// <summary>The chicken finished eating and the step was reported.</summary>
    public bool HasEaten { get; private set; }
    /// <summary>The chicken was sent to the plate (after the bran step).</summary>
    public bool MealStarted { get; private set; }

    void Start()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null || quest == null || questManager == null || vegetable == null || chicken == null || prompt == null)
            Debug.LogError("[FeedingSpot] A required reference is missing.", this);
        SetFoodVisual(FoodState.EmptyPlate);
    }

    // ---------------------------------------------------------------- the three looks of the food
    enum FoodState { EmptyPlate, PlateWithLeaves, FinishedCake }

    bool HasFoodVisuals { get { return emptyPlate != null && finishedCake != null; } }

    /// <summary>Exactly one look at a time (never two plates on top of each other). Idempotent: calling it again changes nothing.</summary>
    void SetFoodVisual(FoodState state)
    {
        if (!HasFoodVisuals)
        {
            // old scenes without the three references: the plate is hidden until the vegetables are placed
            if (plate != null) plate.SetActive(state != FoodState.EmptyPlate);
            return;
        }
        if (plate != null && !plate.activeSelf) plate.SetActive(true); // the parent must stay on: it holds all the looks
        emptyPlate.SetActive(state != FoodState.FinishedCake);
        if (leaves != null)
            for (int i = 0; i < leaves.Length; i++)
                if (leaves[i] != null) leaves[i].SetActive(state == FoodState.PlateWithLeaves);
        finishedCake.SetActive(state == FoodState.FinishedCake);
    }

void Update()
    {
        if (player == null || questManager == null || quest == null) return;

        bool inRange = InRange() && (dialogue == null || !dialogue.IsOpen) && !IsInventoryOpen() && InteractObject.IsAimed(this);

        // After the jar (bran / protein) the powder is only in the bag: the Player has to mix it into the plate himself.
        // The meal never starts by itself - only this E press turns the plate into the finished cake and sends the chicken to eat.
        if (MealPending)
        {
            if (mealPendingFrame < 0) mealPendingFrame = Time.frameCount; // the E press that used the jar must not also count here
            SetPrompt(inRange, mixPromptText);
            if (inRange && Time.frameCount > mealPendingFrame && Input.GetKeyDown(interactKey)) TryStartMeal();
            return;
        }
        mealPendingFrame = -1;

        bool stepPending = StepPending();
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
        SetFoodVisual(FoodState.PlateWithLeaves);
        SetPrompt(false, null);
        if (banner != null) banner.Show(placedMessage, 3f);

        questManager.ReportProgress(quest.objectiveKey, 1); // step: the vegetables are on the plate
        if (mealStartsAtProgress <= 0) StartMeal();
        return true;
    }

/// <summary>True from the moment the jar was used (quest progress reached 'mealStartsAtProgress') until the Player presses E on the plate.</summary>
    public bool MealPending
    {
        get
        {
            if (!IsPlaced || MealStarted || mealStartsAtProgress <= 0 || questManager == null || quest == null) return false;
            QuestStatus s = questManager.GetStatus(quest);
            return s.State == QuestState.Active && s.Progress >= mealStartsAtProgress;
        }
    }

    /// <summary>E on the plate after the jar. Public so a test can drive the same path. Returns true only if the meal started (plate -> cake, chicken goes to eat).</summary>
    public bool TryStartMeal()
    {
        if (!MealPending) return false;

        Vector3 d = chicken.position - transform.position;
        d.y = 0f;
        if (d.magnitude > maxChickenDistance)
        {
            if (banner != null) banner.Show(chickenFarMixMessage, 3f);
            return false;
        }

        SetPrompt(false, null);
        StartMeal(); // flips MealStarted first, so a second press can never start it twice
        return true;
    }


    void StartMeal()
    {
        if (MealStarted) return; // once
        MealStarted = true;
        SetFoodVisual(FoodState.FinishedCake); // the bran / protein is in: the finished cake replaces the plate and the leaves
        routine = StartCoroutine(EatRoutine());
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
        SetFoodVisual(FoodState.EmptyPlate); // eaten: back to the bare plate (old scenes: plate hidden)
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
