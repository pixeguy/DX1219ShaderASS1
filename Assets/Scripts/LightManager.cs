using UnityEngine;

[ExecuteAlways]
public class LightManager : MonoBehaviour
{
    public Material material;
    private IndivLightObject[] allLights;
    public const int count = 10;

    //keep lights outside so i always pass in the same array size
    Vector4[] positions = new Vector4[count];
    Vector4[] directions = new Vector4[count];
    Color[] colors = new Color[count];
    Vector4[] colorVector = new Vector4[count];
    float[] intensities = new float[count];
    int[] types = new int[count];
    Vector4[] attenuations = new Vector4[count];
    float[] cutOffs = new float[count];
    float[] innerCutOffs = new float[count];
    float[] ranges = new float[count];
    float[] orthoSize = new float[count];
    void Update()
    {
        allLights = FindObjectsByType<IndivLightObject>(FindObjectsSortMode.None);

        int activeCount = Mathf.Min(allLights.Length, count);
        int maxIndexUsed = -1;
        foreach (var light in allLights)
        {
            if (light == null) continue;
            int idx = light.shadowIndex;
            if (idx < 0 || idx >= count) continue; // skip lights that aren't in the atlas

            positions[idx] = light.transform.position;
            directions[idx] = light.direction;
            colors[idx] = light.lightColor;
            colorVector[idx] = colors[idx];
            intensities[idx] = light.intensity;
            types[idx] = (int)light.type;
            attenuations[idx] = light.attenuation;
            cutOffs[idx] = light.spotLightCutOff;
            innerCutOffs[idx] = light.spotLightInnerCutOff;
            ranges[idx] = light.range;
            Camera cam = light.gameObject.GetComponentInChildren<Camera>();
            orthoSize[idx] = cam.orthographicSize;

            if (idx > maxIndexUsed) maxIndexUsed = idx;
        }
        Shader.SetGlobalFloat("_lightCount", activeCount);
        Shader.SetGlobalVectorArray("_lightPosition", positions);
        Shader.SetGlobalVectorArray("_lightDirection", directions);
        Shader.SetGlobalVectorArray("_lightColor", colorVector);
        Shader.SetGlobalFloatArray("_lightIntensity", intensities);
        float[] typeFloats = new float[count];
        for (int i = 0; i < count; i++)
            typeFloats[i] = (float)types[i];

        Shader.SetGlobalFloatArray("_lightType", typeFloats);
        Shader.SetGlobalVectorArray("_attenuation", attenuations);
        Shader.SetGlobalFloatArray("_spotLightCutOff", cutOffs);
        Shader.SetGlobalFloatArray("_spotLightInnerCutOff", innerCutOffs);
        Shader.SetGlobalFloatArray("_ranges", ranges);
        Shader.SetGlobalFloatArray("_camSize", orthoSize);
    }
}
