using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DeferredRendererFeature : RenderPipeline
{
    private readonly Material gbufferMaterial;   // optional (you currently use ShaderTagId)
    private readonly Material lightingMaterial;
    private readonly Material debugBlitMaterial; // kept, unused unless you re-enable debug

    private const int MaxLights = 100;

    // Light data packed as Vector4 arrays (matches your current shader interface)
    private readonly Vector4[] _lightPositions = new Vector4[MaxLights];
    private readonly Vector4[] _lightDirections = new Vector4[MaxLights];
    private readonly Vector4[] _lightColors = new Vector4[MaxLights];
    private readonly Vector4[] _lightIntensities = new Vector4[MaxLights];
    private readonly Vector4[] _lightTypes = new Vector4[MaxLights];
    private readonly Vector4[] _lightAttenuations = new Vector4[MaxLights];
    private readonly Vector4[] _lightSpotData = new Vector4[MaxLights];
    private readonly Vector4[] _lightSpecularStrength = new Vector4[MaxLights];
    private readonly Vector4[] _lightRange = new Vector4[MaxLights];
    private readonly Vector4[] _lightSmoothness = new Vector4[MaxLights];

    public DeferredRendererFeature(Material gMat, Material lMat, Material dMat)
    {
        gbufferMaterial = gMat;
        lightingMaterial = lMat;
        debugBlitMaterial = dMat;

        GraphicsSettings.useScriptableRenderPipelineBatching = false;
    }

    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        foreach (var cam in cameras)
            RenderCamera(context, cam);
    }

    private void RenderCamera(ScriptableRenderContext context, Camera camera)
    {
        // ---- Culling ----
        if (!camera.TryGetCullingParameters(out var cullParams))
            camera.TryGetCullingParameters(out cullParams);

        var cullResults = context.Cull(ref cullParams);

#if UNITY_EDITOR
        // SceneView path (kept similar to your original)
        if (camera.cameraType == CameraType.SceneView)
        {
            context.SetupCameraProperties(camera);
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);

            var sort = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var draw = new DrawingSettings(new ShaderTagId("GBuffer"), sort);
            var filt = new FilteringSettings(RenderQueueRange.opaque);

            context.DrawRenderers(cullResults, ref draw, ref filt);
            context.DrawSkybox(camera);

            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);

            context.Submit();
            return;
        }
#endif

        // ---- Camera target ----
        RenderTargetIdentifier camTarget =
            camera.targetTexture != null
                ? new RenderTargetIdentifier(camera.targetTexture)
                : BuiltinRenderTextureType.CameraTarget;

        context.SetupCameraProperties(camera);

        int w = camera.pixelWidth;
        int h = camera.pixelHeight;

        // ---- GBuffer MRT + Depth ----
        RenderTexture g0 = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        RenderTexture g1 = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        RenderTexture g2 = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBHalf); // world pos, etc.
        RenderTexture depth = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.Depth);

        var mrt = new RenderTargetIdentifier[]
        {
            new RenderTargetIdentifier(g0),
            new RenderTargetIdentifier(g1),
            new RenderTargetIdentifier(g2),
        };

        // ---- GBuffer pass ----
        {
            CommandBuffer cmd = CommandBufferPool.Get("GBufferPass");
            cmd.SetViewport(camera.pixelRect);
            cmd.SetRenderTarget(mrt, depth.depthBuffer);
            cmd.ClearRenderTarget(true, true, Color.clear);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);

            var sorting = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var drawing = new DrawingSettings(new ShaderTagId("GBuffer"), sorting);
            // drawing.overrideMaterial = gbufferMaterial; // keep disabled like your original
            var filtering = new FilteringSettings(RenderQueueRange.opaque);

            context.DrawRenderers(cullResults, ref drawing, ref filtering);
        }

        // ---- Lighting pass (fullscreen triangle) ----
        {
            // gather lights from your manager list
            var indivLights = DefferedLighObj.All;
            int lightCount = Mathf.Min(indivLights.Count, MaxLights);

            CommandBuffer cmd = CommandBufferPool.Get("LightingPass");
            cmd.SetViewport(camera.pixelRect);

            cmd.SetGlobalMatrix("_CameraInverseProjection", camera.projectionMatrix.inverse);
            cmd.SetGlobalMatrix("_CameraInverseView", camera.cameraToWorldMatrix);

            cmd.SetGlobalTexture("_GBuffer0", g0);
            cmd.SetGlobalTexture("_GBuffer1", g1);
            cmd.SetGlobalTexture("_GBuffer2", g2);
            cmd.SetGlobalTexture("_CameraDepthTexture", depth);

            for (int i = 0; i < lightCount; i++)
            {
                var L = indivLights[i];

                _lightPositions[i] = L.transform.position;
                _lightDirections[i] = L.direction.normalized;
                _lightColors[i] = new Vector4(L.lightColor.r, L.lightColor.g, L.lightColor.b, 1);
                _lightIntensities[i] = new Vector4(L.intensity, 0, 0, 0);
                _lightTypes[i] = new Vector4((int)L.type, 0, 0, 0);
                _lightAttenuations[i] = new Vector4(L.attenuation.x, L.attenuation.y, L.attenuation.z, 0);
                _lightSpotData[i] = new Vector4(L.spotLightInnerCutOff, L.spotLightCutOff, 0, 0);
                _lightSpecularStrength[i] = new Vector4(L.specularStrength, 0, 0, 0);
                _lightRange[i] = new Vector4(L.range, 0, 0, 0);
                _lightSmoothness[i] = new Vector4(L.smoothness, 0, 0, 0);
            }

            cmd.SetGlobalInt("_DLightCount", lightCount);
            cmd.SetGlobalVectorArray("_DLightPositions", _lightPositions);
            cmd.SetGlobalVectorArray("_DLightDirections", _lightDirections);
            cmd.SetGlobalVectorArray("_DLightColors", _lightColors);
            cmd.SetGlobalVectorArray("_DLightIntensities", _lightIntensities);
            cmd.SetGlobalVectorArray("_DLightTypes", _lightTypes);
            cmd.SetGlobalVectorArray("_DLightSpecularStrength", _lightSpecularStrength);
            cmd.SetGlobalVectorArray("_DLightSmoothness", _lightSmoothness);
            cmd.SetGlobalVectorArray("_DLightRange", _lightRange);
            cmd.SetGlobalVectorArray("_DLightAttenuations", _lightAttenuations);
            cmd.SetGlobalVectorArray("_DSpotAngles", _lightSpotData);

            cmd.SetRenderTarget(camTarget);
            cmd.ClearRenderTarget(true, true, Color.black);

            cmd.DrawProcedural(
                Matrix4x4.identity,
                lightingMaterial,
                0,
                MeshTopology.Triangles,
                3, 3
            );

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

#if UNITY_EDITOR
        context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
        context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
#endif

        // ---- Cleanup ----
        RenderTexture.ReleaseTemporary(g0);
        RenderTexture.ReleaseTemporary(g1);
        RenderTexture.ReleaseTemporary(g2);
        RenderTexture.ReleaseTemporary(depth);

        context.Submit();
    }
}
