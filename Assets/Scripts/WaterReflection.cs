using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class WaterReflection : MonoBehaviour
{
    [Header("Setup")]
    public LayerMask reflectionMask = ~0;
    public int textureSize = 1024;
    public float waterY = 0f;                 // for MVP: horizontal water plane at this Y
    public float clipPlaneOffset = 0.05f;

    [Header("Optional")]
    public bool disablePixelLights = true;

    Camera reflectionCam;
    RenderTexture reflectionRT;
    Renderer rend;
    Camera mainCam;

    static readonly int ReflectionTexID = Shader.PropertyToID("_ReflectionTex");

    bool renderingReflection = false;

    void Awake()
    {
        rend = GetComponent<Renderer>();
    }

    void OnEnable()
    {
        mainCam = Camera.main;
        CreateResources();
    }

    void OnDisable()
    {
        if (reflectionRT != null) { reflectionRT.Release(); reflectionRT = null; }
        if (reflectionCam != null) DestroyImmediate(reflectionCam.gameObject);
    }

    void CreateResources()
    {
        if (reflectionRT == null)
        {
            reflectionRT = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
            reflectionRT.name = "WaterReflectionRT";
            reflectionRT.wrapMode = TextureWrapMode.Clamp;
            reflectionRT.Create();
        }

        if (reflectionCam == null)
        {
            var go = new GameObject("WaterReflectionCam");
            go.hideFlags = HideFlags.HideAndDontSave;
            reflectionCam = go.AddComponent<Camera>();
            reflectionCam.enabled = false;

            // URP needs this component on cameras
            var urpData = go.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderShadows = false;
            urpData.requiresDepthTexture = false;
            urpData.requiresColorTexture = false;
            urpData.renderPostProcessing = false;
        }

        if (rend && rend.sharedMaterial)
            rend.sharedMaterial.SetTexture(ReflectionTexID, reflectionRT);
    }

    void LateUpdate()
    {
        if (!enabled) return;
        if (!mainCam) mainCam = Camera.main;
        if (!mainCam || !rend || !rend.sharedMaterial) return;
        if (renderingReflection) return; // prevent recursion

        CreateResources();

        // Copy main cam settings
        reflectionCam.CopyFrom(mainCam);
        reflectionCam.cullingMask = reflectionMask;
        reflectionCam.targetTexture = reflectionRT;

        // IMPORTANT: don’t let reflection cam be a stack/base camera etc.
        var urp = reflectionCam.GetComponent<UniversalAdditionalCameraData>();
        urp.cameraStack.Clear();

        // --- MVP mirror across horizontal plane (waterY) ---
        Vector3 p = mainCam.transform.position;
        p.y = waterY - (p.y - waterY);
        reflectionCam.transform.position = p;

        Vector3 e = mainCam.transform.eulerAngles;
        e.x = -e.x; // flip pitch
        reflectionCam.transform.eulerAngles = e;

        // Optional: avoid rendering the water itself in the reflection -> put water on a layer, exclude from mask.

        int oldPixelLights = QualitySettings.pixelLightCount;
        if (disablePixelLights) QualitySettings.pixelLightCount = 0;

        // URP-safe render request: still uses Render(), but NOT inside OnWillRenderObject
        renderingReflection = true;
        GL.invertCulling = true;
        reflectionCam.Render();
        GL.invertCulling = false;
        renderingReflection = false;

        if (disablePixelLights) QualitySettings.pixelLightCount = oldPixelLights;

        // Send RT to material (sharedMaterial if you want all instances, or material for per-instance)
        rend.sharedMaterial.SetTexture(ReflectionTexID, reflectionRT);
    }
    private void OnGUI()
    {
        if (reflectionRT != null)
        {
            GUI.DrawTexture(new Rect(10, 10, 256, 256), reflectionRT, ScaleMode.ScaleToFit, false);
        }
    }
}
