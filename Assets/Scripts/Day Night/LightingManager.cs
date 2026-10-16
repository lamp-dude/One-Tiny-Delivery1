using UnityEngine;

[ExecuteAlways]
public class LightingManager : MonoBehaviour
{
    [SerializeField] private Light DirectionalLight;
    [SerializeField] private LightingPreset Preset;
    [SerializeField, Range(0, 24)] private float TimeOfDay;

    private void Update()
    {
        if (Preset == null)
            return;

        if (Application.isPlaying)
            TimeOfDay = Mathf.Repeat(TimeOfDay + Time.deltaTime, 24f);

        RefreshLighting();
    }

    public void RefreshLighting()
    {
        if (Preset == null)
            return;

        float timePercent = Mathf.Repeat(TimeOfDay, 24f) / 24f;
        RenderSettings.ambientLight = Preset.AmbientColor.Evaluate(timePercent);
        RenderSettings.fogColor = Preset.FogColor.Evaluate(timePercent);

        if (DirectionalLight != null)
        {
            DirectionalLight.color = Preset.DirectionalColor.Evaluate(timePercent);
            DirectionalLight.transform.localRotation = Quaternion.Euler((timePercent * 360f) - 90f, 170f, 0f);
        }
    }

    private void OnValidate()
    {
        if (DirectionalLight == null)
        {
            if (RenderSettings.sun != null)
                DirectionalLight = RenderSettings.sun;
            else
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (Light light in lights)
                {
                    if (light.type == LightType.Directional)
                    {
                        DirectionalLight = light;
                        break;
                    }
                }
            }
        }
    }
}
