using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class RippleTextures : MonoBehaviour
{
    [Header("Shader / Material")]
    public Shader rippleShader;               // your WaterRipple sim shader (the one that returns float4(diff,0,0,1))
    private Material rippleMat;

    [Header("Resolution")]
    public int resolution = 1024;

    [Header("Impulse / Object Texture")]
    public RenderTexture objRT;               // can be filled by another camera or script

    [Header("Layers")]
    public string simLayerName = "RippleSim"; // make this layer in Unity

    private RenderTexture CurrRT;
    private RenderTexture PrevRT;
    private RenderTexture TempRT;

    private Camera simCam;
    private GameObject simQuadGO;
    private Renderer waterRenderer;

    private int simLayer;

    private void Awake()
    {
        waterRenderer = GetComponent<Renderer>();
    }

    private void Start()
    {
        // 1) Find or create layer index
        simLayer = LayerMask.NameToLayer(simLayerName);
        if (simLayer < 0)
        {
            Debug.LogWarning($"Layer '{simLayerName}' not found. Please create it in the Tags & Layers settings.");
            simLayer = 0; // default layer fallback
        }

        // 2) Create sim material
        rippleMat = new Material(rippleShader);

        // 3) Create simulation RTs
        CurrRT = CreateRFloatRT(resolution, "Ripple_CurrRT");
        PrevRT = CreateRFloatRT(resolution, "Ripple_PrevRT");
        TempRT = CreateRFloatRT(resolution, "Ripple_TempRT");

        // initialize them to black
        Graphics.Blit(Texture2D.blackTexture, CurrRT);
        Graphics.Blit(Texture2D.blackTexture, PrevRT);
        Graphics.Blit(Texture2D.blackTexture, TempRT);

        // 4) Create hidden camera for simulation
        CreateSimCamera();

        // 5) Create full-screen quad for the sim camera to render
        CreateSimQuad();

        // 6) Send CurrRT to the visible water material as "_RippleTex"
        waterRenderer.material.SetTexture("_RippleTex", CurrRT);

        // 7) Start sim loop
        StartCoroutine(SimLoop());
    }

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

    private void CreateSimCamera()
    {
        GameObject camGO = new GameObject("RippleSimCamera");
        //camGO.transform.SetParent(transform, worldPositionStays: false);

        // Put camera above origin of this object
        camGO.transform.localPosition = new Vector3(1, 1, 0);   // local Y up
        camGO.transform.localRotation = Quaternion.Euler(90, 0, 0); // look down -Y in world, adjust as needed

        simCam = camGO.AddComponent<Camera>();
        simCam.enabled = false; // we will call Render() manually

        simCam.orthographic = true;
        simCam.orthographicSize = 1; // because our quad will be 1x1 in local space
        simCam.nearClipPlane = 0.1f;
        simCam.farClipPlane = 1000f;

        // Only render the sim layer
        simCam.cullingMask = 1 << simLayer;

        // We’ll assign targetTexture each frame before Render()
        simCam.clearFlags = CameraClearFlags.SolidColor;
        simCam.backgroundColor = Color.black;
    }

    private void CreateSimQuad()
    {
        simQuadGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        simQuadGO.name = "RippleSimQuad";

        // Put it as a child of the sim camera so it always fills view
        simQuadGO.transform.SetParent(simCam.transform, worldPositionStays: false);

        // Position directly in front of camera
        simQuadGO.transform.localPosition = new Vector3(0, 0, 1);  // in front of camera
        simQuadGO.transform.localRotation = Quaternion.identity;

        float h = 2f * simCam.orthographicSize;
        float w = h * ((float)resolution / resolution); // 1:1 aspect
        simQuadGO.transform.localScale = new Vector3(w, h, 1);
        // Set layer so only simCam sees it
        simQuadGO.layer = simLayer;

        // Assign sim material
        var quadRenderer = simQuadGO.GetComponent<MeshRenderer>();
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;
        quadRenderer.material = rippleMat;
    }

    private IEnumerator SimLoop()
    {
        while (true)
        {
            // 1) Bind RTs to sim material so shader can read them
            rippleMat.SetTexture("_currRT", CurrRT);
            rippleMat.SetTexture("_prevRT", PrevRT);
            rippleMat.SetTexture("_objRT", objRT);

            // 2) Render simQuad with simCam into TempRT
            simCam.targetTexture = TempRT;
            simCam.Render();  // This executes your sim fragment over the whole RT

            // 3) Ping-pong RTs (Temp becomes new Curr)
            var oldPrev = PrevRT;
            PrevRT = CurrRT;
            CurrRT = TempRT;
            TempRT = oldPrev;

            // 4) Feed updated CurrRT to visible water material
            waterRenderer.material.SetTexture("_RippleTex", CurrRT);

            yield return null; // next frame
        }
    }

    private void OnDestroy()
    {
        if (CurrRT != null) CurrRT.Release();
        if (PrevRT != null) PrevRT.Release();
        if (TempRT != null) TempRT.Release();
    }

    //private void OnGUI()
    //{
    //    if (objRT != null)
    //    {
    //        GUI.DrawTexture(new Rect(10, 10, 256, 256), objRT, ScaleMode.ScaleToFit, false);
    //    }
    //    if (TempRT != null)
    //    {
    //        GUI.DrawTexture(new Rect(300, 10, 256, 256), TempRT, ScaleMode.ScaleToFit, false);
    //    }
    //}
}
