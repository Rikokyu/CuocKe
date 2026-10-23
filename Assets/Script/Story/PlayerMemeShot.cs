using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// A short "reaction shot": the first-person camera swings out in FRONT of the Player and frames the whole 2D meme picture
/// (the SpriteRenderer "meme" on the Player), holds, then returns to the exact first-person pose.
///
/// Why it is needed: in first person the picture sits 0.19 m in front of the camera, inside the near clip, so it is never seen.
/// Findings (measured with rendered pixels): the sprite is only upright when viewed from BEHIND the Player (the Player's own
/// view direction); from the front it is mirrored, so the picture is flipped (flipX) for the duration of the shot and restored after.
///
/// The camera distance is computed from the picture's real size and the camera field of view so the WHOLE picture fits
/// ("fill" = share of the limiting frame side the picture occupies). Nothing is edited permanently: the camera's local pose, the
/// flip, the frozen scripts (mouse look / movement) and the hidden held items are all restored, also if this component is disabled
/// in the middle. It never touches the controller scripts except enabling/disabling them, like the sleep and intro screens do.
/// </summary>
public class PlayerMemeShot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera cam;                    // the first-person Main Camera (child of the Player)
    [SerializeField] SpriteRenderer meme;           // Player/meme
    [Tooltip("Switched off during the shot (MouseMovement, PlayerMovement) and restored after.")]
    [SerializeField] Behaviour[] freeze;
    [Tooltip("Hidden during the shot, e.g. HeldItemAnchor (held items are children of the camera).")]
    [SerializeField] GameObject[] hideDuringShot;
    [Tooltip("Shows the optional caption (the Player's line) while the picture is held on screen.")]
    [SerializeField] MessageBannerUI banner;

    [Header("Framing")]
    [Tooltip("Share of the limiting frame side (height for a tall picture) that the picture fills. Lower = more air around it.")]
    [SerializeField, Range(0.3f, 0.9f)] float fill = 0.62f;
    [SerializeField, Min(0.3f)] float minDistance = 0.5f;
    [Tooltip("The sprite is mirrored when seen from the front; flip it back during the shot.")]
    [SerializeField] bool unmirror = true;

    [Header("Timing")]
    [Tooltip("Swing out to the front of the Player (seconds). Fast: 0.3.")]
    [SerializeField, Min(0.05f)] float moveSeconds = 0.3f;
    [Tooltip("How long the whole picture stays on screen before the camera goes back: 3 to 5 seconds.")]
    [SerializeField, Range(3f, 5f)] float holdSeconds = 4f;
    [Tooltip("Swing back to first person (seconds). Fast: 0.25.")]
    [SerializeField, Min(0.05f)] float returnSeconds = 0.25f;

    bool playing;
    string pendingCaption;
    Coroutine routine;
    Vector3 savedLocalPos;
    Quaternion savedLocalRot;
    bool savedFlip;
    bool[] freezeWas;
    bool[] hideWas;

    /// <summary>True while the camera is away from its first-person pose.</summary>
    public bool IsPlaying { get { return playing; } }

    /// <summary>The pose the camera will take (for tests / tuning). Returns false if the references are missing.</summary>
    public bool ComputePose(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero; rotation = Quaternion.identity;
        if (cam == null || meme == null || meme.sprite == null) return false;

        Bounds b = meme.bounds;
        Vector3 front = meme.transform.forward; front.y = 0f;
        if (front.sqrMagnitude < 0.0001f) front = Vector3.forward;
        front.Normalize();

        float vHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float hHalf = vHalf * cam.aspect;
        float dV = (b.size.y * 0.5f) / (vHalf * fill);
        float dH = (b.size.x * 0.5f) / (hHalf * fill);
        float d = Mathf.Max(Mathf.Max(dV, dH), minDistance);

        // keep the camera out of walls: stop in front of the first solid thing (the Player's own capsule is skipped: the ray starts inside it)
        RaycastHit hit;
        if (Physics.Raycast(b.center, front, out hit, d + 0.15f, ~0, QueryTriggerInteraction.Ignore))
            d = Mathf.Max(0.35f, hit.distance - 0.15f);

        position = b.center + front * d;
        rotation = Quaternion.LookRotation(-front, Vector3.up);
        return true;
    }

    /// <summary>Starts the shot. Returns false if it is already playing or the setup is incomplete.</summary>
    public bool Play(Action onFinished = null)
    {
        return Play(null, onFinished);
    }

    /// <summary>Same, and shows "caption" (e.g. the Player's reaction line) on the message banner while the picture is held.</summary>
    public bool Play(string caption, Action onFinished = null)
    {
        if (playing || !isActiveAndEnabled) return false;
        pendingCaption = caption;
        Vector3 p; Quaternion r;
        if (!ComputePose(out p, out r)) return false;
        routine = StartCoroutine(Run(p, r, onFinished));
        return true;
    }

    IEnumerator Run(Vector3 targetPos, Quaternion targetRot, Action onFinished)
    {
        playing = true;
        Transform ct = cam.transform;
        savedLocalPos = ct.localPosition;
        savedLocalRot = ct.localRotation;
        savedFlip = meme.flipX;

        freezeWas = new bool[freeze != null ? freeze.Length : 0];
        for (int i = 0; i < freezeWas.Length; i++) if (freeze[i] != null) { freezeWas[i] = freeze[i].enabled; freeze[i].enabled = false; }
        hideWas = new bool[hideDuringShot != null ? hideDuringShot.Length : 0];
        for (int i = 0; i < hideWas.Length; i++) if (hideDuringShot[i] != null) { hideWas[i] = hideDuringShot[i].activeSelf; hideDuringShot[i].SetActive(false); }
        if (unmirror) meme.flipX = !savedFlip;

        Vector3 startPos = ct.position;
        Quaternion startRot = ct.rotation;
        yield return Blend(ct, startPos, startRot, targetPos, targetRot, moveSeconds);
        if (banner != null && !string.IsNullOrEmpty(pendingCaption)) banner.Show(pendingCaption, holdSeconds);
        yield return new WaitForSeconds(holdSeconds);

        // return along the way back to the real first-person pose (the Player is frozen, so it is a fixed world pose)
        Transform parent = ct.parent;
        Vector3 homePos = parent != null ? parent.TransformPoint(savedLocalPos) : savedLocalPos;
        Quaternion homeRot = parent != null ? parent.rotation * savedLocalRot : savedLocalRot;
        yield return Blend(ct, ct.position, ct.rotation, homePos, homeRot, returnSeconds);

        Restore();
        if (onFinished != null) onFinished();
    }

    static IEnumerator Blend(Transform t, Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float seconds)
    {
        float e = 0f;
        while (e < seconds)
        {
            e += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / seconds));
            t.position = Vector3.Lerp(fromPos, toPos, k);
            t.rotation = Quaternion.Slerp(fromRot, toRot, k);
            yield return null;
        }
        t.position = toPos;
        t.rotation = toRot;
    }

    void Restore()
    {
        if (cam != null)
        {
            cam.transform.localPosition = savedLocalPos;
            cam.transform.localRotation = savedLocalRot;
        }
        if (meme != null) meme.flipX = savedFlip;
        for (int i = 0; i < hideWas.Length; i++) if (hideDuringShot[i] != null) hideDuringShot[i].SetActive(hideWas[i]);
        for (int i = 0; i < freezeWas.Length; i++) if (freeze[i] != null) freeze[i].enabled = freezeWas[i];
        playing = false;
        routine = null;
    }

    void OnDisable()
    {
        if (playing)
        {
            if (routine != null) StopCoroutine(routine);
            Restore(); // never leave the player without mouse look
        }
    }
}
