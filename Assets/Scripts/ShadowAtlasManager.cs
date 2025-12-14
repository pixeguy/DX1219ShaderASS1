using UnityEngine;

public class ShadowAtlasManager : MonoBehaviour
{
    public static ShadowAtlasManager Instance { get; private set; }

    [Header("Atlas Settings")]
    public int atlasSize = 4096;   
    public int tilesPerRow = 4; // 4x4 grid
    public const int maxLights = 10;

    public RenderTexture Atlas { get; private set; }

    // arrays sent to shader
    private Matrix4x4[] lightVPs = new Matrix4x4[maxLights];
    private Vector4[] shadowAtlasUVs = new Vector4[maxLights];

    private int lightCount = 0;

    [SerializeField] private ShadowMaterialSwapper materialSwapper;
    private ShadowMapRenderer[] shadowRenderers;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Atlas = new RenderTexture(atlasSize, atlasSize, 24, RenderTextureFormat.ARGB32);
        Atlas.useMipMap = false;
        Atlas.autoGenerateMips = false;
        Atlas.filterMode = FilterMode.Bilinear;
        Atlas.wrapMode = TextureWrapMode.Clamp;
        Atlas.Create();

        Shader.SetGlobalTexture("_ShadowAtlas", Atlas);
        Shader.SetGlobalFloat("_ShadowAtlasSize", (float)atlasSize);
        shadowRenderers = FindObjectsOfType<ShadowMapRenderer>();
    }
    public int RegisterLight()
    {
        if (lightCount >= maxLights)
        {
            Debug.LogWarning("Max light count reached for shadow atlas");
            return -1;
        }

        int index = lightCount++;
        return index;
    }
    public Rect AllocateTileForIndex(int index)
    {
        int idx = index; 

        float tileSize = 1f / tilesPerRow;
        int x = idx % tilesPerRow;
        int y = idx / tilesPerRow;

        if (y >= tilesPerRow)
        {
            Debug.LogWarning("Not enough tiles in shadow atlas grid!");
            return new Rect(0, 0, 0, 0);
        }

        return new Rect(x * tileSize, y * tileSize, tileSize, tileSize);
    }

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
    private void LateUpdate()
    {
        if (Atlas == null || shadowRenderers == null) return;

        // clear atlas
        var active = RenderTexture.active;
        RenderTexture.active = Atlas;
        GL.Clear(true, true, Color.white);
        RenderTexture.active = active;

        // change all obj to atlas depth mat
        materialSwapper.BeginDepthOverride();

        foreach (var sr in shadowRenderers)
        {
            if (sr == null) continue;
            sr.RenderShadow();
        }

        // change back mateerials
        materialSwapper.EndDepthOverride();

        Shader.SetGlobalMatrixArray("_lightViewProj", lightVPs);
        Shader.SetGlobalVectorArray("_shadowAtlasUV", shadowAtlasUVs);
    }

    private void OnGUI()
    {
        //if (ShadowAtlasManager.Instance != null && ShadowAtlasManager.Instance.Atlas != null)
        //{
        //    GUI.DrawTexture(new Rect(10, 10, 512, 512),
        //        ShadowAtlasManager.Instance.Atlas, ScaleMode.ScaleToFit, false);
        //}
    }
}