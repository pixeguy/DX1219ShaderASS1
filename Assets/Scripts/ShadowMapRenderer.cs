using UnityEngine;

public class ShadowMapRenderer : MonoBehaviour
{
    [SerializeField]
    private IndivLightObject lightObject;
    [SerializeField]
    private float shadowBias = 0.005f;

    private Camera lightCamera;
    public Material depthMaterial;
    // Atlas data
    private Rect atlasViewport;           // in [0,1] screen-rect for camera
    private Vector4 atlasUVScaleOffset;   // (scaleX, scaleY, offsetX, offsetY)
    private int lightIndex = -1;

    private void Start()
    {
        lightObject = GetComponent<IndivLightObject>();
        if (lightObject == null)
        {
            Debug.LogError("ShadowMapRenderer requires an IndivLightObject on the same GameObject.");
            enabled = false;
            return;
        }

        if (ShadowAtlasManager.Instance == null)
        {
            Debug.LogError("No ShadowAtlasManager in scene!");
            enabled = false;
            return;
        }
        lightIndex = ShadowAtlasManager.Instance.RegisterLight();
        if (lightIndex < 0)
        {
            enabled = false;
            return;
        }

        lightObject.shadowIndex = lightIndex;
        if (lightIndex < 0)
        {
            enabled = false;
            return;
        }

        CreateLightCamera();
    }

    private void Update()
    {
    }
    public void RenderShadow()
    {
        if (lightCamera == null) return;
        UpdateLightCamera();
        SendShadowDataToManager();  
        lightCamera.Render();
    }
    private void CreateLightCamera()
    {
        if (lightObject.type == IndivLightObject.Type.point)
        {
            return;
        }

        // Create shadow camera object
        GameObject lightCamObject = new GameObject("Light Camera - " + name);
        lightCamObject.transform.SetParent(lightObject.transform, false);

        lightCamera = lightCamObject.AddComponent<Camera>();
        lightCamera.enabled = false;
        lightCamera.clearFlags = CameraClearFlags.Nothing;
        lightCamera.backgroundColor = Color.white;

        lightCamera.nearClipPlane = 0.1f;
        lightCamera.farClipPlane = 150;
        lightCamera.orthographic = true;
        lightCamera.orthographicSize = transform.localScale.x * 10;

        // use shared atlas
        lightCamera.targetTexture = ShadowAtlasManager.Instance.Atlas;


        atlasViewport = ShadowAtlasManager.Instance.AllocateTileForIndex(lightIndex);
        lightCamera.rect = atlasViewport;

        atlasUVScaleOffset = new Vector4(
            atlasViewport.width,
            atlasViewport.height,
            atlasViewport.x,
            atlasViewport.y
        );
    }

    private void UpdateLightCamera()
    {
        if (lightObject.type == IndivLightObject.Type.point || lightCamera == null)
            return;

        // Sync shadow camera with light
        lightCamera.transform.position = lightObject.transform.position;
        lightCamera.transform.forward = lightObject.GetDirection();
    }

    private void SendShadowDataToManager()
    {
        if (lightCamera == null || lightIndex < 0) return;

        Matrix4x4 lightViewProjMatrix = lightCamera.projectionMatrix * lightCamera.worldToCameraMatrix;

        ShadowAtlasManager.Instance.UpdateLightData(lightIndex, lightViewProjMatrix, atlasUVScaleOffset);


        Shader.SetGlobalFloat("_shadowBias", shadowBias);
    }

    private void OnDestroy()
    {
        if (lightCamera != null)
        {
            Destroy(lightCamera.gameObject);
        }
    }


}
