using UnityEngine;

/// <summary>
/// Makes a prop that is NOT skinned (a conical hat, a pith helmet) move with a bone of the character that wears it.
/// The hat keeps the exact place it has in the scene: its position and rotation relative to the bone are captured once (in Awake, before the
/// Animator has moved anything) and re-applied every frame in LateUpdate, after the Animator. Nothing is re-parented, so the hat's scale,
/// its place in the hierarchy and the rig are untouched, and bones with big import scales cannot distort the hat.
/// </summary>
public class FollowBone : MonoBehaviour
{
    [Tooltip("The bone to follow (the Head bone of the character).")]
    [SerializeField] Transform bone;

    Vector3 localPosition;
    Quaternion localRotation;
    bool ready;

    void Awake()
    {
        if (bone == null) return;
        localPosition = bone.InverseTransformPoint(transform.position);
        localRotation = Quaternion.Inverse(bone.rotation) * transform.rotation;
        ready = true;
    }

    void LateUpdate()
    {
        if (!ready || bone == null) return;
        transform.SetPositionAndRotation(bone.TransformPoint(localPosition), bone.rotation * localRotation);
    }
}
