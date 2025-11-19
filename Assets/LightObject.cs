using UnityEngine;

[ExecuteInEditMode]
public class LightObject : MonoBehaviour
{
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
    [Range(0f,10f)]
    public float intensity;

    public Vector3 attenuation = new Vector3(1.0f, 0.09f, 0.032f);
    [Range(0f, 360f)]
    public float spotLightCutOff;
    [Range(0f, 360f)]
    public float spotLightInnerCutOff;

    private static LightObject[] allLights;


    // Update is called once per frame
    void Update()
    {
        direction = transform.rotation * new Vector3(0, -1, 0);
        direction = direction.normalized;
        allLights = FindObjectsByType<LightObject>(FindObjectsSortMode.None);
        SendToShader();
    }

    private void SendToShader()
    {
        //material.SetInteger("_lightCounts", allLights.Length);
        //material.SetVector("_lightPosition", transform.position);
        //material.SetVector("_lightDirection", direction);
        //material.SetColor("_lightColor", lightColor);
        //material.SetFloat("_smoothness", smoothness);
        //material.SetFloat("_specularStrength", specularStrength);
        //material.SetInteger("_lightType", (int)type);
        //material.SetFloat("_lightIntensity", intensity);
        //material.SetVector("_attenuation", attenuation);
        //material.SetFloat("_spotLightCutOff", spotLightCutOff);
        //material.SetFloat("_spotLightInnerCutOff", spotLightInnerCutOff);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1);
        Gizmos.DrawRay(transform.position, direction * 10.0f);
    }
}
