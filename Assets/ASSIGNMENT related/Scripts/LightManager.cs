using UnityEngine;

[ExecuteAlways]
public class LightManager : MonoBehaviour
{
    public Material material;

    private IndivLightObject[] allLights;

    public const int count = 10;

    //keep lights outside so i always pass in the same array size
    private Vector4[] positions = new Vector4[count];
    private Vector4[] directions = new Vector4[count];
    private Color[] colors = new Color[count];
    private Vector4[] colorVector = new Vector4[count];
    private float[] intensities = new float[count];
    private int[] types = new int[count];
    private Vector4[] attenuations = new Vector4[count];
    private float[] cutOffs = new float[count];
    private float[] innerCutOffs = new float[count];
    private float[] ranges = new float[count];
    private float[] orthoSize = new float[count];
    private float[] hasShadow = new float[count];

    private void Update()
    {
        allLights = FindObjectsByType<IndivLightObject>(FindObjectsSortMode.None);

        int activeCount = Mathf.Min(allLights.Length, count);
        int maxIndexUsed = -1;

        foreach (var light in allLights)
        {
            if (light == null)
                continue;
            if (light.type == IndivLightObject.Type.point) continue; // IMPORTANT

            int idx = light.shadowIndex;
            if (idx < 0 || idx >= count)
                continue; // skip lights that aren't in the atlas
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

            hasShadow[idx] = 1f;

            if (idx > maxIndexUsed)
                maxIndexUsed = idx;
        }

        int pointIdx = maxIndexUsed + 1;

        foreach (var light in allLights)
        {
            if (light == null) continue;
            if (light.type != IndivLightObject.Type.point) continue;

            if (pointIdx < 0 || pointIdx >= count) break; // no more room

            int idx = pointIdx++;

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

            hasShadow[idx] = 0f;

            // point lights have no shadow camera
            orthoSize[idx] = -1f;
        }

        Shader.SetGlobalFloat("_lightCount", activeCount);

        Shader.SetGlobalVectorArray("_lightPosition", positions);
        Shader.SetGlobalVectorArray("_lightDirection", directions);
        Shader.SetGlobalVectorArray("_lightColor", colorVector);

        Shader.SetGlobalFloatArray("_lightIntensity", intensities);

        // shader expects float array for types
        float[] typeFloats = new float[count];
        for (int i = 0; i < count; i++)
            typeFloats[i] = (float)types[i];

        Shader.SetGlobalFloatArray("_lightType", typeFloats);

        Shader.SetGlobalVectorArray("_attenuation", attenuations);
        Shader.SetGlobalFloatArray("_spotLightCutOff", cutOffs);
        Shader.SetGlobalFloatArray("_spotLightInnerCutOff", innerCutOffs);
        Shader.SetGlobalFloatArray("_ranges", ranges);
        Shader.SetGlobalFloatArray("_camSize", orthoSize);
        Shader.SetGlobalFloatArray("_hasShadow", hasShadow);
    }
}
