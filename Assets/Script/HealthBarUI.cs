using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space health bar that follows a target and billboards toward the camera.
/// Put this on a World Space Canvas (small, parented under or near the fighter, or left
/// standalone in the scene and positioned via worldAnchor+offset every frame).
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [Header("Setup")]
    public HealthSystem target;
    public Image fillImage;

    [Header("Positioning")]
    [Tooltip("Usually the fighter's own transform (e.g. ChienKe_Pet or Rooster root).")]
    public Transform worldAnchor;
    public Vector3 offset = new Vector3(0f, 2f, 0f);

    Camera cam;

    void Start()
    {
        cam = Camera.main;

        if (target != null)
        {
            target.OnHealthChanged.AddListener(UpdateBar);
            UpdateBar(target.currentHealth, target.maxHealth);
        }
    }

    void LateUpdate()
    {
        if (worldAnchor != null)
            transform.position = worldAnchor.position + offset;

        if (cam != null)
            transform.forward = cam.transform.forward; // billboard so it always faces the camera
    }

    void UpdateBar(float current, float max)
    {
        if (fillImage != null)
            fillImage.fillAmount = max > 0f ? current / max : 0f;
    }

    void OnDestroy()
    {
        if (target != null)
            target.OnHealthChanged.RemoveListener(UpdateBar);
    }
}
