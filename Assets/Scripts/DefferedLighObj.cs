using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class DefferedLighObj : MonoBehaviour
{
    public static readonly List<DefferedLighObj> All = new List<DefferedLighObj>();

    public Vector3 direction = new Vector3(0, -1, 0);
    public Material material;
    public Color lightColor;
    [Range(0f, 1f)]
    public float smoothness;
    [Range(0f, 1f)]
    public float specularStrength;


    public enum Type
    {
        direction = 0,
        point = 1,
        spot = 2,
    }

    public Type type;
    [Range(0f, 10f)]
    public float intensity;

    public Vector3 attenuation = new Vector3(1.0f, 0.09f, 0.032f);
    [Range(0f, 360f)]
    public float spotLightCutOff;
    [Range(0f, 360f)]
    public float spotLightInnerCutOff;
    public float range = 10f;

    private static LightObject[] allLights;

    private void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }


    // Update is called once per frame
    void Update()
    {
        direction = transform.forward;
        direction = direction.normalized;
        allLights = FindObjectsByType<LightObject>(FindObjectsSortMode.None);
        SendToShader();
    }

    public Vector3 GetDirection()
    {
        return direction;
    }

    public Material GetMaterial() { return material; }

    private void SendToShader()
    {
        Shader.SetGlobalInt("_lightCounts", allLights.Length);

        Shader.SetGlobalFloat("_lightRange", range);
        Shader.SetGlobalVector("_lightPosition", transform.position);
        Shader.SetGlobalVector("_lightDirection", direction);
        Shader.SetGlobalColor("_lightColor", lightColor);

        Shader.SetGlobalFloat("_smoothness", smoothness);
        Shader.SetGlobalFloat("_specularStrength", specularStrength);

        Shader.SetGlobalInt("_lightType", (int)type);
        Shader.SetGlobalFloat("_lightIntensity", intensity);

        Shader.SetGlobalVector("_attenuation", attenuation);

        Shader.SetGlobalFloat("_spotLightCutOff", spotLightCutOff);
        Shader.SetGlobalFloat("_spotLightInnerCutOff", spotLightInnerCutOff);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1);
        Gizmos.DrawRay(transform.position, direction * 10.0f);
    }
}
