using UnityEngine;

[ExecuteInEditMode]
public class LightManager : MonoBehaviour
{
    public Material material;
    private LightObject[] allLights;
    const int count = 2;//Mathf.Min(allLights.Length, 16);

    void Update()
    {
        allLights = FindObjectsByType<LightObject>(FindObjectsSortMode.None);

        Vector4[] positions = new Vector4[count];
        Vector4[] directions = new Vector4[count];
        Color[] colors = new Color[count];
        float[] intensities = new float[count];
        int[] types = new int[count];
        Vector4[] attenuations = new Vector4[count];
        float[] cutOffs = new float[count];
        float[] innerCutOffs = new float[count];

        for (int i = 0; i < count; i++)
        {
            var light = allLights[i];

            positions[i] = light.transform.position;
            directions[i] = light.direction;
            colors[i] = light.lightColor;
            intensities[i] = light.intensity;
            types[i] = (int)light.type;
            attenuations[i] = light.attenuation;
            cutOffs[i] = light.spotLightCutOff;
            innerCutOffs[i] = light.spotLightInnerCutOff;
        }
        Debug.Log(count);
        material.SetInteger("_lightCounts", count);
        material.SetVectorArray("_lightPosition", positions);
        material.SetVectorArray("_lightDirection", directions);
        material.SetColorArray("_lightColor", colors);
        material.SetFloatArray("_lightIntensity", intensities);
        float[] typeFloats = new float[count];
        for (int i = 0; i < count; i++)
            typeFloats[i] = (float)types[i];

        material.SetFloatArray("_lightType", typeFloats);
        material.SetVectorArray("_attenuation", attenuations);
        material.SetFloatArray("_spotLightCutOff", cutOffs);
        material.SetFloatArray("_spotLightInnerCutOff", innerCutOffs);
    }
}
