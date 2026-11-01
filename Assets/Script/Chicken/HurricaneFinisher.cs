using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// The finishing move of the final rematch: the chicken's Hurricane Kick.
///
/// Trigger (once per final fight, never anywhere else): HurricaneSkill is unlocked, FinalBossController says its fight is active,
/// BattleManager's battle is active and the opponent's health fell to 'triggerHealthFraction' (50%) or less.
///
/// What it does (a short cinematic, nothing else in the project is rewritten):
///   1. Both fighters stop (their combat scripts are paused and their damage is held at 0 for the few seconds, so nobody can die by
///      accident and the battle result cannot be decided by anything but the finisher). The chicken faces the opponent.
///   2. The chicken first runs out to take a run-up, then spins in with the real Hurricane Kick animation (PetCombatAI.TryPlayHurricane, hurricaneSeconds long = 4 s; the 1.83 s clip is played twice, slightly slowed) and touches the opponent ONLY when it is over, while the green Tornado effect (tornadoPrefab) builds around it - it does NOT turn to look at the opponent, the clip's own root-motion spin plays as authored -,
///      the camera gets a FOV punch + shake and "HURRICANE KICK!" flashes on the screen.
///   3. At the moment of the kick the opponent takes the killing blow through HealthSystem.TakeDamage - the very same call that
///      any normal win uses - so HealthSystem.Die -> OnDeath -> BattleManager.HandleEnemyChickenDeath -> PlayerWon ->
///      FinalBossController (OnVictory, quest, victory dialogue, ending) all run unchanged. The body is then thrown far away
///      along an arc (the opponent's colliders and agent are already off from Die, so nothing blocks the flight).
///   4. Everything that was paused is restored. The opponent's damage is NOT restored here when it is dead: FinalBossController
///      restores its own saved stats when it handles the result, and writing the boss value back would undo that.
/// If anything goes wrong before the kick (chicken dead, battle ended) the cinematic stops and restores what it paused.
/// Visual effects are runtime particle bursts (no new assets); the same wind is reused for the "skill learned" moment
/// (PlayLearnEffect, called by ZoroReunionStory).
/// </summary>
public class HurricaneFinisher : MonoBehaviour
{
    public static HurricaneFinisher Instance { get; private set; }

    [Header("Scene references (all optional: empty = the singletons)")]
    [SerializeField] FinalBossController finalBoss;
    [SerializeField] MessageBannerUI banner;
    [Tooltip("Soft additive material for the wind streaks (the GameVfx hit-glow material works well). Empty = Sprites/Default.")]
    [SerializeField] Material windMaterial;

    [Header("Trigger")]
    [SerializeField, Range(0.05f, 0.95f)] float triggerHealthFraction = 0.5f;

    [Header("Timing")]
    [Tooltip("Seconds the Hurricane Kick lasts: the chicken spins in towards the opponent and touches it only when this time is over (owner request: 4 s). The 1.83 s clip is played a whole number of times, slightly slowed, to fill it.")]
    [SerializeField, Min(1f)] float hurricaneSeconds = 4f;
    [Tooltip("Before the kick the chicken runs this far away from the opponent to take a run-up (metres).")]
    [SerializeField, Min(2f)] float runUpDistance = 8f;
    [SerializeField, Min(2f)] float runUpSpeed = 9f;
    [SerializeField, Min(0.3f)] float flightSeconds = 1.7f;
    [Tooltip("Seconds the chicken keeps its kick pose / wind after the launch, before the AI is given back.")]
    [SerializeField, Min(0f)] float followThroughSeconds = 0.6f;
    [Tooltip("Safety: the cinematic gives everything back after this many seconds no matter what.")]
    [SerializeField, Min(3f)] float maxSeconds = 14f;

    [Header("Launch")]
    [SerializeField, Min(5f)] float flightDistance = 34f;
    [SerializeField, Min(1f)] float flightHeight = 10f;
    [SerializeField] float spinTurns = 3f;
    [SerializeField] float tumbleTurns = 1.5f;
    [Tooltip("The chicken is placed this far from the opponent for the kick (metres).")]
    [SerializeField, Min(0.8f)] float kickDistance = 1.6f;

    [Header("Wind / screen")]
    [SerializeField, Min(1f)] float windRadius = 1.1f;
    [SerializeField, Min(10f)] float windRatePerSecond = 260f;
    [SerializeField] Color windColor = new Color(0.75f, 0.95f, 1f, 0.9f);

    [Header("Tornado effect (replaces the old wind spiral while the chicken spins)")]
    [Tooltip("The Tornado VFX prefab (Assets/Prefabs/Tornado/Tornado.prefab). It is spawned when the spin starts, follows the chicken during the whole spin and fades out at the moment of the kick. Empty = the old wind spiral is used instead.")]
    [SerializeField] GameObject tornadoPrefab;
    [Tooltip("Tornado size relative to the chicken: the prefab is scaled by (chicken world scale x this value). The Tornado prefab is about 6.5 m tall at scale 1, so 0.18 makes it ~1.5x as tall as a chicken of scale 3.")]
    [SerializeField, Min(0.01f)] float tornadoScalePerPetScale = 0.18f;
    [Tooltip("Seconds the tornado takes to grow to full size when the spin starts (0 = instantly).")]
    [SerializeField, Min(0f)] float tornadoGrowSeconds = 0.5f;
    [Tooltip("Offset of the tornado from the chicken's position (the prefab's base is at its pivot, so 0 puts it on the ground under the chicken).")]
    [SerializeField] Vector3 tornadoOffset = Vector3.zero;
    [SerializeField] float cameraShake = 0.12f;
    [SerializeField] float cameraFovKick = 14f;
    [SerializeField] string skillText = "HURRICANE KICK!";

    /// <summary>True while the cinematic runs.</summary>
    public bool Playing { get; private set; }
    /// <summary>How many times the finisher ran in this play session (one per final fight; diagnostics for tests).</summary>
    public int PlayCount { get; private set; }
    /// <summary>Set after the opponent was launched in the current/last run.</summary>
    public bool LaunchedOpponent { get; private set; }
    /// <summary>Where the opponent landed in the last run.</summary>
    public Vector3 LastLandingPoint { get; private set; }

    bool triggeredThisFight;
    ParticleSystem wind;
    GameObject tornado;          // the live Tornado VFX instance (null when none)
    Vector3 tornadoBaseScale;    // its full-size scale
    Coroutine punchRoutine;
    GameObject overlay;
    CanvasGroup overlayGroup;
    Image flash;
    TextMeshProUGUI label;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[HurricaneFinisher] A second instance was found - this one is ignored.", this);
            enabled = false;
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnDisable()
    {
        if (overlay != null) { Destroy(overlay); overlay = null; }
        StopTornado();
    }

    // ---------------------------------------------------------------- trigger

    void Update()
    {
        FinalBossController boss = finalBoss != null ? finalBoss : FinalBossController.Instance;
        BattleManager bm = BattleManager.Instance;
        bool fight = boss != null && boss.FightActive && bm != null && bm.IsBattleActive;
        if (!fight)
        {
            if (!Playing) triggeredThisFight = false; // the next final fight may play it again (only if this one never reached it)
            return;
        }
        if (triggeredThisFight || Playing || !HurricaneSkill.Unlocked) return;

        HealthSystem foe = bm.enemyHealth;
        HealthSystem pet = bm.mainChickenHealth;
        if (foe == null || pet == null || foe.IsDead || pet.IsDead) return;
        if (bm.mainChickenCombat == null || bm.enemyCombat == null) return;
        if (foe.currentHealth > foe.maxHealth * triggerHealthFraction) return;

        triggeredThisFight = true; // flip first: it can never start twice in the same fight
        StartCoroutine(FinisherRoutine(bm, pet, foe));
    }

    // ---------------------------------------------------------------- the cinematic

    IEnumerator FinisherRoutine(BattleManager bm, HealthSystem pet, HealthSystem foe)
    {
        Playing = true;
        LaunchedOpponent = false;
        PlayCount++;
        float started = Time.time;

        PetCombatAI petAI = bm.mainChickenCombat;
        RoosterCombatAI foeAI = bm.enemyCombat;
        NavMeshAgent petAgent = pet.GetComponent<NavMeshAgent>();
        NavMeshAgent foeAgent = foe.GetComponent<NavMeshAgent>();
        Animator petAnim = pet.animator != null ? pet.animator : pet.GetComponent<Animator>();
        Animator foeAnim = foe.animator != null ? foe.animator : foe.GetComponent<Animator>();

        // ---- pause both fighters (restored in the finally block)
        bool petAIWas = petAI.enabled;
        bool foeAIWas = foeAI.enabled;
        float petDamageWas = petAI.damage;
        float foeDamageWas = foeAI.damage;
        bool petRootMotionWas = petAnim != null && petAnim.applyRootMotion;
        float petSpeedWas = petAnim != null ? petAnim.speed : 1f;
        bool foeRootMotionWas = foeAnim != null && foeAnim.applyRootMotion;
        bool petUpdateRotationWas = petAgent != null && petAgent.updateRotation;

        try
        {
            petAI.damage = 0f;   // the spin's hit events must not change the result: only the finishing blow below does
            foeAI.damage = 0f;   // and the opponent cannot hurt the chicken while it is paused
            petAI.enabled = false;
            foeAI.enabled = false;
            if (petAgent != null && petAgent.enabled && petAgent.isOnNavMesh) { petAgent.ResetPath(); petAgent.isStopped = true; }
            if (foeAgent != null && foeAgent.enabled && foeAgent.isOnNavMesh) { foeAgent.ResetPath(); foeAgent.isStopped = true; }
            if (petAnim != null) petAnim.SetBool("isRunning", false);
            if (foeAnim != null) foeAnim.SetBool("isRunning", false);

            // ---- 0. run out to take a run-up: the chicken backs away from the opponent at running speed
            Transform pt = pet.transform;
            Vector3 foePos = foe.transform.position;
            Vector3 away = pt.position - foePos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -foe.transform.forward;
            away.Normalize();
            if (petAnim != null) petAnim.applyRootMotion = false; // from now on the chicken is moved by this script only
            Vector3 flatNow = new Vector3(pt.position.x - foePos.x, 0f, pt.position.z - foePos.z);
            if (flatNow.magnitude < runUpDistance - 0.5f)
            {
                Vector3 runUpPoint = foePos + away * runUpDistance;
                NavMeshHit rh;
                if (NavMesh.SamplePosition(runUpPoint, out rh, 3f, NavMesh.AllAreas)) runUpPoint = rh.position;
                yield return RunTo(pt, petAgent, petAnim, runUpPoint, bm, pet, foe, started);
                if (petAnim != null) petAnim.SetBool("isRunning", false);
                if (!StillOn(bm, pet, foe) || Time.time - started > maxSeconds) yield break;
            }

            // turn round to face the opponent
            Vector3 charge = foePos - pt.position;
            charge.y = 0f;
            Vector3 chargeDir = charge.sqrMagnitude > 0.01f ? charge.normalized : -away;
            Vector3 contactPoint = foePos - chargeDir * kickDistance;
            Quaternion faceRot = Quaternion.LookRotation(chargeDir);
            for (float turn = 0f; turn < 0.25f; turn += Time.deltaTime)
            {
                pt.rotation = Quaternion.Slerp(pt.rotation, faceRot, 0.35f);
                yield return null;
            }
            pt.rotation = faceRot;

            // ---- 1. the Hurricane Kick for hurricaneSeconds: the chicken spins in and only touches the opponent when it is over
            float clipLen = HurricaneClipLength(petAnim);
            int plays = Mathf.Max(1, Mathf.RoundToInt(hurricaneSeconds / clipLen));
            if (petAnim != null) petAnim.speed = plays * clipLen / hurricaneSeconds; // a whole number of full clips fills the time exactly
            // The Hurricane Kick's spin lives in the clip's ROOT MOTION (RootQ: ~3 turns per play; RootT: ~3.8 m forward). With Apply Root Motion off
            // Unity throws it away and the chicken would not turn at all. So it is ON for the spin: the animation turns the chicken exactly as authored and
            // the chicken is NOT made to look at the opponent. Only the PATH is ours: the position is set to an absolute point on the line towards
            // the opponent every frame (MovePet below), which also cancels the clip's own forward drift.
            if (petAnim != null) petAnim.applyRootMotion = true;
            // The agent must not turn the chicken towards its movement either: the PATH points at the opponent, the chicken's rotation is the clip's own spin.
            if (petAgent != null) petAgent.updateRotation = false;
            float replayEvery = hurricaneSeconds / plays;
            float nextReplay = replayEvery - 0.1f; // re-trigger just before the clip ends so there is no idle flash in between
            if (!petAI.TryPlayHurricane())
            {
                Debug.LogWarning("[HurricaneFinisher] The Hurricane Kick could not be played - the finisher is skipped.", this);
                yield break;
            }
            Debug.Log("[HurricaneFinisher] Hurricane! opponent HP " + foe.currentHealth + "/" + foe.maxHealth);
            BuildOverlay();
            StartPunch(hurricaneSeconds, cameraShake * 0.4f, cameraFovKick);

            Vector3 chargeFrom = pt.position;
            SpawnTornado(pt);
            float t = 0f;
            float acc = 0f;
            while (t < hurricaneSeconds)
            {
                if (!StillOn(bm, pet, foe) || Time.time - started > maxSeconds) yield break; // aborted: nothing was killed, the finally block restores
                float dt = Time.deltaTime;
                t += dt;
                float k = Mathf.Clamp01(t / hurricaneSeconds);
                MovePet(pt, petAgent, Vector3.Lerp(chargeFrom, contactPoint, k * k)); // accelerates: it spins up, then slams in at the end
                Vector3 toPet = pt.position - foe.transform.position;
                toPet.y = 0f;
                if (toPet.sqrMagnitude > 0.01f) foe.transform.rotation = Quaternion.LookRotation(toPet);
                if (t >= nextReplay && t < hurricaneSeconds - 0.3f) { petAI.TryPlayHurricane(); nextReplay += replayEvery; }

                acc += dt * windRatePerSecond;
                int n = Mathf.FloorToInt(acc);
                acc -= n;
                if (tornado != null)
                {
                    tornado.transform.position = pt.position + tornadoOffset;
                    if (tornadoGrowSeconds > 0f) tornado.transform.localScale = tornadoBaseScale * Mathf.SmoothStep(0.15f, 1f, t / tornadoGrowSeconds);
                }
                else EmitSpiral(pt.position + Vector3.up * 0.2f, n, Mathf.Lerp(0.5f, 1f, k)); // no tornado prefab assigned: the old wind
                UpdateOverlay(k, 0f);
                yield return null;
            }
            MovePet(pt, petAgent, contactPoint); // contact: exactly now, after the whole 4 s
            if (petAnim != null) petAnim.applyRootMotion = false; // the spin is over: from here the chicken is moved by this script only again
            StopTornado();

            // ---- 2. the blow: the opponent takes the killing damage through the normal health path, then flies
            if (!StillOn(bm, pet, foe)) yield break;

            Vector3 foeBefore = foe.transform.position;
            Vector3 dir = foeBefore - pet.transform.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : pet.transform.forward;

            Vector3 impactAt = Center(foe);
            EmitShockwave(pet.transform.position + Vector3.up * 0.3f);
            StartPunch(0.5f, cameraShake, cameraFovKick * 0.6f);
            UpdateOverlay(1f, 1f);

            if (foeAnim != null) foeAnim.applyRootMotion = false; // the thrown body is moved by this script only
            foe.TakeDamage(foe.currentHealth + 1f);               // HealthSystem.Die -> BattleManager (PlayerWon) -> FinalBossController
            LaunchedOpponent = true;

            Vector3 land = FindLanding(foeBefore, dir);
            LastLandingPoint = land;
            yield return Fly(foe.transform, foeBefore, land, dir);

            if (foe != null) foe.transform.position = land;
            if (foe != null) foe.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameVfx.Hit(land + Vector3.up * 0.4f, true);
            EmitShockwave(land + Vector3.up * 0.2f);

            // ---- 3. follow-through
            float tail = 0f;
            while (tail < followThroughSeconds && Time.time - started < maxSeconds)
            {
                tail += Time.deltaTime;
                float rest = 1f - tail / Mathf.Max(0.01f, followThroughSeconds);
                UpdateOverlay(1f, Mathf.Clamp01(rest * 2f - 1f), rest);
                yield return null;
            }
        }
        finally
        {
            // give everything back, whatever happened
            if (petAI != null) { petAI.damage = petDamageWas; petAI.enabled = petAIWas; }
            if (foeAI != null)
            {
                // a dead opponent's stats belong to FinalBossController (it restores its saved damage when it handles the result);
                // only an opponent that is still alive gets its own value back here
                if (foe != null && !foe.IsDead) foeAI.damage = foeDamageWas;
                foeAI.enabled = foeAIWas;
            }
            if (petAnim != null) { petAnim.applyRootMotion = petRootMotionWas; petAnim.speed = petSpeedWas; }
            if (petAgent != null) petAgent.updateRotation = petUpdateRotationWas;
            if (foeAnim != null) foeAnim.applyRootMotion = foeRootMotionWas;
            if (petAgent != null && petAgent.enabled && petAgent.isOnNavMesh && !pet.IsDead) petAgent.isStopped = false;
            if (overlay != null) { Destroy(overlay); overlay = null; }
            StopTornado();
            Playing = false;
        }
    }

    /// <summary>Spawns the Tornado VFX on the chicken (scaled to its size). Does nothing when no tornadoPrefab is assigned.</summary>
    void SpawnTornado(Transform follow)
    {
        StopTornado();
        if (tornadoPrefab == null || follow == null) return;
        float s = Mathf.Max(0.05f, follow.lossyScale.y * tornadoScalePerPetScale);
        tornado = Instantiate(tornadoPrefab, follow.position + tornadoOffset, Quaternion.identity);
        tornado.name = "HurricaneTornado";
        tornadoBaseScale = tornadoPrefab.transform.localScale * s;
        tornado.transform.localScale = tornadoGrowSeconds > 0f ? tornadoBaseScale * 0.15f : tornadoBaseScale;
    }

    /// <summary>Lets the Tornado VFX fade out and removes it (safe to call any time, also when there is none).</summary>
    void StopTornado()
    {
        if (tornado == null) return;
        GameObject go = tornado;
        tornado = null;
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            // the swirl meshes have a lifetime of hours (one big mesh particle): they must be cleared, the soft particles just stop and fade
            bool mesh = r != null && r.renderMode == ParticleSystemRenderMode.Mesh;
            ps.Stop(false, mesh ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        }
        Destroy(go, 2.5f);
    }

    /// <summary>Moves the chicken by a flat offset (through the agent so it stays on the NavMesh; the agent's own path following is stopped).</summary>
    static void MovePet(Transform body, NavMeshAgent agent, Vector3 target)
    {
        Vector3 delta = target - body.position;
        delta.y = 0f;
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(delta);
        else body.position += delta;
    }

    /// <summary>The run-up: runs to 'point' at runUpSpeed with the normal run animation, facing where it goes.</summary>
    IEnumerator RunTo(Transform body, NavMeshAgent agent, Animator anim, Vector3 point, BattleManager bm, HealthSystem pet, HealthSystem foe, float started)
    {
        if (anim != null) anim.SetBool("isRunning", true);
        float limit = 4f; // safety
        while (limit > 0f)
        {
            if (!StillOn(bm, pet, foe) || Time.time - started > maxSeconds) break;
            Vector3 to = point - body.position;
            to.y = 0f;
            float d = to.magnitude;
            if (d <= 0.2f) break;
            Vector3 dirn = to / d;
            MovePet(body, agent, body.position + dirn * Mathf.Min(d, runUpSpeed * Time.deltaTime));
            body.rotation = Quaternion.Slerp(body.rotation, Quaternion.LookRotation(dirn), 15f * Time.deltaTime);
            limit -= Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>Length of the Hurricane Kick clip in the chicken's controller (1.83 s in this project).</summary>
    static float HurricaneClipLength(Animator a)
    {
        if (a != null && a.runtimeAnimatorController != null)
            foreach (AnimationClip c in a.runtimeAnimatorController.animationClips)
                if (c != null && c.name.ToLower().Contains("hurricane")) return c.length;
        return 1.83f;
    }

    bool StillOn(BattleManager bm, HealthSystem pet, HealthSystem foe)
    {
        return bm != null && bm.IsBattleActive && pet != null && !pet.IsDead && foe != null && !foe.IsDead;
    }

    static Vector3 Center(HealthSystem h)
    {
        Collider c = h.GetComponentInChildren<Collider>();
        return c != null ? c.bounds.center : h.transform.position + Vector3.up * 0.5f;
    }

    /// <summary>The chicken stands 'kickDistance' from the opponent and looks at it (the Hurricane Kick needs to be close).</summary>
    void FaceAndPlace(HealthSystem pet, HealthSystem foe, NavMeshAgent petAgent)
    {
        Vector3 foePos = foe.transform.position;
        Vector3 away = pet.transform.position - foePos;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -foe.transform.forward;
        float d = away.magnitude;
        if (d > kickDistance * 1.6f || d < kickDistance * 0.5f)
        {
            Vector3 spot = foePos + away.normalized * kickDistance;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(spot, out hit, 2f, NavMesh.AllAreas)) spot = hit.position;
            if (petAgent != null && petAgent.enabled) petAgent.Warp(spot);
            else pet.transform.position = spot;
        }
        Vector3 look = foePos - pet.transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) pet.transform.rotation = Quaternion.LookRotation(look);
        Vector3 back = pet.transform.position - foePos;
        back.y = 0f;
        if (back.sqrMagnitude > 0.01f) foe.transform.rotation = Quaternion.LookRotation(-back);
    }

    // ---------------------------------------------------------------- the throw

    /// <summary>A landing point 'flightDistance' away (shortened until it is on the ground / the walkable area).</summary>
    Vector3 FindLanding(Vector3 from, Vector3 dir)
    {
        float[] factors = { 1f, 0.75f, 0.55f, 0.4f, 0.28f };
        for (int i = 0; i < factors.Length; i++)
        {
            Vector3 target = from + dir * (flightDistance * factors[i]);
            NavMeshHit nh;
            if (NavMesh.SamplePosition(target, out nh, 6f, NavMesh.AllAreas)) return nh.position;
            RaycastHit gh;
            if (Physics.Raycast(target + Vector3.up * 60f, Vector3.down, out gh, 120f, ~0, QueryTriggerInteraction.Ignore)
                && gh.point.y < from.y + 6f && gh.point.y > from.y - 6f)
                return gh.point;
        }
        return from + dir * 6f; // nothing found: a short throw on the same level
    }

    IEnumerator Fly(Transform body, Vector3 from, Vector3 to, Vector3 dir)
    {
        float t = 0f;
        Quaternion baseRot = body.rotation;
        float acc = 0f;
        while (t < flightSeconds && body != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / flightSeconds);
            float ease = 1f - (1f - k) * (1f - k);                     // fast at the start, slower at the end
            Vector3 pos = Vector3.Lerp(from, to, ease);
            pos.y += flightHeight * 4f * k * (1f - k);                 // arc
            body.position = pos;
            body.rotation = baseRot * Quaternion.Euler(k * tumbleTurns * 360f, k * spinTurns * 360f, 0f);

            acc += Time.deltaTime * windRatePerSecond * 0.6f;
            int n = Mathf.FloorToInt(acc);
            acc -= n;
            EmitTrail(pos + Vector3.up * 0.4f, -dir, n);
            yield return null;
        }
    }

    // ---------------------------------------------------------------- "skill learned" effect

    /// <summary>A short wind spiral around a point (used when the chicken learns Hurricane). Safe to call at any time.</summary>
    public void PlayLearnEffect(Vector3 at)
    {
        StartCoroutine(LearnRoutine(at));
    }

    IEnumerator LearnRoutine(Vector3 at)
    {
        float t = 0f, acc = 0f, duration = 1.3f;
        while (t < duration)
        {
            float dt = Time.deltaTime;
            t += dt;
            acc += dt * windRatePerSecond * 0.7f;
            int n = Mathf.FloorToInt(acc);
            acc -= n;
            EmitSpiral(at, n, 0.6f + 0.6f * (t / duration));
            yield return null;
        }
        EmitShockwave(at);
    }

    // ---------------------------------------------------------------- particles (runtime, no assets)

    void EnsureWind()
    {
        if (wind != null) return;
        GameObject go = new GameObject("HurricaneWind");
        go.transform.SetParent(transform, false);
        wind = go.AddComponent<ParticleSystem>();
        wind.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = wind.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.5f;
        main.startSpeed = 0f;
        main.startSize = 0.3f;
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.maxParticles = 1500;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule em = wind.emission; em.enabled = false;   // only explicit Emit() calls
        ParticleSystem.ShapeModule shape = wind.shape; shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule col = wind.colorOverLifetime; col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystem.SizeOverLifetimeModule sz = wind.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;   // streaks along the velocity = wind lines
        r.lengthScale = 5f;
        r.velocityScale = 0.15f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.maxParticleSize = 1f;
        r.sortingFudge = -10f;
        Material m = windMaterial;
        if (m == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s != null) m = new Material(s);
        }
        if (m != null) r.sharedMaterial = m;
    }

    /// <summary>Streaks circling a point (upwards spiral). 'strength' 0..1 scales speed and size.</summary>
    void EmitSpiral(Vector3 center, int count, float strength)
    {
        EnsureWind();
        for (int i = 0; i < count; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float rad = windRadius * Random.Range(0.5f, 1.15f);
            Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            p.position = center + radial * rad + Vector3.up * Random.Range(0f, 1.6f);
            p.velocity = tangent * Random.Range(9f, 15f) * strength + Vector3.up * Random.Range(1.5f, 4f) - radial * 0.8f;
            p.startSize = Random.Range(0.12f, 0.3f) * (0.6f + strength);
            p.startLifetime = Random.Range(0.35f, 0.6f);
            p.startColor = Color.Lerp(windColor, Color.white, Random.value * 0.5f);
            wind.Emit(p, 1);
        }
    }

    /// <summary>A flat ring of streaks flying outwards (the shockwave of the kick / the landing).</summary>
    void EmitShockwave(Vector3 center)
    {
        EnsureWind();
        const int n = 90;
        for (int i = 0; i < n; i++)
        {
            float ang = i * Mathf.PI * 2f / n + Random.Range(-0.05f, 0.05f);
            Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            p.position = center + radial * 0.5f;
            p.velocity = radial * Random.Range(12f, 20f) + Vector3.up * Random.Range(0f, 1.5f);
            p.startSize = Random.Range(0.2f, 0.45f);
            p.startLifetime = Random.Range(0.3f, 0.55f);
            p.startColor = windColor;
            wind.Emit(p, 1);
        }
    }

    /// <summary>Wind lines streaming behind the thrown body.</summary>
    void EmitTrail(Vector3 at, Vector3 backwards, int count)
    {
        EnsureWind();
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            p.position = at + Random.insideUnitSphere * 0.5f;
            p.velocity = backwards * Random.Range(8f, 16f) + Random.insideUnitSphere * 1.5f;
            p.startSize = Random.Range(0.15f, 0.35f);
            p.startLifetime = Random.Range(0.3f, 0.5f);
            p.startColor = windColor;
            wind.Emit(p, 1);
        }
    }

    // ---------------------------------------------------------------- camera + screen

    void StartPunch(float seconds, float shake, float fovKick)
    {
        if (punchRoutine != null) StopCoroutine(punchRoutine); // its finally block restores the camera first
        punchRoutine = StartCoroutine(CameraPunch(seconds, shake, fovKick));
    }

    IEnumerator CameraPunch(float duration, float amount, float fovKick)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;
        Vector3 basePos = cam.transform.localPosition;
        float baseFov = cam.fieldOfView;
        float t = 0f;
        try
        {
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / duration);
                cam.transform.localPosition = basePos + Random.insideUnitSphere * amount * k;
                cam.fieldOfView = baseFov + fovKick * k;
                yield return null;
            }
        }
        finally
        {
            if (cam != null)
            {
                cam.transform.localPosition = basePos;
                cam.fieldOfView = baseFov;
            }
        }
    }

    void BuildOverlay()
    {
        if (overlay != null) Destroy(overlay);
        overlay = new GameObject("HurricaneOverlayCanvas");
        Canvas canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 550;
        overlayGroup = overlay.AddComponent<CanvasGroup>();
        overlayGroup.blocksRaycasts = false;
        overlayGroup.interactable = false;

        GameObject flashGO = new GameObject("Flash");
        flashGO.transform.SetParent(overlay.transform, false);
        flash = flashGO.AddComponent<Image>();
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.raycastTarget = false;
        Stretch(flash.rectTransform);

        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(overlay.transform, false);
        label = textGO.AddComponent<TextMeshProUGUI>();
        label.text = skillText;
        label.fontSize = 110;
        label.fontStyle = FontStyles.Bold | FontStyles.Italic;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.8f, 0.97f, 1f, 0f);
        label.raycastTarget = false;
        RectTransform rt = label.rectTransform;
        rt.anchorMin = new Vector2(0f, 0.62f);
        rt.anchorMax = new Vector2(1f, 0.85f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>'build' 0..1 grows the title, 'impact' 0..1 is the white flash (1 at the blow, then fades).</summary>
void UpdateOverlay(float build, float impact, float fade = 1f)
    {
        if (overlay == null) return;
        if (label != null)
        {
            float a = Mathf.Clamp01(build * 3f) * Mathf.Clamp01(fade);
            label.color = new Color(0.8f, 0.97f, 1f, a);
            label.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.15f, build);
        }
        if (flash != null) flash.color = new Color(1f, 1f, 1f, 0.55f * impact * impact);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
