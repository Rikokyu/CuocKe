using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks something the player can look at / interact with (NPC, pickup, quest spot...). The camera centre (the crosshair dot) is the only
/// aim: a ray from the middle of the screen is tested against the visual bounds of every InteractObject (no collider needed), the nearest
/// one that is not hidden behind a wall is <see cref="Aimed"/>. The HUD (InteractionAimHUD) shows its name; interactor scripts only offer the
/// E key while <see cref="IsAimed"/> is true for them, so an object can only be used when the dot points exactly at it.
/// </summary>
public class InteractObject : MonoBehaviour
{
    [Tooltip("Name shown under the crosshair. Empty = the GameObject name.")]
    [SerializeField] string displayName = "";
    [SerializeField, Min(1f)] float aimDistance = 12f;
    [Tooltip("Added to the visual bounds so thin objects are still easy to hit.")]
    [SerializeField, Min(0f)] float boundsPadding = 0.05f;
    [Tooltip("Size of the aim box when the object has no renderer.")]
    [SerializeField] Vector3 fallbackSize = new Vector3(1.2f, 1.2f, 1.2f);

    static readonly List<InteractObject> all = new List<InteractObject>();
    static int aimFrame = -1;

    /// <summary>The object under the crosshair this frame (null if none).</summary>
    public static InteractObject Aimed { get { EnsureAim(); return aimed; } }
    static InteractObject aimed;

    public string DisplayName { get { return string.IsNullOrEmpty(displayName) ? name : displayName; } }

    /// <summary>Sets the shown name from code (auto-registration). Ignored if a name was already typed in the Inspector.</summary>
    public void SetDisplayName(string value) { if (string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(value)) displayName = value; }

    Renderer[] renderers;
    int cacheFrame = -1000;

    void OnEnable() { if (!all.Contains(this)) all.Add(this); }

    void OnDisable()
    {
        all.Remove(this);
        if (aimed == this) aimed = null;
    }

    /// <summary>True if the crosshair points at the object that 'who' belongs to (same object, or a parent / child of it).</summary>
    public static bool IsAimed(Component who)
    {
        if (who == null) return false;
        InteractObject a = Aimed;
        if (a == null) return false;
        Transform t = who.transform, at = a.transform;
        return t == at || t.IsChildOf(at) || at.IsChildOf(t);
    }

    /// <summary>True if the crosshair points at exactly this GameObject (or something inside it).</summary>
    public static bool IsAimedAtObject(GameObject go)
    {
        if (go == null) return false;
        InteractObject a = Aimed;
        return a != null && (a.transform == go.transform || a.transform.IsChildOf(go.transform) || go.transform.IsChildOf(a.transform));
    }

    static void EnsureAim()
    {
        if (aimFrame == Time.frameCount) return;
        aimFrame = Time.frameCount;
        aimed = ComputeAim();
    }

    static InteractObject ComputeAim()
    {
        Camera cam = Camera.main;
        if (cam == null || all.Count == 0) return null;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        InteractObject best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < all.Count; i++)
        {
            InteractObject o = all[i];
            if (o == null) continue;
            Bounds b;
            if (!o.TryGetBounds(out b)) continue;
            float d;
            if (!b.IntersectRay(ray, out d)) continue;
            if (d < 0f) d = 0f;
            if (d > o.aimDistance || d >= bestDist) continue;
            if (o.IsHiddenBehindWall(ray, d)) continue;
            best = o;
            bestDist = d;
        }
        return best;
    }

    bool IsHiddenBehindWall(Ray ray, float distance)
    {
        if (distance < 0.5f) return false;
        RaycastHit[] hits = Physics.RaycastAll(ray, distance - 0.3f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Transform ht = hits[i].collider.transform;
            if (ht == transform || ht.IsChildOf(transform) || transform.IsChildOf(ht)) continue;       // the object itself
            if (hits[i].collider.GetComponentInParent<CharacterController>() != null) continue;           // the player
            if (hits[i].collider.GetComponentInParent<PetCombatAI>() != null) continue;                   // the pet chicken follows the player
            return true;
        }
        return false;
    }

    bool TryGetBounds(out Bounds b)
    {
        if (renderers == null || Time.frameCount - cacheFrame > 120)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            cacheFrame = Time.frameCount;
        }

        bool has = false;
        b = new Bounds(transform.position, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer) && !(r is SpriteRenderer)) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!has)
        {
            if (renderers.Length > 0) return false;      // it has a model but the model is hidden right now (picked up / carried): not aimable
            b = new Bounds(transform.position + Vector3.up * (fallbackSize.y * 0.5f), fallbackSize);
        }
        b.Expand(boundsPadding * 2f);
        return true;
    }
}
