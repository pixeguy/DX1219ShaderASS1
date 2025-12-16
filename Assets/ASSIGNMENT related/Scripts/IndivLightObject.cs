using UnityEngine;

[ExecuteAlways]
public class IndivLightObject : MonoBehaviour
{
    public Vector3 direction = new Vector3(0, -1, 0);
    public Material material;
    public Color lightColor;


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
    public float range;

    private static IndivLightObject[] allLights;
    public int shadowIndex = -1;

    // Update is called once per frame
    void Update()
    {
        direction = transform.rotation * new Vector3(0, -1, 0);
        direction = direction.normalized;
        allLights = FindObjectsByType<IndivLightObject>(FindObjectsSortMode.None);
        SendToShader();
    }

    public Vector3 GetDirection()
    {
        return direction;
    }

    public Material GetMaterial() { return material; }

    private void SendToShader()
    {
        //Shader.SetGlobalInt("_lightCount", allLights.Length);

        //Shader.SetGlobalVector("_lightPosition", transform.position);
        //Shader.SetGlobalVector("_lightDirection", direction);
        //Shader.SetGlobalColor("_lightColor", lightColor);

        //Shader.SetGlobalFloat("_smoothness", smoothness);
        //Shader.SetGlobalFloat("_specularStrength", specularStrength);

        //Shader.SetGlobalInt("_lightType", (int)type);
        //Shader.SetGlobalFloat("_lightIntensity", intensity);

        //Shader.SetGlobalVector("_attenuation", attenuation);

        //Shader.SetGlobalFloat("_spotLightCutOff", spotLightCutOff);
        //Shader.SetGlobalFloat("_spotLightInnerCutOff", spotLightInnerCutOff);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1);
        Gizmos.DrawRay(transform.position, direction * 10.0f);
    }
}
