using UnityEngine;

public class ShadowAtlasManager : MonoBehaviour
{
    public static ShadowAtlasManager Instance { get; private set; }

    // =========================
    // Atlas Settings
    // =========================
    [Header("Atlas Settings")]
    public int atlasSize = 4096;
    public int tilesPerRow = 4;                 // 4x4 grid
    public const int maxLights = 10;             // must match shaders & light system

    public RenderTexture Atlas { get; private set; }

    // =========================
    // Data sent to shaders
    // =========================
    private Matrix4x4[] lightVPs = new Matrix4x4[maxLights];
    private Vector4[] shadowAtlasUVs = new Vector4[maxLights];

    public float shadowRadius = 0.5f;

    private int lightCount = 0;

    // =========================
    // References
    // =========================
    [SerializeField] private ShadowMaterialSwapper materialSwapper;
    private ShadowMapRenderer[] shadowRenderers;

    // =========================
    // Lifecycle
    // =========================
    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Use ARGB32 so atlas tiling works correctly (depth RT breaks UV tiling)
        Atlas = new RenderTexture(atlasSize, atlasSize, 24, RenderTextureFormat.ARGB32);
        Atlas.useMipMap = false;
        Atlas.autoGenerateMips = false;
        Atlas.filterMode = FilterMode.Bilinear;
        Atlas.wrapMode = TextureWrapMode.Clamp;
        Atlas.Create();

        Shader.SetGlobalTexture("_ShadowAtlas", Atlas);
        Shader.SetGlobalFloat("_ShadowAtlasSize", atlasSize);

        shadowRenderers = FindObjectsOfType<ShadowMapRenderer>();
    }

    // =========================
    // Light registration
    // =========================
    public int RegisterLight()
    {
        if (lightCount >= maxLights)
        {
            Debug.Log("max lights reached");
            return -1;
        }

        return lightCount++;
    }

    // =========================
    // Atlas allocation
    // =========================
    public Rect AllocateTileForIndex(int index)
    {
        float tileSize = 1f / tilesPerRow;

        int x = index % tilesPerRow;
        int y = index / tilesPerRow;

        if (y >= tilesPerRow)
        {
            Debug.Log("not enough tiles on atlas grid");
            return new Rect(0, 0, 0, 0);
        }

        // Rect = (offsetX, offsetY, scaleX, scaleY)
        return new Rect(
            x * tileSize,
            y * tileSize,
            tileSize,
            tileSize
        );
    }

    // =========================
    // Per-light data update
    // =========================
    public void UpdateLightData(int lightIndex, Matrix4x4 lightVP, Vector4 atlasUVScaleOffset)
    {
        if (lightIndex < 0 || lightIndex >= maxLights)
            return;

        lightVPs[lightIndex] = lightVP;
        shadowAtlasUVs[lightIndex] = atlasUVScaleOffset;
    }

    private void Update()
    {
    }

    // =========================
    // Shadow rendering
    // =========================
    private void LateUpdate()
    {
        if (Atlas == null || shadowRenderers == null)
            return;

        // Clear atlas (white = far depth in inverted depth convention)
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = Atlas;
        GL.Clear(true, true, Color.white);
        RenderTexture.active = active;

        // Override all materials with depth-only material
        materialSwapper.BeginDepthOverride();

        // Render each light's shadow into its atlas tile
        foreach (var sr in shadowRenderers)
        {
            if (sr == null)
                continue;

            sr.RenderShadow();
        }

        // Restore original materials
        materialSwapper.EndDepthOverride();

        // Push shadow data to shaders
        Shader.SetGlobalMatrixArray("_lightViewProj", lightVPs);
        Shader.SetGlobalVectorArray("_shadowAtlasUV", shadowAtlasUVs);
        Shader.SetGlobalFloat("_shadowRadius", shadowRadius);
    }

    // =========================
    // Debug
    // =========================
    private void OnGUI()
    {
        if (Atlas != null)
        {
            //GUI.DrawTexture(
            //    new Rect(10, 10, 512, 512),
            //    Atlas,
            //    ScaleMode.ScaleToFit,
            //    false
            //);
        }
    }
}
