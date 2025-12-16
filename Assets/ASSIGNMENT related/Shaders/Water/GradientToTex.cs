using UnityEngine;

[ExecuteAlways]
public class GradientToTex : MonoBehaviour
{
    [Header("Gradient to Texture")]
    public Gradient depthGradient;
    [Range(16, 512)] public int width = 256;

    public AnimationCurve depthCurve = AnimationCurve.Linear(0, 0, 1, 1);

    // ======================================================
    // Target material
    // ======================================================
    [Header("Target material")]
    public Material waterMaterial;
    public string textureProperty = "_DepthGradient";

    // Texture to pass to Mat
    private Texture2D generatedTex;

    private void OnValidate()
    {
        BakeAndAssign();
    }

    private void Awake()
    {
        BakeAndAssign();
    }

    // ======================================================
    // Bake
    // ======================================================
    private void BakeAndAssign()
    {
        if (depthGradient == null || waterMaterial == null)
            return;

        // ------------------------------
        // Allocate / recreate texture
        // ------------------------------
        if (generatedTex == null || generatedTex.width != width)
        {
            if (generatedTex != null)
                DestroyImmediate(generatedTex);

            generatedTex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
            generatedTex.wrapMode = TextureWrapMode.Clamp;
            generatedTex.name = "WaterDepthGradientTex";
        }

        // ------------------------------
        // Bake pixels while following the curve
        // ------------------------------
        for (int x = 0; x < width; x++)
        {
            float t = x / (float)(width - 1);
            float u = depthCurve.Evaluate(t);

            Color c = depthGradient.Evaluate(u);
            generatedTex.SetPixel(x, 0, c);
        }

        // ------------------------------
        // Upload + assign
        // ------------------------------
        generatedTex.Apply();
        waterMaterial.SetTexture(textureProperty, generatedTex);
    }
}
