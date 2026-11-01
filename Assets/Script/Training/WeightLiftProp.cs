using UnityEngine;

/// <summary>
/// The barbell of the weight-lifting TrainingStation. It rests on the ground at its scene position and, while the chicken
/// lifts (TrainingStation calls Hold), it follows the midpoint of the chicken's two hands, so the existing 'pushweight'
/// clip really looks like lifting this prop. Release() lays it back on its resting spot.
///
/// Purely visual: no colliders, no gameplay state. If the chicken has no humanoid hands the prop simply stays where it is.
/// </summary>
[DisallowMultipleComponent]
public class WeightLiftProp : MonoBehaviour
{
    [Tooltip("World-space offset added to the hand midpoint (the hand bones sit at the wrists, the bar is gripped a bit lower).")]
    [SerializeField] Vector3 gripOffset = new Vector3(0f, 0.05f, 0f);
    [Tooltip("Seconds to move between the resting spot and the hands (pick up / put down).")]
    [SerializeField, Min(0.05f)] float blendSeconds = 0.5f;

    Transform leftHand;
    Transform rightHand;
    Transform chickenRoot;
    bool held;
    float blend;
    Vector3 restLocalPosition;
    Quaternion restLocalRotation;

    void Awake()
    {
        restLocalPosition = transform.localPosition;
        restLocalRotation = transform.localRotation;
    }

    /// <summary>Starts following the chicken's hands. Does nothing if the Animator has no humanoid hands.</summary>
    public void Hold(Animator chicken)
    {
        if (chicken == null || held) return;
        Transform l = chicken.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform r = chicken.GetBoneTransform(HumanBodyBones.RightHand);
        if (l == null || r == null) return;
        leftHand = l;
        rightHand = r;
        chickenRoot = chicken.transform;
        held = true;
    }

    /// <summary>Puts the barbell back on its resting spot.</summary>
    public void Release()
    {
        held = false;
    }

    public bool IsHeld { get { return held; } }

    void LateUpdate()
    {
        float target = held ? 1f : 0f;
        if (blend <= 0f && target <= 0f) return; // resting: nothing to do
        blend = Mathf.MoveTowards(blend, target, Time.deltaTime / blendSeconds);

        Transform parent = transform.parent;
        Vector3 restPos = parent != null ? parent.TransformPoint(restLocalPosition) : restLocalPosition;
        Quaternion restRot = parent != null ? parent.rotation * restLocalRotation : restLocalRotation;

        if (leftHand == null || rightHand == null)
        {
            blend = 0f;
            held = false;
            transform.SetPositionAndRotation(restPos, restRot);
            return;
        }

        Vector3 mid = (leftHand.position + rightHand.position) * 0.5f + gripOffset;
        Vector3 axis = leftHand.position - rightHand.position;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.0001f) axis = chickenRoot != null ? chickenRoot.right : Vector3.right;
        axis.Normalize();
        Quaternion handRot = Quaternion.LookRotation(Vector3.Cross(axis, Vector3.up), Vector3.up); // bar's local X follows hand-to-hand

        float t = blend * blend * (3f - 2f * blend);
        transform.SetPositionAndRotation(Vector3.Lerp(restPos, mid, t), Quaternion.Slerp(restRot, handRot, t));
    }
}
