using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ONE place for the light particle effects: chicken-fight hits, the punching-bag spark and the water drops.
/// Everything is a one-shot burst through ParticleSystem.Emit at a world position:
///   - particles live 0.15-0.7 s and die by themselves, nothing loops or emits by rate,
///   - maxParticles caps every system and every effect has a minimum interval, so nothing is emitted every frame,
///   - no gameplay value is read or changed: fight hits come from HealthSystem.OnHealthChanged (listen only, the same
///     event and the same call that GameAudio listens to, so the flash and the sound happen on the same frame).
/// Chicken-fight hit = a big bright flash + a burst of embers + a burst of stars (additive, HDR material so the scene's
/// Bloom picks it up). It is pushed towards the camera so the body does not hide it, and it grows with distance so it
/// stays readable from the gameplay camera. The punching bag and the water keep their own small effects.
/// </summary>
public class GameVfx : MonoBehaviour
{
    [SerializeField] Material particleMaterial;   // soft dot, alpha blended: punching-bag spark and water
    [Tooltip("Minimum seconds between two LIGHT fight-hit bursts (same value as GameAudio.hitLight.minInterval).")]
    [SerializeField, Min(0.01f)] float hitInterval = 0.12f;
    [Tooltip("Minimum seconds between two HEAVY fight-hit bursts (same value as GameAudio.hitHeavy.minInterval). A heavy hit is never swallowed by a light one.")]
    [SerializeField, Min(0.01f)] float heavyHitInterval = 0.2f;
    [SerializeField, Min(0.01f)] float bagInterval = 0.3f;
    [SerializeField, Min(0.01f)] float waterInterval = 0.25f;

    [Header("Chicken fight hit (big, bright, additive)")]
    [Tooltip("Additive HDR soft-dot material: flash + embers.")]
    [SerializeField] Material hitGlowMaterial;
    [Tooltip("Additive HDR star material: the star burst.")]
    [SerializeField] Material hitStarMaterial;
    [Tooltip("Overall size multiplier of the fight hit (1 = the designed size).")]
    [SerializeField, Min(0.1f)] float hitSize = 1f;
    [Tooltip("Metres the burst is moved towards the camera so it is not buried inside the chicken's body.")]
    [SerializeField, Min(0f)] float pushTowardCamera = 1.5f;
    [Tooltip("Beyond this camera distance the effect grows (distance / this value), up to Far Boost Max.")]
    [SerializeField, Min(1f)] float farBoostDistance = 12f;
    [SerializeField, Min(1f)] float farBoostMax = 2.2f;

    const int GlowMax = 60;     // flash + embers alive at once
    const int StarMax = 160;    // stars alive at once

    public static GameVfx Instance { get; private set; }

    ParticleSystem hitSpark, hitGlow, hitStars, water;
    float lastHit = -99f, lastHeavyHit = -99f, lastBag = -99f, lastWater = -99f;
    readonly Dictionary<HealthSystem, float> health = new Dictionary<HealthSystem, float>();

    /// <summary>Diagnostics for tests.</summary>
    public int HitCount { get; private set; }
    public int HeavyHitCount { get; private set; }
    public int WaterCount { get; private set; }
    public int BagCount { get; private set; }
    public int Blocked { get; private set; }
    /// <summary>Time.frameCount of the last fight-hit burst (compare with GameAudio.LastHitSfxFrame).</summary>
    public int LastHitFrame { get; private set; }
    public bool LastHitHeavy { get; private set; }
    public Vector3 LastHitPosition { get; private set; }
    public float LastHitScale { get; private set; }
    public int AliveParticles { get { return Count(hitSpark) + Count(hitGlow) + Count(hitStars) + Count(water); } }
    public int AliveHitParticles { get { return Count(hitGlow) + Count(hitStars); } }

    static int Count(ParticleSystem ps) { return ps != null ? ps.particleCount : 0; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        hitSpark = Build("HitSpark", new Color(1f, 0.92f, 0.55f, 1f), 0.10f, 0.18f, 2.5f, 4.5f, 0.25f, 0.45f, 0.6f, 40);   // punching bag
        water = Build("WaterDrops", new Color(0.55f, 0.8f, 1f, 1f), 0.07f, 0.13f, 0.8f, 2.4f, 0.55f, 0.9f, 1.6f, 60);
        hitGlow = BuildCombat("HitGlow", hitGlowMaterial != null ? hitGlowMaterial : particleMaterial, GlowMax, 0.5f);
        hitStars = BuildCombat("HitStars", hitStarMaterial != null ? hitStarMaterial : particleMaterial, StarMax, 1.2f);
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

    /// <summary>Combat system: size, lifetime, speed and colour are given per particle at Emit time.</summary>
    ParticleSystem BuildCombat(string name, Material material, int maxParticles, float gravity)
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
        main.startLifetime = 0.5f;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.startColor = Color.white;
        main.gravityModifier = gravity;
        main.maxParticles = maxParticles;      // hard cap: extra Emit() calls are ignored by Unity
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule em = ps.emission; em.enabled = false;
        ParticleSystem.ShapeModule shape = ps.shape; shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime; col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        // pop in fast, then shrink away
        AnimationCurve curve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f));
        ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, curve);

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.maxParticleSize = 1f;               // allow a big flash to fill more than half of the screen height
        r.sortingFudge = -10f;                // draw in front of other transparent objects
        if (material != null) r.sharedMaterial = material;
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
        if (Time.unscaledTime - (heavy ? lastHeavyHit : lastHit) < (heavy ? heavyHitInterval : hitInterval)) { Blocked++; return; }
        if (heavy) lastHeavyHit = Time.unscaledTime; else lastHit = Time.unscaledTime;
        HitCount++;
        if (heavy) HeavyHitCount++;
        LastHitFrame = Time.frameCount;
        LastHitHeavy = heavy;

        // in front of the body, and bigger when the camera is far away
        Vector3 at = position;
        float boost = 1f;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 to = cam.transform.position - position;
            float d = to.magnitude;
            if (d > 0.01f) at = position + to / d * Mathf.Min(pushTowardCamera, d * 0.4f);
            boost = Mathf.Clamp(d / farBoostDistance, 1f, farBoostMax);
        }
        float s = hitSize * boost;
        LastHitPosition = at;
        LastHitScale = s;

        if (hitGlow != null)
        {
            // 1 flash (the thing the eye sees first) + embers
            EmitOne(hitGlow, at, Vector3.zero, (heavy ? 4.4f : 2.6f) * s, heavy ? 0.26f : 0.18f,
                    heavy ? new Color(1f, 0.82f, 0.45f, 1f) : new Color(1f, 0.95f, 0.7f, 1f), Random.Range(0f, 360f));
            int embers = heavy ? 10 : 4;
            for (int i = 0; i < embers; i++)
            {
                float sp = Random.Range(heavy ? 7f : 5f, heavy ? 13f : 8f) * s;
                EmitOne(hitGlow, at, RandomDir() * sp, Random.Range(0.28f, 0.45f) * s, Random.Range(0.35f, heavy ? 0.7f : 0.5f),
                        heavy ? new Color(1f, 0.5f, 0.15f, 1f) : new Color(1f, 0.8f, 0.3f, 1f), 0f);
            }
        }
        if (hitStars != null)
        {
            int stars = heavy ? 18 : 9;
            for (int i = 0; i < stars; i++)
            {
                float sp = Random.Range(heavy ? 8f : 5f, heavy ? 15f : 9f) * s;
                float size = (heavy ? Random.Range(0.8f, 1.5f) : Random.Range(0.5f, 0.9f)) * s;
                float life = heavy ? Random.Range(0.45f, 0.7f) : Random.Range(0.35f, 0.5f);
                Color c = heavy ? Color.Lerp(new Color(1f, 0.55f, 0.2f, 1f), new Color(1f, 0.9f, 0.5f, 1f), Random.value)
                                : Color.Lerp(new Color(1f, 0.85f, 0.4f, 1f), Color.white, Random.value);
                EmitOne(hitStars, at, RandomDir() * sp, size, life, c, Random.Range(0f, 360f));
            }
        }
    }

    static Vector3 RandomDir()
    {
        Vector3 d = Random.onUnitSphere;
        d.y = Mathf.Abs(d.y) * 0.7f + 0.2f;      // mostly up and out, not into the ground
        return d.normalized;
    }

    static void EmitOne(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color color, float rotation)
    {
        ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
        p.position = pos;
        p.velocity = vel;
        p.startSize = size;
        p.startLifetime = life;
        p.startColor = color;
        p.rotation = rotation;
        ps.Emit(p, 1);
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
