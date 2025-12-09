using UnityEngine;
using UnityEngine.Rendering;

public class SwitchingPipelines : MonoBehaviour
{
    [SerializeField] RenderPipelineAsset deferredAsset;
    [SerializeField] RenderPipelineAsset someOtherAsset; // e.g. URP or alt custom

    public void UseDeferred()
    {
        GraphicsSettings.defaultRenderPipeline = deferredAsset;
        QualitySettings.renderPipeline = deferredAsset;
    }

    public void UseOther()
    {
        GraphicsSettings.defaultRenderPipeline = someOtherAsset;
        QualitySettings.renderPipeline = someOtherAsset;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            UseDeferred();
        }
        else if (Input.GetKeyDown(KeyCode.W))
        {
            UseOther();
        }
    }
}
