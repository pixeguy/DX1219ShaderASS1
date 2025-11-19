using UnityEngine;

public class ShadowMapRenderer : MonoBehaviour
{
    [SerializeField]
    private IndivLightObject lightObject;
    [SerializeField]
    private int shadowMapResolution = 1024;
    [SerializeField]
    private float shadowBias = 0.005f;

    private Camera lightCamera;
    private RenderTexture shadowMap;

    private void Start()
    {
        lightObject = GetComponent<IndivLightObject>();
        if (lightObject == null)
        {
            Debug.LogError("ShadowMapper requires a lightObject");
            return;
        }
        CreateLightCamera();
    }
    private void Update()
    {
        if (lightCamera == null || shadowMap == null)
            return;

        UpdateLightCamera();
        SendShadowDataToShader();
    }

    private void CreateLightCamera()
    {
        shadowMap = new RenderTexture(shadowMapResolution, shadowMapResolution, 24, RenderTextureFormat.Depth);
        shadowMap.Create();

        //Create shadow camera
        GameObject lightCamObject = new GameObject("Light Camera");
        lightCamera = lightCamObject.AddComponent<Camera>();
        lightCamera.enabled = false;
        lightCamera.clearFlags = CameraClearFlags.Depth;
        lightCamera.backgroundColor = Color.white;
        lightCamera.targetTexture = shadowMap;

        //Configure camera type
        lightCamera.nearClipPlane = 0.1f;
        lightCamera.farClipPlane = 100f;
        lightCamera.orthographic = true;
        lightCamera.orthographicSize = 30;

        lightCamObject.transform.SetParent(lightObject.transform, false);
    }

    private void UpdateLightCamera()
    {
        //sync shadow camera with light transform
        lightCamera.transform.position = lightObject.transform.position;
        lightCamera.transform.forward = lightObject.GetDirection();

        //render shadow map manually
        lightCamera.Render();
    }

    private void SendShadowDataToShader()
    {
        Material material = lightObject.GetMaterial();
        if (material == null) return;   

        //Calculate light's view-projection matrix
        Matrix4x4 lightViewProjMatrix = lightCamera.projectionMatrix * lightCamera.worldToCameraMatrix;

        //send shadow data to shader
        material.SetTexture("_shadowMap", shadowMap);
        material.SetFloat("_shadowBias", shadowBias);
        material.SetMatrix("_lightViewProj", lightViewProjMatrix);
    }

    private void OnDestroy()
    {
        if (shadowMap != null)
        {
            shadowMap.Release();
        }
        if (lightCamera != null)
        {
            Destroy(lightCamera.gameObject);
        }
    }

    private void OnGUI()
    {
        GUI.DrawTexture(new Rect(10, 10, 512, 512), shadowMap, ScaleMode.ScaleToFit, false);
    }
}
