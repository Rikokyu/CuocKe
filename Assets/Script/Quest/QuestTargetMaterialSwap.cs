using UnityEngine;

/// <summary>
/// Simple visual feedback for a QuestObjectiveTarget (e.g. a vegetable bed): shows the "dry" material until
/// the target was used in the current quest cycle, then the "used" (watered) material.
/// Optionally a plant on the bed "perks up" (grows a little) while the target counts as used.
/// It only READS QuestObjectiveTarget.IsUsed - it never counts progress or pays anything.
/// </summary>
[RequireComponent(typeof(QuestObjectiveTarget))]
public class QuestTargetMaterialSwap : MonoBehaviour
{
    [SerializeField] Renderer targetRenderer;
    [SerializeField] Material dryMaterial;
    [SerializeField] Material usedMaterial;

    [Header("Optional: plant that perks up when watered")]
    [Tooltip("Root of the plant model. Must NOT be a child of a non-uniformly scaled object.")]
    [SerializeField] Transform plantVisual;
    [SerializeField, Min(1f)] float wateredScaleMultiplier = 1.25f;
    [SerializeField, Min(0.1f)] float scaleSpeed = 6f;

    QuestObjectiveTarget target;
    bool? lastUsed;
    Vector3 plantBaseScale;
    Vector3 plantTargetScale;

    void Awake()
    {
        target = GetComponent<QuestObjectiveTarget>();
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        if (plantVisual != null)
        {
            plantBaseScale = plantVisual.localScale;
            plantTargetScale = plantBaseScale;
        }
    }

    void Update()
    {
        if (target == null) return;

        bool used = target.IsUsed;
        if (!lastUsed.HasValue || lastUsed.Value != used)
        {
            lastUsed = used;

            if (targetRenderer != null)
            {
                Material wanted = used ? usedMaterial : dryMaterial;
                if (wanted != null) targetRenderer.sharedMaterial = wanted;
            }

            if (plantVisual != null)
                plantTargetScale = used ? plantBaseScale * wateredScaleMultiplier : plantBaseScale;
        }

        if (plantVisual != null && plantVisual.localScale != plantTargetScale)
        {
            Vector3 next = Vector3.Lerp(plantVisual.localScale, plantTargetScale, 1f - Mathf.Exp(-scaleSpeed * Time.deltaTime));
            if ((next - plantTargetScale).sqrMagnitude < 0.000001f) next = plantTargetScale;
            plantVisual.localScale = next;
        }
    }
}
