using UnityEngine;

public class ShadowMapRenderer : MonoBehaviour
{
    // =========================
    // References 
    // =========================
    [SerializeField]
    private IndivLightObject lightObject;

    [SerializeField]
    private float shadowBias = 0.005f;

    private Camera lightCamera;
    public Material depthMaterial;

    // =========================
    // Atlas data
    // =========================
    private Rect atlasViewport;
    private Vector4 atlasUVScaleOffset;   // (offsetX, offsetY, scaleX, scaleY)
    private int lightIndex = -1;

    // =========================
    // Lifecycle
    // =========================
    private void Start()
    {
        lightObject = GetComponent<IndivLightObject>();
        if (lightObject == null)
        {
            Debug.Log("ShadowMapRenderer have no light");
            enabled = false;
            return;
        }
        if (lightObject.type == IndivLightObject.Type.point)
        {
            return;
        }

        if (ShadowAtlasManager.Instance == null)
        {
            Debug.Log("no atlas manager");
            enabled = false;
            return;
        }

        lightIndex = ShadowAtlasManager.Instance.RegisterLight(); //atlas manager will give light their index
        if (lightIndex < 0)
        {
            enabled = false;
            return;
        }

        lightObject.shadowIndex = lightIndex; //for light manager
        if (lightIndex < 0)
        {
            enabled = false;
            return;
        }

        CreateLightCamera();
    }

    // =========================
    // Rendering
    // =========================
    public void RenderShadow()
    {
        if (lightCamera == null)
            return;

        if (lightObject.type == IndivLightObject.Type.point)
        {
            return;
        }
        UpdateLightCamera();
        SendShadowDataToManager();
        lightCamera.Render();
    }

    // =========================
    // Camera setup
    // =========================
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

        
        if (lightObject.type == IndivLightObject.Type.spot)
        {
            lightCamera.orthographic = false;   // perspective
            lightCamera.nearClipPlane = 1f;
            lightCamera.farClipPlane = 40f; 
        }
        else
        {
            lightCamera.orthographic = true;    // orthographic
        }
        lightCamera.orthographicSize = transform.localScale.x * 10;
        float outerFov = lightObject.spotLightCutOff * 2f;
        lightCamera.fieldOfView = outerFov;

        lightCamera.cullingMask = ~0;
        int unlitWaterLayer = LayerMask.NameToLayer("UnlitWater");
        if (unlitWaterLayer >= 0)
            lightCamera.cullingMask &= ~(1 << unlitWaterLayer);

        // use shared atlas
        lightCamera.targetTexture = ShadowAtlasManager.Instance.Atlas;

        atlasViewport = ShadowAtlasManager.Instance.AllocateTileForIndex(lightIndex);
        lightCamera.rect = atlasViewport;

        // sets where in [0-1] space the shadowmap will be on the atlas
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

    // =========================
    // Data upload
    // =========================
    private void SendShadowDataToManager()
    {
        if (lightCamera == null || lightIndex < 0)
            return;

        if (lightObject.type == IndivLightObject.Type.point)
        {
            return;
        }
        Matrix4x4 lightViewProjMatrix =
            lightCamera.projectionMatrix *
            lightCamera.worldToCameraMatrix;

        ShadowAtlasManager.Instance.UpdateLightData(
            lightIndex,
            lightViewProjMatrix,
            atlasUVScaleOffset
        );

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
