using UnityEngine;
using UnityEngine.Rendering;

public class WaterDepthTexture : MonoBehaviour
{
    // ======================================================
    // References
    // ======================================================
    private Material material;
    private Camera depthCam;
    private Camera mainCam;

    // ======================================================
    // Render Targets
    // ======================================================
    private RenderTexture depthRT;
    private RenderTexture underwaterColorRT;

    // ======================================================
    // Settings
    // ======================================================
    public int textureWidth = 0;
    public int textureHeight = 0;


    private void Start()
    {
        // ------------------------------
        // Find Cam and Mat
        // ------------------------------
        mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("WaterDepthTexture: No maine camera fund!");
            enabled = false;
            return;
        }

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            material = rend.material; // per-instance material
        }

        // ------------------------------
        // Create depth camera
        // ------------------------------
        GameObject depthObj = new GameObject("Water Depth Cam");
        depthObj.transform.parent = transform;

        depthCam = depthObj.AddComponent<Camera>();
        depthCam.enabled = false; // manual rendering only

        // ------------------------------
        // Cleanup old RTs
        // ------------------------------
        if (depthRT != null)
        {
            depthRT.Release();
            depthRT = null;
        }

        if (underwaterColorRT != null)
        {
            underwaterColorRT.Release();
            underwaterColorRT = null;
        }

        // ------------------------------
        // Create RTs
        // ------------------------------
        int w = (textureWidth > 0) ? textureWidth : Screen.width;
        int h = (textureHeight > 0) ? textureHeight : Screen.height;

        //depth format for only storing depth
        depthRT = new RenderTexture(w, h, 24, RenderTextureFormat.Depth);
        depthRT.filterMode = FilterMode.Bilinear;
        depthRT.wrapMode = TextureWrapMode.Clamp;
        depthRT.Create();

        //requires ARGB32 because its storing all colors under water
        underwaterColorRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        underwaterColorRT.filterMode = FilterMode.Bilinear;
        underwaterColorRT.wrapMode = TextureWrapMode.Clamp;
        underwaterColorRT.Create();
    }

    private void LateUpdate()
    {
        if (depthCam == null || depthRT == null || mainCam == null)
            return;

        // ------------------------------
        // Sync camera to main Cam
        // ------------------------------
        UpdateCameraSettings();

        // ------------------------------
        // Render depth RT
        // ------------------------------
        depthCam.clearFlags = CameraClearFlags.SolidColor;
        depthCam.backgroundColor = Color.black;
        depthCam.targetTexture = depthRT;
        depthCam.Render();

        // ------------------------------
        // Render underwater color RT
        // ------------------------------
        depthCam.clearFlags = CameraClearFlags.SolidColor;
        depthCam.backgroundColor = Color.black; 
        depthCam.targetTexture = underwaterColorRT;
        depthCam.Render();

        depthCam.targetTexture = null;

        // ------------------------------
        // Push RTs to water material
        // ------------------------------
        if (material != null)
        {
            material.SetTexture("_waterDepthRT", depthRT);
            material.SetTexture("_underWaterRT", underwaterColorRT);
        }
    }

    private void OnDisable()
    {
        // ------------------------------
        // Destroy camera
        // ------------------------------
        if (depthCam != null)
        {
            DestroyImmediate(depthCam.gameObject);
            depthCam = null;
        }

        // ------------------------------
        // Release RTs
        // ------------------------------
        if (depthRT != null)
        {
            depthRT.Release();
            depthRT = null;
        }

        if (underwaterColorRT != null)
        {
            underwaterColorRT.Release();
            underwaterColorRT = null;
        }
    }

    // ======================================================
    // Camera Setup
    // ======================================================
    private void UpdateCameraSettings()
    {
        if (mainCam == null || depthCam == null) return;

        // ------------------------------
        // Match main camera transform
        // ------------------------------
        depthCam.transform.position = mainCam.transform.position;
        depthCam.transform.rotation = mainCam.transform.rotation;

        // ------------------------------
        // Match projection & settings
        // ------------------------------
        depthCam.orthographic = mainCam.orthographic;
        depthCam.fieldOfView = mainCam.fieldOfView;
        depthCam.orthographicSize = mainCam.orthographicSize;
        depthCam.nearClipPlane = mainCam.nearClipPlane;
        depthCam.farClipPlane = mainCam.farClipPlane;
        depthCam.aspect = mainCam.aspect;

        // ------------------------------
        // Culling mask (exclude water layers)
        // ------------------------------
        depthCam.cullingMask = mainCam.cullingMask;

        int waterLayer = LayerMask.NameToLayer("Water");
        int unlitLayer = LayerMask.NameToLayer("UnlitWater"); 

        if (waterLayer >= 0)
            depthCam.cullingMask &= ~(1 << waterLayer);

        if (unlitLayer >= 0)
            depthCam.cullingMask &= ~(1 << unlitLayer);

        // (kept from your intent) clear depth every frame
        depthCam.clearFlags = CameraClearFlags.Depth;
    }

    // ======================================================
    // Debug
    // ======================================================
    private void OnGUI()
    {
        //if (depthRT != null)
        //{
        //    GUI.DrawTexture(new Rect(10, 10, 256, 256), depthRT, ScaleMode.ScaleToFit, false);
        //}
        //if (underwaterColorRT != null)
        //{
        //    GUI.DrawTexture(new Rect(276, 10, 256, 256), underwaterColorRT, ScaleMode.ScaleToFit, false);
        //}
    }
}
