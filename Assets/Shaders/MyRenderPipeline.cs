using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/My Render Pipeline")]
public class MyRenderPipeline : RenderPipelineAsset<DeferredRendererFeature>
{
    // Assign these in inspector later
    public Material gbufferMaterial;
    public Material lightingMaterial;
    public Material debugBlitMaterial;

    protected override RenderPipeline CreatePipeline()
    {
        return new DeferredRendererFeature(gbufferMaterial, lightingMaterial, debugBlitMaterial);
    }

}
