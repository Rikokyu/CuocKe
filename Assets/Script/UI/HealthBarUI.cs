using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space health bar of one chicken. The scene has TWO instances of the same prefab, told apart by 'role':
///   Player   - always the player's chicken (BattleManager.mainChickenHealth)
///   Opponent - always the current opponent (BattleManager.enemyHealth)
/// Both are HIDDEN outside a fight. While BattleManager.IsBattleActive both are visible at the same time and read
/// HealthSystem.currentHealth / maxHealth every frame (HealthSystem is the single store of HP for all four chickens; ChickenStats
/// writes the player's MaxHP into it). The fighters are re-read from BattleManager every frame, so whoever TournamentManager /
/// RematchController / FinalBossController put there (Chi Ba, Chu Tu, the friend's rooster for the rematch or the boss) is
/// followed automatically, with no per-chicken names. The bar is hidden as soon as the fight is over (the killing blow ends
/// the fight in the same frame). Nothing in the combat code is changed: this only READS BattleManager and HealthSystem.
/// The bar sits above the target's highest renderer, so no per-chicken offset is needed.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    public enum Role { Player, Opponent }

    [Header("Setup")]
    [Tooltip("Player = the player's chicken, Opponent = whoever BattleManager currently fights.")]
    public Role role = Role.Player;
    public Image fillImage;

    [Header("Positioning")]
    [Tooltip("Set automatically from the current target; kept as a serialized field for compatibility.")]
    public Transform worldAnchor;
    [Tooltip("Fallback height when the target has no renderer. Normally the height is measured from the target's renderers.")]
    public Vector3 offset = new Vector3(0f, 2f, 0f);
    [SerializeField, Min(0f)] float headroom = 0.4f;
    [SerializeField, Min(0f)] float minHeight = 1.2f;
    [SerializeField, Min(0.5f)] float maxHeight = 5f;

    Camera cam;
    Canvas canvas;
    HealthSystem current;

    /// <summary>The chicken whose HP is displayed right now (null = hidden).</summary>
    public HealthSystem CurrentTarget { get { return current; } }

    /// <summary>True while this bar is on screen.</summary>
    public bool IsShown { get { return canvas != null && canvas.enabled; } }

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        Hide();
    }

    void Start()
    {
        cam = Camera.main;
    }

    /// <summary>Points the bar at 't'. Null hides it. Normally not needed: the bar follows BattleManager by itself.</summary>
    public void SetTarget(HealthSystem t)
    {
        if (t == current) return;
        current = t;
        worldAnchor = t != null ? t.transform : null;
        if (t != null) offset = MeasureOffset(t);
    }

    void Hide()
    {
        if (canvas != null && canvas.enabled) canvas.enabled = false;
    }

    void LateUpdate()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
        BattleManager bm = BattleManager.Instance;
        HealthSystem wanted = null;
        if (bm != null && bm.IsBattleActive)
            wanted = role == Role.Player ? bm.mainChickenHealth : bm.enemyHealth;

        SetTarget(wanted);
        if (current == null || !current.gameObject.activeInHierarchy) { Hide(); return; }

        if (cam == null) cam = Camera.main;
        transform.position = current.transform.position + offset;
        if (cam != null) transform.forward = cam.transform.forward; // billboard so it always faces the camera

        if (fillImage != null)
            fillImage.fillAmount = current.maxHealth > 0f ? Mathf.Clamp01(current.currentHealth / current.maxHealth) : 0f;
        if (canvas != null && !canvas.enabled) canvas.enabled = true;
    }

    /// <summary>Height above the target's pivot = top of its renderers + headroom (clamped), so every chicken gets a fitting offset.</summary>
    Vector3 MeasureOffset(HealthSystem t)
    {
        float top = float.NegativeInfinity;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            top = Mathf.Max(top, r.bounds.max.y);
        }

        if (float.IsNegativeInfinity(top)) return offset;
        float h = Mathf.Clamp(top - t.transform.position.y + headroom, minHeight, maxHeight);
        return new Vector3(0f, h, 0f);
    }
}
