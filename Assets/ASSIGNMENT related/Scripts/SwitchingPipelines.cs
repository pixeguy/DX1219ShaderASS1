using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class SwitchingPipelines : MonoBehaviour
{
    [SerializeField] RenderPipelineAsset deferredAsset;
    [SerializeField] RenderPipelineAsset someOtherAsset; // URP or alt custom

    const string DeferredSceneName = "DeferredScene";
    const string OtherSceneName = "SampleScene";

    public void UseDeferred()
    {
        GraphicsSettings.defaultRenderPipeline = deferredAsset;
        QualitySettings.renderPipeline = deferredAsset;

        if (SceneManager.GetActiveScene().name != DeferredSceneName)
            SceneManager.LoadScene(DeferredSceneName);
    }

    public void UseOther()
    {
        GraphicsSettings.defaultRenderPipeline = someOtherAsset;
        QualitySettings.renderPipeline = someOtherAsset;

        if (SceneManager.GetActiveScene().name != OtherSceneName)
            SceneManager.LoadScene(OtherSceneName);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha2))
            UseDeferred();
        else if (Input.GetKeyDown(KeyCode.Alpha1))
            UseOther();
    }
}
