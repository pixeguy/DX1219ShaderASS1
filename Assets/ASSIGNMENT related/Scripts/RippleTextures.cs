using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class RippleTextures : MonoBehaviour
{
    // ======================================================
    // Shader / Material
    // ======================================================
    [Header("Shader / Material")]
    public Shader rippleShader;  // WaterRipple sim shader that returns height
    private Material rippleMat;

    [Header("Resolution")]
    public int resolution = 1024;

    [Header("Impulse / Object Texture")]
    public RenderTexture objRT;     // stores texture of the objects on water // tells shader when impulse happens

    [Header("Layers")]
    public string simLayerName; // make this layer in Unity
    private int simLayer;

    // ======================================================
    // RTs
    // ======================================================
    private RenderTexture CurrRT;
    private RenderTexture PrevRT;
    private RenderTexture TempRT;

    // ======================================================
    // Scene objects
    // ======================================================
    private Camera simCam;
    private GameObject simQuadGO;
    private Renderer waterRenderer;

    private void Awake()
    {
        waterRenderer = GetComponent<Renderer>();
    }

    private void Start()
    {
        // ------------------------------
        // Layer setup
        // ------------------------------
        simLayer = LayerMask.NameToLayer(simLayerName);
        if (simLayer < 0)
        {
            Debug.LogWarning($"Layer '{simLayerName}' not found. Please create it in the Tags & Layers settings.");
            simLayer = 0;
        }

        // ------------------------------
        // Create sim material
        // ------------------------------
        rippleMat = new Material(rippleShader);

        // ------------------------------
        // Create simulation RTs
        // ------------------------------
        CurrRT = CreateRFloatRT(resolution, "Ripple_CurrRT"); //RFloatRT because it only stores one float, for height
        PrevRT = CreateRFloatRT(resolution, "Ripple_PrevRT");
        TempRT = CreateRFloatRT(resolution, "Ripple_TempRT");

        Graphics.Blit(Texture2D.blackTexture, CurrRT);
        Graphics.Blit(Texture2D.blackTexture, PrevRT);
        Graphics.Blit(Texture2D.blackTexture, TempRT);

        // ------------------------------
        // Create sim camera + quad
        // ------------------------------
        CreateSimCamera();
        CreateSimQuad();

        // ------------------------------
        // Send initial RT to water material
        // ------------------------------
        waterRenderer.material.SetTexture("_RippleTex", CurrRT);
    }

    private void OnDestroy()
    {
        if (CurrRT != null) CurrRT.Release();
        if (PrevRT != null) PrevRT.Release();
        if (TempRT != null) TempRT.Release();
    }

    // ======================================================
    // RT creation
    // ======================================================
    private RenderTexture CreateRFloatRT(int size, string name)
    {
        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.RFloat);
        rt.name = name;
        rt.enableRandomWrite = false;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();
        return rt;
    }

    // ======================================================
    // Sim camera
    // ======================================================
    private void CreateSimCamera()
    {
        GameObject camGO = new GameObject("RippleSimCamera");

        camGO.transform.localPosition = new Vector3(1, 1, 0);
        camGO.transform.localRotation = Quaternion.Euler(90, 0, 0);

        simCam = camGO.AddComponent<Camera>();
        simCam.enabled = false;

        simCam.orthographic = true;
        simCam.orthographicSize = 1;
        simCam.nearClipPlane = 0.1f;
        simCam.farClipPlane = 1000f;

        simCam.cullingMask = 1 << simLayer;

        simCam.clearFlags = CameraClearFlags.SolidColor;
        simCam.backgroundColor = Color.black;
    }

    // ======================================================
    // Sim quad
    // ======================================================
    private void CreateSimQuad()
    {
        simQuadGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        simQuadGO.name = "RippleSimQuad";

        simQuadGO.transform.SetParent(simCam.transform, worldPositionStays: false);

        simQuadGO.transform.localPosition = new Vector3(0, 0, 1);
        simQuadGO.transform.localRotation = Quaternion.identity;

        float h = 2f * simCam.orthographicSize;
        float w = h * ((float)resolution / resolution);
        simQuadGO.transform.localScale = new Vector3(w, h, 1);

        simQuadGO.layer = simLayer;

        var quadRenderer = simQuadGO.GetComponent<MeshRenderer>();
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;
        quadRenderer.material = rippleMat;
    }

    // ======================================================
    // Simulation loop
    // ======================================================
    private void Update()
    {
        // ------------------------------
        // Bind inputs
        // ------------------------------
        rippleMat.SetTexture("_currRT", CurrRT);
        rippleMat.SetTexture("_prevRT", PrevRT);
        rippleMat.SetTexture("_objRT", objRT);

        // ------------------------------
        // Render sim pass into TempRT
        // ------------------------------
        simCam.targetTexture = TempRT;
        simCam.Render();

        // ------------------------------
        // Ping-pong RTs
        // ------------------------------
        var oldPrev = PrevRT;
        PrevRT = CurrRT;
        CurrRT = TempRT;
        TempRT = oldPrev;

        // ------------------------------
        // Push to water
        // ------------------------------
        waterRenderer.material.SetTexture("_RippleTex", CurrRT);
    }
}
