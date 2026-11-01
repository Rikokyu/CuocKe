using UnityEngine;

/// <summary>
/// Short cutscene shots with the player's own camera (Camera.main). Focus(target, awayFrom) takes the camera over: the camera look
/// script (MouseMovement) is switched off and the camera glides to a spot in front of 'target' (on the side of 'awayFrom', so the other
/// character is in the background) and keeps looking at it. Focus again on another target to cut / glide to it. Release restores the
/// camera exactly as it was (local position, rotation, field of view) and gives the look script back.
/// Created at runtime on first use (CinematicCamera.Get), no scene setup needed.
/// </summary>
public class CinematicCamera : MonoBehaviour
{
    static CinematicCamera instance;

    /// <summary>The scene's CinematicCamera; one is created if there is none.</summary>
    public static CinematicCamera Get()
    {
        if (instance == null)
        {
            instance = FindFirstObjectByType<CinematicCamera>();
            if (instance == null) instance = new GameObject("CinematicCamera").AddComponent<CinematicCamera>();
        }
        return instance;
    }

    /// <summary>Gives the camera back if a shot is running. Safe to call when nothing is running.</summary>
    public static void ReleaseIfActive()
    {
        if (instance != null && instance.Active) instance.Release();
    }

    [SerializeField, Min(1f)] float minDistance = 2.5f;
    [Tooltip("How far (degrees) the camera sits to the side of the line target -> awayFrom, so the shot is not flat.")]
    [SerializeField] float sideAngle = 25f;
    [SerializeField, Min(0.05f)] float moveSmoothTime = 0.35f;
    [SerializeField, Min(0.1f)] float turnSpeed = 8f;

    public enum ShotStyle { Normal, Close, Face }

    [Header("Close shot (celebrating chicken)")]
    [SerializeField, Min(0.3f)] float closeMinDistance = 1.3f;
    [SerializeField, Min(0.1f)] float closeSizeFactor = 0.85f;

    [Header("Face shot (straight in front of a humanoid's face)")]
    [SerializeField, Min(0.3f)] float faceMinDistance = 1.0f;
    [Tooltip("Camera distance = model height x this.")]
    [SerializeField, Min(0.1f)] float faceHeightFactor = 0.45f;

    public bool Active { get; private set; }

    ShotStyle style;
    Transform faceHead, faceRoot;
    float faceUp;

    Camera cam;
    Transform camT;
    Vector3 savedLocalPosition;
    Quaternion savedLocalRotation;
    float savedFov;
    MouseMovement look;
    bool lookWasEnabled;

    Transform target, awayFrom;
    float distance;
    Vector3 velocity;

    /// <summary>Starts / retargets the shot. 'awayFrom' (optional) is the character that should stay in the background.</summary>
    public bool Focus(Transform shotTarget, Transform shotAwayFrom, ShotStyle shotStyle = ShotStyle.Normal)
    {
        if (shotTarget == null) return false;
        if (!Active && !Begin()) return false;

        target = shotTarget;
        awayFrom = shotAwayFrom;
        style = shotStyle;
        faceHead = null; faceRoot = null;
        Bounds tb = BoundsOf(target);
        if (style == ShotStyle.Face)
        {
            Animator a = target.GetComponentInChildren<Animator>();
            if (a != null && a.isHuman) { faceHead = a.GetBoneTransform(HumanBodyBones.Head); faceRoot = a.transform; }
            if (faceHead == null) style = ShotStyle.Normal; // no humanoid head: normal shot
        }
        if (style == ShotStyle.Face)
        {
            distance = Mathf.Max(faceMinDistance, tb.size.y * faceHeightFactor);
            faceUp = tb.size.y * 0.03f;
        }
        else if (style == ShotStyle.Close) distance = Mathf.Max(closeMinDistance, tb.size.magnitude * closeSizeFactor);
        else distance = Mathf.Max(minDistance, tb.size.magnitude * 1.6f);
        return true;
    }

    bool Begin()
    {
        cam = Camera.main;
        if (cam == null) return false;
        camT = cam.transform;
        savedLocalPosition = camT.localPosition;
        savedLocalRotation = camT.localRotation;
        savedFov = cam.fieldOfView;
        look = camT.GetComponent<MouseMovement>();
        lookWasEnabled = look != null && look.enabled;
        if (look != null) look.enabled = false;
        velocity = Vector3.zero;
        Active = true;
        return true;
    }

    public void Release()
    {
        if (!Active) return;
        Active = false;
        target = null;
        awayFrom = null;
        if (camT != null)
        {
            camT.localPosition = savedLocalPosition;
            camT.localRotation = savedLocalRotation;
            if (cam != null) cam.fieldOfView = savedFov;
        }
        if (look != null && lookWasEnabled) look.enabled = true;
    }

    void OnDisable()
    {
        Release();
    }

    void LateUpdate()
    {
        if (!Active) return;
        if (camT == null || target == null) { Release(); return; }

        if (style == ShotStyle.Face && faceHead != null)
        {
            float dtf = Time.unscaledDeltaTime;
            Vector3 fwd = faceRoot != null ? faceRoot.forward : target.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = target.forward;
            fwd.Normalize();
            Vector3 faceCenter = faceHead.position + Vector3.up * faceUp;
            camT.position = Vector3.SmoothDamp(camT.position, faceCenter + fwd * distance, ref velocity, moveSmoothTime, Mathf.Infinity, dtf);
            Vector3 toFace = faceCenter - camT.position;
            if (toFace.sqrMagnitude > 0.0001f)
                camT.rotation = Quaternion.Slerp(camT.rotation, Quaternion.LookRotation(toFace), 1f - Mathf.Exp(-turnSpeed * dtf));
            return;
        }

        Bounds b = BoundsOf(target);
        Vector3 center = b.center;

        Vector3 dir = awayFrom != null ? BoundsOf(awayFrom).center - center : target.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = target.forward;
        dir.Normalize();
        dir = Quaternion.Euler(0f, sideAngle, 0f) * dir;

        Vector3 wanted = center + dir * distance + Vector3.up * (b.extents.y * 0.3f + 0.2f);
        float dt = Time.unscaledDeltaTime;
        camT.position = Vector3.SmoothDamp(camT.position, wanted, ref velocity, moveSmoothTime, Mathf.Infinity, dt);

        Vector3 to = center - camT.position;
        if (to.sqrMagnitude > 0.0001f)
            camT.rotation = Quaternion.Slerp(camT.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-turnSpeed * dt));
    }

    /// <summary>World bounds of the visible meshes of 't' (children included); a 1 m box above its position if it has none.</summary>
    static Bounds BoundsOf(Transform t)
    {
        bool has = false;
        Bounds b = new Bounds(t.position + Vector3.up, Vector3.one);
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
