using UnityEngine;

/// <summary>
/// Spins this object round the world's vertical axis without stopping (the Zoro Tsunami / UIIA-cat spin) and loops a sound while it does.
///  - The spin turns the whole object round the centre of its renderers, so every child (e.g. the conical hat) turns with it.
///  - It is applied in LateUpdate from the pose the object had at the start, as an absolute angle: even if an Animator on the same object
///    writes the transform every frame, the result is still a clean spin and nothing drifts.
///  - It only moves this transform. No quest, dialogue, collider or animation parameter is touched.
/// </summary>
public class ContinuousSpin : MonoBehaviour
{
    [Tooltip("Degrees per second. 720 = two full turns every second (UIIA cat speed). Negative = the other way.")]
    [SerializeField] float degreesPerSecond = 720f;
    [Tooltip("Optional. The turning centre is the centre of the renderers under this object. Empty = all of them.")]
    [SerializeField] Renderer[] pivotRenderers;

    [Header("Looping sound (UIIA)")]
    [SerializeField] AudioClip loopSound;
    [SerializeField, Range(0f, 1f)] float volume = 0.7f;
    [Tooltip("0 = heard everywhere at the same volume, 1 = fully 3D (gets quieter with distance).")]
    [SerializeField, Range(0f, 1f)] float spatialBlend = 1f;
    [SerializeField, Min(1f)] float minDistance = 3f;
    [SerializeField, Min(1f)] float maxDistance = 18f;

    Vector3 basePosition;
    Quaternion baseRotation;
    Vector3 center;
    float angle;
    AudioSource source;

    void Start()
    {
        basePosition = transform.position;
        baseRotation = transform.rotation;

        Renderer[] rs = (pivotRenderers != null && pivotRenderers.Length > 0) ? pivotRenderers : GetComponentsInChildren<Renderer>(true);
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            center = b.center;
        }
        else center = basePosition;

        if (loopSound != null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = loopSound;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = volume;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.Play();
        }
    }

    void OnEnable() { if (source != null && !source.isPlaying) source.Play(); }
    void OnDisable() { if (source != null) source.Pause(); }

    void LateUpdate()
    {
        angle = Mathf.Repeat(angle + degreesPerSecond * Time.deltaTime, 360f);
        Quaternion q = Quaternion.AngleAxis(angle, Vector3.up);
        transform.SetPositionAndRotation(center + q * (basePosition - center), q * baseRotation);
    }
}
