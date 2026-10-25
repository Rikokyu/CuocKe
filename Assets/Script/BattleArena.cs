using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The one place where NPC fights happen (the village arena). Existing fight controllers (tournament, rematch, final boss)
/// keep their own flow; they only ask this component whether player and chicken have arrived, show its ground marker
/// while guiding, and let it bring the opponent rooster into the arena (and send it home afterwards).
/// The first fight with the friend uses RequestStart. No combat code lives here.
/// </summary>
public class BattleArena : MonoBehaviour
{
    public static BattleArena Instance { get; private set; }

    [Header("Zone (XZ circle, inside the arena fence)")]
    [SerializeField] Vector3 center = new Vector3(28.5f, 0f, 19.7f);
    [SerializeField, Min(2f)] float radius = 8.5f;
    [Tooltip("Where an opponent rooster that lives outside the arena is put, relative to the center.")]
    [SerializeField] Vector3 foeOffset = new Vector3(3f, 0f, 0f);
    [SerializeField, Min(1f)] float returnDelay = 8f;

    [Header("Guidance")]
    [SerializeField, Min(1f)] float guidanceReminderSeconds = 6f;
    [SerializeField] string firstFightGuidance = "Hãy dẫn gà đến đấu trường để bắt đầu trận đấu!";
    [SerializeField] Material markerMaterial;

    class AwayFoe
    {
        public HealthSystem health;
        public RoosterCombatAI combat;
        public Vector3 home;
        public Quaternion homeRotation;
        public float returnAt = -1f;
    }

    readonly List<AwayFoe> awayFoes = new List<AwayFoe>();
    MessageBannerUI banner;
    Transform player;
    LineRenderer ring;
    float markerUntil;
    System.Action pendingStart;
    float nextGuidanceAt;

    public Vector3 Center { get { return center; } }
    public float Radius { get { return radius; } }
    public bool IsPending { get { return pendingStart != null; } }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public bool Contains(Vector3 p)
    {
        Vector3 d = p - center;
        d.y = 0f;
        return d.sqrMagnitude <= radius * radius;
    }

    /// <summary>Both the chicken and the player stand inside the arena.</summary>
    public bool Arrived(Vector3 chickenPos, Vector3 playerPos)
    {
        return Contains(chickenPos) && Contains(playerPos);
    }

    /// <summary>Show the ground ring for a few seconds (called whenever a controller shows its guidance banner).</summary>
    public void ShowMarker() { markerUntil = Time.time + guidanceReminderSeconds + 1f; }

    /// <summary>
    /// Put the opponent into the arena (only if it is outside) and make it treat the arena as home, so its idle AI does
    /// not walk back to the village in the middle of the fight. Call after the opponent was healed/revived.
    /// </summary>
    public void BringFoe(HealthSystem foe)
    {
        if (foe == null) return;
        AwayFoe rec = awayFoes.Find(a => a.health == foe);
        if (rec != null) rec.returnAt = -1f; // a new fight is coming: cancel the walk home
        if (Contains(foe.transform.position)) return;

        RoosterCombatAI rc = foe.GetComponent<RoosterCombatAI>();
        if (rec == null)
        {
            rec = new AwayFoe { health = foe, combat = rc, home = rc != null ? rc.HomePoint : foe.transform.position, homeRotation = foe.transform.rotation };
            awayFoes.Add(rec);
        }
        Vector3 spot = center + foeOffset;
        Place(foe, spot, Quaternion.LookRotation(Flat(center - spot, Vector3.left)));
        if (rc != null) rc.SetHomePoint(foe.transform.position);
    }

    /// <summary>Guide the player to the arena and run <paramref name="begin"/> once chicken and player are inside.</summary>
    public bool RequestStart(System.Action begin, string message)
    {
        if (begin == null) return false;
        BattleManager bm = BattleManager.Instance;
        FindRefs();
        if (bm != null && bm.mainChickenHealth != null && player != null && Arrived(bm.mainChickenHealth.transform.position, player.position))
        {
            pendingStart = null;
            begin();
            return true;
        }
        pendingStart = begin;
        if (!string.IsNullOrEmpty(message)) firstFightGuidance = message;
        nextGuidanceAt = 0f;
        return false;
    }

    public void CancelPending() { pendingStart = null; }

    void Update()
    {
        UpdateMarker();
        UpdatePending();
        UpdateReturns();
    }

    void UpdatePending()
    {
        if (pendingStart == null) return;
        BattleManager bm = BattleManager.Instance;
        FindRefs();
        if (bm == null || bm.IsBattleActive || bm.mainChickenHealth == null || bm.mainChickenHealth.IsDead || player == null)
        {
            pendingStart = null; // something changed under us: cancel, nothing starts
            return;
        }
        if (Arrived(bm.mainChickenHealth.transform.position, player.position))
        {
            System.Action go = pendingStart;
            pendingStart = null;
            go();
            return;
        }
        if (Time.time >= nextGuidanceAt)
        {
            nextGuidanceAt = Time.time + guidanceReminderSeconds;
            ShowMarker();
            if (banner != null && !string.IsNullOrEmpty(firstFightGuidance)) banner.Show(firstFightGuidance, 4f);
        }
    }

    void UpdateReturns()
    {
        if (awayFoes.Count == 0) return;
        BattleManager bm = BattleManager.Instance;
        bool busy = bm != null && bm.IsBattleActive;
        for (int i = awayFoes.Count - 1; i >= 0; i--)
        {
            AwayFoe a = awayFoes[i];
            if (a.health == null) { awayFoes.RemoveAt(i); continue; }
            if (busy) { a.returnAt = -1f; continue; }
            if (a.returnAt < 0f) { a.returnAt = Time.time + returnDelay; continue; }
            if (Time.time < a.returnAt) continue;
            Place(a.health, a.home, a.homeRotation);
            if (a.combat != null) a.combat.SetHomePoint(a.health.transform.position);
            awayFoes.RemoveAt(i);
        }
    }

    void Place(HealthSystem who, Vector3 spot, Quaternion rot)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(spot, out hit, 3f, NavMesh.AllAreas)) spot = hit.position;
        NavMeshAgent agent = who.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(spot);
        else who.transform.position = spot;
        who.transform.rotation = rot;
    }

    static Vector3 Flat(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.01f ? v : fallback;
    }

    void FindRefs()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (banner == null) banner = FindFirstObjectByType<MessageBannerUI>();
    }

    void UpdateMarker()
    {
        bool show = Time.time < markerUntil;
        if (show && ring == null) BuildRing();
        if (ring != null && ring.gameObject.activeSelf != show) ring.gameObject.SetActive(show);
    }

    void BuildRing()
    {
        GameObject go = new GameObject("ArenaMarkerRing");
        go.transform.SetParent(transform, false);
        ring = go.AddComponent<LineRenderer>();
        ring.loop = true;
        ring.useWorldSpace = true;
        ring.widthMultiplier = 0.35f;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        if (markerMaterial != null) ring.sharedMaterial = markerMaterial;
        const int n = 72;
        ring.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            ring.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * radius, 0.25f, center.z + Mathf.Sin(a) * radius));
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, radius);
    }
}
