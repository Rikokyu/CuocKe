using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Lets a chicken idle and wander, but ONLY inside a circle (radius) around the position it had when the scene started.
/// It never walks outside that circle: every step is clamped, and a new target is always picked inside it.
///
/// Works with or without a NavMeshAgent:
///  - no agent: the transform is moved directly (flat, Y is kept), the chicken turns toward its target first.
///  - agent on the same object: targets are snapped to the NavMesh and the agent does the walking.
///
/// It only feeds the Animator (walk bool, optional gesture trigger), so it is safe to add to any character whose
/// controller has an "isWalking" bool. It is NOT attached to the player's pet (ChienKe_Pet), which must keep following the player.
/// </summary>
[DisallowMultipleComponent]
public class ChickenPatrol : MonoBehaviour
{
    [Header("Area (never left)")]
    [Tooltip("Max distance from the starting position, in metres.")]
    public float radius = 1.5f;

    [Header("Movement")]
    public float walkSpeed = 0.6f;
    public float turnSpeed = 180f;
    public float arriveDistance = 0.1f;

    [Header("Timing (seconds)")]
    public Vector2 idleTime = new Vector2(2.5f, 6f);

    [Header("Animation")]
    public Animator animator;
    public string walkBool = "isWalking";
    [Tooltip("Optional trigger fired now and then while idle (character gesture). Leave empty for none.")]
    public string gestureTrigger = "";
    public Vector2 gestureInterval = new Vector2(8f, 16f);

    public Vector3 Home { get { return home; } }
    public bool IsWalking { get { return walking; } }

    Vector3 home;
    Vector3 target;
    bool walking;
    float stateEnd;
    float nextGesture;
    bool hasWalkParam;
    bool hasGestureParam;
    NavMeshAgent agent;

    void Awake()
    {
        home = transform.position;
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        CacheParameters();
    }

    void OnEnable()
    {
        BeginIdle();
        ScheduleGesture();
    }

    void CacheParameters()
    {
        hasWalkParam = hasGestureParam = false;
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.name == walkBool) hasWalkParam = true;
            if (p.type == AnimatorControllerParameterType.Trigger && !string.IsNullOrEmpty(gestureTrigger) && p.name == gestureTrigger) hasGestureParam = true;
        }
    }

    void Update()
    {
        if (walking) TickWalk(); else TickIdle();
    }

    void TickIdle()
    {
        if (Time.time >= stateEnd) { BeginWalk(); return; }
        if (hasGestureParam && Time.time >= nextGesture)
        {
            animator.SetTrigger(gestureTrigger);
            ScheduleGesture();
            stateEnd = Mathf.Max(stateEnd, Time.time + 3f); // let the gesture finish before walking off
        }
    }

    void BeginIdle()
    {
        walking = false;
        SetWalkAnim(false);
        if (UsingAgent() && agent.isOnNavMesh) agent.isStopped = true;
        stateEnd = Time.time + Random.Range(idleTime.x, idleTime.y);
    }

    void BeginWalk()
    {
        Vector2 c = Random.insideUnitCircle * (radius * 0.9f);
        // avoid tiny shuffles: push the point out to at least a third of the radius from the current spot
        Vector3 candidate = home + new Vector3(c.x, 0f, c.y);
        Vector3 here = transform.position;
        if ((candidate - here).magnitude < radius * 0.3f)
        {
            Vector3 dir = (candidate - home).sqrMagnitude > 0.0001f ? (candidate - home).normalized : transform.forward;
            candidate = home + dir * radius * 0.8f;
        }
        candidate.y = here.y;

        if (UsingAgent())
        {
            NavMeshHit hit;
            if (!agent.isOnNavMesh || !NavMesh.SamplePosition(candidate, out hit, 1.5f, NavMesh.AllAreas)) { BeginIdle(); return; }
            Vector3 flatHome = home; flatHome.y = hit.position.y;
            if ((hit.position - flatHome).magnitude > radius) { BeginIdle(); return; }
            target = hit.position;
            agent.speed = walkSpeed;
            agent.isStopped = false;
            agent.SetDestination(target);
        }
        else
        {
            target = candidate;
        }

        walking = true;
        SetWalkAnim(true);
        float dist = (target - here).magnitude;
        stateEnd = Time.time + dist / Mathf.Max(0.05f, walkSpeed) + 4f; // generous timeout, never walks forever
    }

    void TickWalk()
    {
        if (Time.time > stateEnd) { BeginIdle(); return; }

        if (UsingAgent())
        {
            if (!agent.isOnNavMesh) { BeginIdle(); return; }
            if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(arriveDistance, agent.stoppingDistance)) BeginIdle();
            return;
        }

        Vector3 pos = transform.position;
        Vector3 to = target - pos; to.y = 0f;
        float dist = to.magnitude;
        if (dist <= arriveDistance) { BeginIdle(); return; }

        Quaternion want = Quaternion.LookRotation(to / dist, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
        if (Quaternion.Angle(transform.rotation, want) > 35f) return; // turn on the spot first

        Vector3 step = transform.forward * walkSpeed * Time.deltaTime; step.y = 0f;
        Vector3 next = pos + step;
        Vector3 off = next - home; off.y = 0f;
        if (off.magnitude > radius) { BeginIdle(); return; } // hard leash: never step outside the area
        transform.position = next;
    }

    bool UsingAgent()
    {
        return agent != null && agent.enabled;
    }

    void SetWalkAnim(bool on)
    {
        if (hasWalkParam && animator != null && animator.isActiveAndEnabled) animator.SetBool(walkBool, on);
    }

    void ScheduleGesture()
    {
        nextGesture = Time.time + Random.Range(gestureInterval.x, gestureInterval.y);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 c = Application.isPlaying ? home : transform.position;
        Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.9f);
        const int seg = 40;
        Vector3 prev = c + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 p = c + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
