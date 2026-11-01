using UnityEngine;

public class DayNightSystem : MonoBehaviour
{
    [Tooltip("Sun light. Leave empty to use RenderSettings.sun or the first Directional Light.")]
    public Light directionalLight;

    [Tooltip("Real seconds for one full 24h cycle.")]
    public float dayDurationInSeconds = 120f;

    [Range(0f, 24f)]
    public float currentTimeOfDay = 8f;

    public float maxSunIntensity = 1.6f;
    public float minAmbientIntensity = 0.2f;
    public float maxAmbientIntensity = 1f;
    public float minSkyExposure = 0.3f;
    public float maxSkyExposure = 1.3f;

    [Tooltip("Yaw of the sun around the vertical axis.")]
    public float sunYaw = 170f;

    Material skyMat;
    bool hasExposure;

    void Start()
    {
        if (directionalLight == null) directionalLight = RenderSettings.sun;
        if (directionalLight == null)
        {
            foreach (var l in FindObjectsOfType<Light>())
            {
                if (l.type == LightType.Directional) { directionalLight = l; break; }
            }
        }
        skyMat = RenderSettings.skybox;
        hasExposure = skyMat != null && skyMat.HasProperty("_Exposure");
    }

    void Update()
    {
        currentTimeOfDay += Time.deltaTime / dayDurationInSeconds * 24f;
        if (currentTimeOfDay >= 24f) currentTimeOfDay -= 24f;

        // 0h = midnight (sun below horizon), 12h = noon
        float angle = currentTimeOfDay / 24f * 360f - 90f;
        if (directionalLight != null)
        {
            directionalLight.transform.rotation = Quaternion.Euler(angle, sunYaw, 0f);
        }

        // Elevation factor: 1 at noon, 0 at or below horizon
        float dayFactor = 0f;
        if (directionalLight != null)
        {
            dayFactor = Mathf.Clamp01(Vector3.Dot(-directionalLight.transform.forward, Vector3.up));
            directionalLight.intensity = dayFactor * maxSunIntensity;
        }

        RenderSettings.ambientIntensity = Mathf.Lerp(minAmbientIntensity, maxAmbientIntensity, dayFactor);

        if (hasExposure)
        {
            skyMat.SetFloat("_Exposure", Mathf.Lerp(minSkyExposure, maxSkyExposure, dayFactor));
        }
    }
}
