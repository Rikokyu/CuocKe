using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ONE place for the light particle effects: hit sparks (chicken fights, punching bag) and water drops (watering can).
/// Three ParticleSystems are built here (one per effect), all one-shot bursts via ParticleSystem.Emit at a world position:
///   - particles live 0.2-0.9 s and die by themselves, nothing is looping or emitting by rate,
///   - maxParticles caps each system, and every effect has a minimum interval, so nothing is emitted every frame,
///   - no gameplay value is read or changed: fight hits come from HealthSystem.OnHealthChanged (listen only), the watering
///     and punching-bag effects are one null-safe call from QuestObjectiveTarget / TrainingStation.
/// The material is assigned in the scene (a URP particle material with a generated soft dot texture).
/// </summary>
public class GameVfx : MonoBehaviour
{
    [SerializeField] Material particleMaterial;
    [SerializeField, Min(0.01f)] float hitInterval = 0.06f;
    [SerializeField, Min(0.01f)] float bagInterval = 0.3f;
    [SerializeField, Min(0.01f)] float waterInterval = 0.25f;

    public static GameVfx Instance { get; private set; }

    ParticleSystem hitSpark, hitHeavy, water;
    float lastHit = -99f, lastBag = -99f, lastWater = -99f;
    readonly Dictionary<HealthSystem, float> health = new Dictionary<HealthSystem, float>();

    /// <summary>Diagnostics for tests.</summary>
    public int HitCount { get; private set; }
    public int WaterCount { get; private set; }
    public int BagCount { get; private set; }
    public int Blocked { get; private set; }
    public int AliveParticles { get { return Count(hitSpark) + Count(hitHeavy) + Count(water); } }

    static int Count(ParticleSystem ps) { return ps != null ? ps.particleCount : 0; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        hitSpark = Build("HitSpark", new Color(1f, 0.92f, 0.55f, 1f), 0.10f, 0.18f, 2.5f, 4.5f, 0.25f, 0.45f, 0.6f, 40);
        hitHeavy = Build("HitHeavy", new Color(1f, 0.6f, 0.2f, 1f), 0.14f, 0.26f, 3f, 6f, 0.3f, 0.55f, 0.6f, 40);
        water = Build("WaterDrops", new Color(0.55f, 0.8f, 1f, 1f), 0.07f, 0.13f, 0.8f, 2.4f, 0.55f, 0.9f, 1.6f, 60);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        foreach (HealthSystem h in FindObjectsByType<HealthSystem>(FindObjectsInactive.Include))
        {
            if (health.ContainsKey(h)) continue;
            health[h] = h.currentHealth;
            HealthSystem captured = h;
            h.OnHealthChanged.AddListener((cur, max) => OnHealthChanged(captured, cur, max));
        }
    }

    ParticleSystem Build(string name, Color color, float sizeMin, float sizeMax, float speedMin, float speedMax, float lifeMin, float lifeMax, float gravity, int maxParticles)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.maxParticles = maxParticles;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule em = ps.emission; em.enabled = false;           // only explicit Emit() calls
        ParticleSystem.ShapeModule shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.05f;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime; col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (particleMaterial != null) r.sharedMaterial = particleMaterial;
        return ps;
    }

    // ------------------------------------------------------------ public API (all null-safe through the statics)
    public static void Hit(Vector3 position, bool heavy)
    {
        if (Instance != null) Instance.EmitHit(position, heavy);
    }

    /// <summary>Punching bag strike (own, slower interval).</summary>
    public static void BagHit(Vector3 position)
    {
        if (Instance != null) Instance.EmitBag(position);
    }

    public static void Water(Vector3 position)
    {
        if (Instance != null) Instance.EmitWater(position);
    }

    void EmitHit(Vector3 position, bool heavy)
    {
        if (Time.unscaledTime - lastHit < hitInterval) { Blocked++; return; }
        lastHit = Time.unscaledTime;
        HitCount++;
        Emit(heavy ? hitHeavy : hitSpark, position, heavy ? 14 : 8);
    }

    void EmitBag(Vector3 position)
    {
        if (Time.unscaledTime - lastBag < bagInterval) { Blocked++; return; }
        lastBag = Time.unscaledTime;
        BagCount++;
        Emit(hitSpark, position, 10);
    }

    void EmitWater(Vector3 position)
    {
        if (Time.unscaledTime - lastWater < waterInterval) { Blocked++; return; }
        lastWater = Time.unscaledTime;
        WaterCount++;
        Emit(water, position, 16);
    }

    static void Emit(ParticleSystem ps, Vector3 position, int count)
    {
        if (ps == null) return;
        ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
        p.position = position;
        p.applyShapeToPosition = true;
        ps.Emit(p, count);
    }

    // ------------------------------------------------------------ fights: listen to health changes only
    void OnHealthChanged(HealthSystem h, float current, float max)
    {
        float before;
        if (!health.TryGetValue(h, out before)) before = current;
        health[h] = current;
        float lost = before - current;
        if (lost <= 0.01f) return;

        Vector3 at = h.transform.position + Vector3.up * 0.5f;
        Collider c = h.GetComponentInChildren<Collider>();
        if (c != null) at = c.bounds.center;
        bool heavy = lost >= 0.15f * max || lost >= 25f;
        Hit(at, heavy);
    }
}
