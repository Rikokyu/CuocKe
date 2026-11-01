using UnityEngine;

/// <summary>
/// Visual-only helper for the rooster fighters whose model was swapped (Chicken Warrior, ROCKY, jacketBlocky).
/// It never touches HP, damage, cooldowns, quest or battle state; it only reads them.
///  - Plays the Animator trigger "GetHit" when the fighter loses health (only if its controller has that trigger).
///  - For models that have NO animation clips (ROCKY is a static mesh, jacketBlocky has a rig but no clips) it moves the
///    wrapper object procedurally: idle breathing, run bob + lean, a lunge during the attack states, a flinch when hit
///    and a tip-over when HealthSystem.IsDead. The real combat timing (attack tag, OnAttackHit events) still comes from
///    the Animator controller on this object.
/// </summary>
[DisallowMultipleComponent]
public class OpponentModelMotion : MonoBehaviour
{
    [Header("Animated model")]
    public string hitTrigger = "GetHit";

    [Header("Procedural motion (models without clips)")]
    public bool procedural;
    [Tooltip("The wrapper that moves. Its pivot should be at the feet.")]
    public Transform visual;
    public float bobHeight = 0.04f;
    public float bobSpeed = 11f;
    public float runLeanDegrees = 10f;
    public float lungeDistance = 0.12f;
    public float lungeDegrees = 22f;
    public float flinchDegrees = 12f;
    public float fallDegrees = 88f;
    public float fallDrop = 0.0f;

    Animator anim;
    HealthSystem health;
    float lastHealth;
    float flinch;      // 1 -> 0
    float fall;        // 0 upright .. 1 lying
    Vector3 basePos;
    Quaternion baseRot;

    void Awake()
    {
        anim = GetComponent<Animator>();
        health = GetComponent<HealthSystem>();
        if (visual != null) { basePos = visual.localPosition; baseRot = visual.localRotation; }
        if (!procedural) enabled = enabled; // keep Update cheap: it returns at once when not procedural
    }

    void OnEnable()
    {
        if (health == null) health = GetComponent<HealthSystem>();
        if (health != null) { lastHealth = health.currentHealth; health.OnHealthChanged.AddListener(OnHealthChanged); }
    }

    void OnDisable()
    {
        if (health != null) health.OnHealthChanged.RemoveListener(OnHealthChanged);
    }

    void OnHealthChanged(float current, float max)
    {
        bool lost = current < lastHealth - 0.01f;
        lastHealth = current;
        if (!lost || (health != null && health.IsDead)) return;
        flinch = 1f;
        if (anim != null && !string.IsNullOrEmpty(hitTrigger) && HasParameter(hitTrigger, AnimatorControllerParameterType.Trigger))
            anim.SetTrigger(hitTrigger);
    }

    bool HasParameter(string parameterName, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.name == parameterName && p.type == type) return true;
        return false;
    }

    void Update()
    {
        if (!procedural || visual == null || anim == null) return;
        float dt = Time.deltaTime;

        bool dead = health != null && health.IsDead;
        fall = Mathf.MoveTowards(fall, dead ? 1f : 0f, dt / 0.5f);
        flinch = Mathf.MoveTowards(flinch, 0f, dt / 0.25f);

        float lunge = 0f;
        AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(0);
        if (!dead && st.IsTag("Attack")) lunge = Mathf.Sin(Mathf.Clamp01(st.normalizedTime / 0.9f) * Mathf.PI);

        bool running = anim.GetBool("isRunning");
        float bob = running ? Mathf.Abs(Mathf.Sin(Time.time * bobSpeed)) * bobHeight : Mathf.Sin(Time.time * 2f) * bobHeight * 0.25f;
        float lean = running ? runLeanDegrees : 0f;

        float pitch = lean + lunge * lungeDegrees - flinch * flinchDegrees + fall * fallDegrees; // +x tips forward... fall is backwards below
        Vector3 pos = basePos + Vector3.up * (bob - fall * fallDrop) + Vector3.forward * (lunge * lungeDistance - flinch * lungeDistance * 0.5f);
        visual.localPosition = pos;
        // forward lean/lunge pitch the model forward; the death tips it onto its back
        visual.localRotation = baseRot * Quaternion.Euler(lean + lunge * lungeDegrees - flinch * flinchDegrees, 0f, 0f) * Quaternion.Euler(-fall * fallDegrees, 0f, 0f);
    }
}
