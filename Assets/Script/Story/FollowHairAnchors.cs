using UnityEngine;

/// <summary>
/// Makes a prop that is NOT skinned (the grandmother's conical hat) move exactly like the hair it sits on.
///
/// Why not FollowBone: the grandmother's hair is skinned to Hips and Spine only (about 86% Hips / 14% Spine under the hat, 0% Head).
/// A hat that follows the Head bone slides on the hair whenever Hips / Spine move differently from the Head (walking, talking, dancing).
///
/// How: three ANCHORS are baked in the Editor, one per third of the hair under the hat. An anchor is the average point of a group of hair vertices,
/// written as "share + local point per bone": its world position is the sum over the bones of  share * bone.TransformPoint(localPoint),
/// which is exactly where the skinning puts that group of vertices. No mesh data is read at runtime (it also works in a build).
/// From the three points a frame (origin + rotation) is built every frame in LateUpdate, after the Animator. The hat's pose is stored
/// relative to that frame in Awake (before the Animator moves anything), so the hat goes wherever the hair goes.
/// Nothing is re-parented and the hat's scale is untouched.
/// </summary>
public class FollowHairAnchors : MonoBehaviour
{
    [System.Serializable]
    public class Anchor
    {
        [Tooltip("Share of this anchor that is skinned to each bone (same order as the Bones array; adds up to 1).")]
        public float[] shares;
        [Tooltip("The anchor's point in each bone's local space (same order as the Bones array).")]
        public Vector3[] localPoints;
    }

    [Tooltip("The bones the hair under the hat is skinned to.")]
    [SerializeField] Transform[] bones;
    [Tooltip("Exactly three anchors, spread around the hair under the hat.")]
    [SerializeField] Anchor[] anchors;

    Vector3 hatOffsetInFrame;
    Quaternion hatRotationInFrame;
    bool ready;

    void Awake()
    {
        if (!HasValidData())
        {
            Debug.LogError("[FollowHairAnchors] Bones / Anchors are not filled in correctly (3 anchors, one share and one point per bone).", this);
            return;
        }

        Vector3 origin;
        Quaternion frame;
        if (!ReadFrame(out origin, out frame))
        {
            Debug.LogError("[FollowHairAnchors] The three anchors do not form a triangle.", this);
            return;
        }

        Quaternion frameInverse = Quaternion.Inverse(frame);
        hatOffsetInFrame = frameInverse * (transform.position - origin);
        hatRotationInFrame = frameInverse * transform.rotation;
        ready = true;
    }

    void LateUpdate()
    {
        if (!ready) return;

        Vector3 origin;
        Quaternion frame;
        if (!ReadFrame(out origin, out frame)) return;

        transform.SetPositionAndRotation(origin + frame * hatOffsetInFrame, frame * hatRotationInFrame);
    }

    bool HasValidData()
    {
        if (bones == null || bones.Length == 0 || anchors == null || anchors.Length != 3) return false;
        for (int i = 0; i < bones.Length; i++) if (bones[i] == null) return false;
        foreach (Anchor a in anchors)
            if (a == null || a.shares == null || a.localPoints == null || a.shares.Length != bones.Length || a.localPoints.Length != bones.Length) return false;
        return true;
    }

    Vector3 AnchorWorld(Anchor a)
    {
        Vector3 p = Vector3.zero;
        for (int i = 0; i < bones.Length; i++)
            if (a.shares[i] > 0f) p += a.shares[i] * bones[i].TransformPoint(a.localPoints[i]);
        return p;
    }

    /// <summary>The hair's frame: origin in the middle of the three anchors; X from anchor 0 to 1, Z perpendicular to the triangle.</summary>
    bool ReadFrame(out Vector3 origin, out Quaternion rotation)
    {
        Vector3 a = AnchorWorld(anchors[0]);
        Vector3 b = AnchorWorld(anchors[1]);
        Vector3 c = AnchorWorld(anchors[2]);
        origin = (a + b + c) / 3f;
        rotation = Quaternion.identity;

        Vector3 x = b - a;
        Vector3 n = Vector3.Cross(x, c - a);
        if (x.sqrMagnitude < 1e-10f || n.sqrMagnitude < 1e-10f) return false;
        x.Normalize();
        n.Normalize();
        Vector3 y = Vector3.Cross(n, x);
        rotation = Quaternion.LookRotation(n, y);
        return true;
    }
}
