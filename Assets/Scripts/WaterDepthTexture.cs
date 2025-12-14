using UnityEngine;
using UnityEngine.Rendering;

public class WaterDepthTexture : MonoBehaviour
{
    private Material material;
    public Camera depthCam;
    private Camera mainCam;
    public RenderTexture depthRT;
    public RenderTexture underwaterColorRT;
    public int TextureWidth = 0;
    public int textureHeight = 0;

    private void Start()
    {
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
            // Per-instance material
            material = rend.material;
        }

        //Create depth camera
        GameObject depthObj = new GameObject("Water Depth Cam");
        depthObj.transform.parent = transform;
        depthCam = depthObj.AddComponent<Camera>();
        depthCam.enabled = false; // manual rendering only

        // Clean up old RT if any
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

        int w = (TextureWidth > 0) ? TextureWidth : Screen.width;
        int h = (textureHeight > 0) ? textureHeight : Screen.height;

        depthRT = new RenderTexture(w, h, 24, RenderTextureFormat.Depth);
        depthRT.filterMode = FilterMode.Bilinear;
        depthRT.wrapMode = TextureWrapMode.Clamp;
        depthRT.Create();

        underwaterColorRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        underwaterColorRT.filterMode = FilterMode.Bilinear;
        underwaterColorRT.wrapMode = TextureWrapMode.Clamp;
        underwaterColorRT.Create();
    }

    private void UpdateCameraSettings()
    {
        if (mainCam == null || depthCam == null) return;

        // Match main camera transform
        depthCam.transform.position = mainCam.transform.position;
        depthCam.transform.rotation = mainCam.transform.rotation;

        // Match projection & settings
        depthCam.orthographic = mainCam.orthographic;
        depthCam.fieldOfView = mainCam.fieldOfView;
        depthCam.orthographicSize = mainCam.orthographicSize;
        depthCam.nearClipPlane = mainCam.nearClipPlane;
        depthCam.farClipPlane = mainCam.farClipPlane;
        depthCam.aspect = mainCam.aspect;

        // Clear depth every frame
        depthCam.clearFlags = CameraClearFlags.Depth;

        // Start by copying main cam culling mask
        depthCam.cullingMask = mainCam.cullingMask;
        int waterLayer = LayerMask.NameToLayer("Water");
        if (waterLayer >= 0)
        {
            depthCam.cullingMask &= ~(1 << waterLayer);
        }
    }

    private void LateUpdate()
    {
        if (depthCam == null || depthRT == null || mainCam == null)
            return;

        UpdateCameraSettings();

        depthCam.clearFlags = CameraClearFlags.SolidColor;
        depthCam.backgroundColor = Color.black;
        depthCam.targetTexture = depthRT;
        depthCam.Render();

        depthCam.clearFlags = CameraClearFlags.SolidColor;
        depthCam.backgroundColor = Color.black;   //or fog color, etc.
        depthCam.targetTexture = underwaterColorRT;
        depthCam.Render();

        depthCam.targetTexture = null;

        // Send depth texture to water material
        if (material != null)
        {
            material.SetTexture("_waterDepthRT", depthRT);
            material.SetTexture("_underWaterRT", underwaterColorRT);
        }
    }

    private void OnDisable()
    {
        if (depthCam != null)
        {
            DestroyImmediate(depthCam.gameObject);
            depthCam = null;
        }

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
