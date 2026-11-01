using UnityEngine;

/// <summary>
/// Decorative big punching bag that swings in time with Zoro's looping punch animation.
/// It owns no gameplay (no quest, money or stats): it only READS the Zoro Animator's looping state time and, every time
/// that time crosses one of 'hitTimes' (normalized 0..1 of the loop), kicks a damped spring that tilts 'swingPivot' away from Zoro.
/// Because it follows the animation clock, the bag stays in rhythm even if the animation speed changes.
/// </summary>
public class ZoroBagSwing : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Animator zoro;
    [Tooltip("The pivot the bag hangs from (the bag is its child). Rotated around its own position.")]
    [SerializeField] Transform swingPivot;

    [Header("Punch timing (normalized time of the Zoro loop)")]
    [SerializeField] float[] hitTimes = { 0.11f, 0.61f, 0.70f };
    [Tooltip("Strength of each hit, same order as Hit Times.")]
    [SerializeField] float[] hitPower = { 1f, 1.1f, 0.8f };

    [Header("Swing")]
    [Tooltip("Angular speed (deg/s) a hit of power 1 adds.")]
    [SerializeField, Min(0f)] float impulse = 120f;
    [SerializeField, Min(0f)] float stiffness = 55f;
    [SerializeField, Min(0f)] float damping = 3.2f;
    [SerializeField, Min(1f)] float maxAngle = 28f;

    Quaternion baseRotation;
    float angle;
    float velocity;
    float prevTime = -1f;

    void Start()
    {
        if (swingPivot == null) { Debug.LogError("[ZoroBagSwing] Swing Pivot missing.", this); enabled = false; return; }
        baseRotation = swingPivot.rotation;
        if (zoro == null) Debug.LogWarning("[ZoroBagSwing] Zoro Animator missing - the bag will not swing.", this);
    }

    void Update()
    {
        if (swingPivot == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        if (zoro != null && zoro.isActiveAndEnabled && zoro.runtimeAnimatorController != null)
        {
            float t = zoro.GetCurrentAnimatorStateInfo(0).normalizedTime;
            t = t - Mathf.Floor(t); // 0..1 inside the loop
            if (prevTime >= 0f) DetectHits(prevTime, t);
            prevTime = t;
        }

        // damped spring back to hanging straight down
        float accel = -stiffness * angle - damping * velocity;
        velocity += accel * dt;
        angle = Mathf.Clamp(angle + velocity * dt, -maxAngle, maxAngle);

        // push away from Zoro: a positive angle moves the bottom of the bag along (pivot - zoro), flat
        Vector3 away = swingPivot.position - (zoro != null ? zoro.transform.position : swingPivot.position - Vector3.forward);
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = Vector3.forward;
        Vector3 axis = Vector3.Cross(away.normalized, Vector3.up);
        swingPivot.rotation = Quaternion.AngleAxis(angle, axis) * baseRotation;
    }

    void DetectHits(float from, float to)
    {
        bool wrapped = to < from;
        for (int i = 0; i < hitTimes.Length; i++)
        {
            float h = hitTimes[i];
            bool crossed = wrapped ? (h > from || h <= to) : (h > from && h <= to);
            if (!crossed) continue;
            float power = (hitPower != null && i < hitPower.Length) ? hitPower[i] : 1f;
            velocity += impulse * power;
        }
    }
}
