using UnityEngine;

/// <summary>
/// Sparkle (twinkling stars) on the scrap items of the scrap quest, so the player can spot them.
///
/// Rules:
///  - Only the pickups under "pickupsRoot" (the same root ScrapCollector uses = the quest items). Other pickups
///    (ItemPickup, the inventory Pickup_VeChai) are not under it and are never touched.
///  - A pickup sparkles only while the quest is Active AND the pickup is not collected. It stops for good when the
///    quest is ReadyToTurnIn / Completed (no more scrap is needed) or before it was accepted.
///  - Each sparkle lasts "sparkleSeconds" (2 s), then it is off for "pauseSeconds", and it repeats. Items are spread
///    over the cycle ("stagger") so they do not all blink in sync.
///  - Collecting an item switches its GameObject off (ScrapPickup.MarkCollected), which stops its particles at once.
///  - No Particle System is created at runtime: every pickup already owns a "SparkleFX" child (prefab instance) placed in
///    the scene; this script only calls Play / Stop when the wanted state CHANGES (no per-frame allocation or spawning).
/// It never changes quest acceptance, the required amount, collection or the reward.
/// </summary>
public class ScrapSparkleController : MonoBehaviour
{
    [Header("What sparkles")]
    [Tooltip("Same root as ScrapCollector.pickupsRoot (ScrapItems). Only quest scrap lives here.")]
    [SerializeField] Transform pickupsRoot;
    [SerializeField] QuestDefinition quest;   // scrap_collector
    [SerializeField] QuestManager questManager;
    [Tooltip("Name of the particle child on each pickup.")]
    [SerializeField] string fxChildName = "SparkleFX";

    [Header("Timing")]
    [SerializeField, Min(0.2f)] float sparkleSeconds = 2f;
    [SerializeField, Min(0f)] float pauseSeconds = 3f;
    [Tooltip("0 = every item blinks together, 1 = start times are spread over the whole cycle.")]
    [SerializeField, Range(0f, 1f)] float stagger = 1f;

    ScrapPickup[] pickups;
    ParticleSystem[] fx;
    bool[] playing;

    public int ItemCount { get { return pickups != null ? pickups.Length : 0; } }
    public float CycleSeconds { get { return sparkleSeconds + pauseSeconds; } }

    void Start()
    {
        if (pickupsRoot == null || quest == null || questManager == null)
        {
            Debug.LogError("[ScrapSparkle] pickupsRoot / quest / questManager is not assigned.", this);
            enabled = false;
            return;
        }

        pickups = pickupsRoot.GetComponentsInChildren<ScrapPickup>(true);
        fx = new ParticleSystem[pickups.Length];
        playing = new bool[pickups.Length];
        int missing = 0;
        for (int i = 0; i < pickups.Length; i++)
        {
            Transform c = pickups[i].transform.Find(fxChildName);
            if (c != null) fx[i] = c.GetComponent<ParticleSystem>();
            if (fx[i] == null) missing++;
        }
        if (missing > 0) Debug.LogWarning("[ScrapSparkle] " + missing + " pickup(s) have no " + fxChildName + " child - they will not sparkle.", this);
    }

    void Update()
    {
        if (pickups == null) return;

        // sparkle only while scrap is still needed
        bool questNeedsScrap = questManager.GetState(quest.questId) == QuestState.Active;
        float now = Time.time;

        for (int i = 0; i < pickups.Length; i++)
        {
            ParticleSystem ps = fx[i];
            if (ps == null) continue;

            ScrapPickup p = pickups[i];
            bool available = questNeedsScrap && p != null && p.enabled && !p.IsCollected && p.gameObject.activeInHierarchy;
            bool want = available && IsSparklePhase(i, now);

            if (want == playing[i]) continue;
            playing[i] = want;

            if (want) ps.Play(true);
            else if (available) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);          // end of this 2 s burst: let the last stars fade
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);                 // collected / quest over: stop at once
        }
    }

    /// <summary>True while item "index" is inside its sparkle window at "time" (pure function, also used by tests).</summary>
    public bool IsSparklePhase(int index, float time)
    {
        float cycle = CycleSeconds;
        float offset = pickups != null && pickups.Length > 0 ? (index / (float)pickups.Length) * cycle * stagger : 0f;
        return ((time + offset) % cycle) < sparkleSeconds;
    }

    /// <summary>Is the particle system of item "index" emitting right now (for tests).</summary>
    public bool IsEmitting(int index)
    {
        return fx != null && index >= 0 && index < fx.Length && fx[index] != null && fx[index].isEmitting;
    }

    public ScrapPickup GetPickup(int index) { return pickups[index]; }
    public ParticleSystem GetFx(int index) { return fx[index]; }

    void OnDisable()
    {
        if (fx == null) return;
        for (int i = 0; i < fx.Length; i++)
            if (fx[i] != null && playing[i]) { fx[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); playing[i] = false; }
    }
}
