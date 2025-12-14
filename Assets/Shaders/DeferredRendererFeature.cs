using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class DeferredRendererFeature : RenderPipeline
{
    private Material gbufferMaterial;
    private Material lightingMaterial;
    private Material debugBlitMaterial;

    private const int MaxLights = 100;
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

    private RenderTexture _directionalShadowMap;
    private const int ShadowMapSize = 2048;

    enum ShadowTextureType
    {
        None = 0,
        Shadow2D = 1,
        ShadowCube = 2
    }

    struct ShadowData
    {
        public ShadowTextureType type;
        public RenderTexture shadowTex;

        public Matrix4x4 lightVP;     // for directional + spot
        public Matrix4x4[] cubeVPs;   // for point lights (unused now)

        public float farPlane;        // for cube shadows later
    }
    ShadowData[] shadowDatas = new ShadowData[32];
    int shadowCount = 0;

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
        {
            RenderCamera(context, cam);
        }
    }
    private void EnsureDirectionalShadowMap()
    {
        if (_directionalShadowMap != null &&
            _directionalShadowMap.width == ShadowMapSize &&
            _directionalShadowMap.height == ShadowMapSize)
            return;

        if (_directionalShadowMap != null)
        {
            _directionalShadowMap.Release();
            _directionalShadowMap = null;
        }

        _directionalShadowMap = new RenderTexture(ShadowMapSize, ShadowMapSize, 0, RenderTextureFormat.RFloat)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DirectionalShadowMap"
        };
        _directionalShadowMap.Create();
    }

    private void RenderCamera(ScriptableRenderContext context, Camera camera)
    {
        // CULLING
        if (!camera.TryGetCullingParameters(out var cullParams))
            camera.TryGetCullingParameters(out cullParams); // force fallback

        var cullResults = context.Cull(ref cullParams);

#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
        {
            // IMPORTANT: setup camera props first
            context.SetupCameraProperties(camera);

            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);

            var sceneSorting = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var sceneDrawing = new DrawingSettings(new ShaderTagId("GBuffer"), sceneSorting);
            var sceneFiltering = new FilteringSettings(RenderQueueRange.opaque);

            context.DrawRenderers(cullResults, ref sceneDrawing, ref sceneFiltering);

            context.DrawSkybox(camera);
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);

            context.Submit();
            return;
        }
#endif

        RenderTargetIdentifier camTarget =
    camera.targetTexture != null
        ? new RenderTargetIdentifier(camera.targetTexture)
        : BuiltinRenderTextureType.CameraTarget;

        // 2. --- SETUP CAMERA ---
        context.SetupCameraProperties(camera);

        int w = camera.pixelWidth;
        int h = camera.pixelHeight;

        // 3. --- CREATE GBuffers + Depth ---
        RenderTexture g0 = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        RenderTexture g1 = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        RenderTexture g2 = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBHalf); //half is jus cuz its oni for world pos
        RenderTexture depth = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.Depth);

        RenderTargetIdentifier[] mrt =
        {
            new RenderTargetIdentifier(g0),
            new RenderTargetIdentifier(g1),
            new RenderTargetIdentifier(g2)
        };
        //reg all lights
        var indivLights = DefferedLighObj.All;
        int count = Mathf.Min(indivLights.Count, MaxLights);
        CommandBuffer cmd = CommandBufferPool.Get("ShadowPass");

        //shadowCount = 0;
        //for (int i = 0; i < count; i++)
        //{
        //    var L = indivLights[i];
        //    if (L.type != DefferedLighObj.Type.direction) continue;

        //    // For now: use only the first directional light for shadows
        //    EnsureDirectionalShadowMap();

        //    // Build VP from light POV
        //    float3 pos = L.transform.position;
        //    float3 target = L.direction.normalized;

        //    float3 referenceUp = Vector3.up;
        //    if (Mathf.Abs(Vector3.Dot(target, referenceUp)) > 0.99f)
        //        referenceUp = Vector3.forward;

        //    Vector3 right = Vector3.Normalize(Vector3.Cross(referenceUp, target));
        //    Vector3 up = Vector3.Cross(target, right);
        //    up = L.transform.up; // your original override

        //    float3 focus = pos;
        //    float shadowDistance = 50f;
        //    float3 lightPos = focus;

        //    Matrix4x4 view = Matrix4x4.LookAt(lightPos, lightPos + target, up);

        //    float halfSize = 30f;
        //    float near = 0.1f;
        //    float far = shadowDistance * 2f;

        //    Matrix4x4 proj = Matrix4x4.Ortho(-halfSize, halfSize, -halfSize, halfSize, near, far);
        //    proj = GL.GetGPUProjectionMatrix(proj, true);

        //    Matrix4x4 vp = proj * view;

        //    shadowDatas[shadowCount].shadowTex = _directionalShadowMap;
        //    shadowDatas[shadowCount].lightVP = vp;

        //    // Bind depth-only target
        //    cmd.SetRenderTarget(_directionalShadowMap);
        //    cmd.ClearRenderTarget(true, false, Color.clear);
        //    cmd.SetGlobalMatrix("_LightVP", vp);
        //    cmd.SetGlobalVector("_LightPos", L.transform.position);
        //    cmd.SetGlobalMatrix("_LightView", view);

        //    context.ExecuteCommandBuffer(cmd);
        //    cmd.Clear();

        //    var sort = new SortingSettings(camera);
        //    var draw = new DrawingSettings(new ShaderTagId("ShadowCaster"), sort);
        //    var filter = new FilteringSettings(RenderQueueRange.all);

        //    context.DrawRenderers(cullResults, ref draw, ref filter);

        //    shadowCount++;
        //    break; // only one directional shadow for now
        //}
        //// Set texture + matrix
        //if (shadowCount > 0)
        //{
        //    cmd.SetGlobalTexture("_ShadowMap", shadowDatas[0].shadowTex);
        //    cmd.SetGlobalMatrix("_LightVP", shadowDatas[0].lightVP);
        //}

        // Execute and release
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);

        //Debug.Log(count);
        
        cmd = CommandBufferPool.Get("GBufferPass");

        // Make sure viewport covers the full camera
        // incase previous code change viewport
        cmd.SetViewport(camera.pixelRect);

        // Bind MRT with a real depth buffer
        cmd.SetRenderTarget(mrt, depth.depthBuffer);
        cmd.ClearRenderTarget(true, true, Color.clear);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        // 4. --- DRAW SCENE INTO GBuffers ---
        var sorting = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
        var drawing = new DrawingSettings(new ShaderTagId("GBuffer"), sorting);
        //drawing.overrideMaterial = gbufferMaterial;
        var filtering = new FilteringSettings(RenderQueueRange.opaque);

        context.DrawRenderers(cullResults, ref drawing, ref filtering);

        CommandBufferPool.Release(cmd);
        //context.DrawSkybox(camera);



        // 5. --- LIGHTING PASS (Fullscreen) ---
        cmd = CommandBufferPool.Get("LightingPass");

        // Make sure viewport is correct again
        cmd.SetViewport(camera.pixelRect);

        cmd.SetGlobalMatrix("_CameraInverseProjection", camera.projectionMatrix.inverse);
        cmd.SetGlobalMatrix("_CameraInverseView", camera.cameraToWorldMatrix);

        cmd.SetGlobalTexture("_GBuffer0", g0);
        cmd.SetGlobalTexture("_GBuffer1", g1);
        cmd.SetGlobalTexture("_GBuffer2", g2);
        cmd.SetGlobalTexture("_CameraDepthTexture", depth);
         
        //Lights 
        {
            for (int i = 0; i < count; i++)
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

            // send to shader
            cmd.SetGlobalInt("_LightCount", count);
            cmd.SetGlobalVectorArray("_LightPositions", _lightPositions);
            cmd.SetGlobalVectorArray("_LightDirections", _lightDirections);
            cmd.SetGlobalVectorArray("_LightColors", _lightColors);
            cmd.SetGlobalVectorArray("_LightIntensities", _lightIntensities);
            cmd.SetGlobalVectorArray("_LightTypes", _lightTypes);
            cmd.SetGlobalVectorArray("_LightSpecularStrength", _lightSpecularStrength);
            cmd.SetGlobalVectorArray("_LightSmoothness", _lightSmoothness);
            cmd.SetGlobalVectorArray("_LightRange", _lightRange);

            // attenuation + spot info
            cmd.SetGlobalVectorArray("_LightAttenuations", _lightAttenuations);
            cmd.SetGlobalVectorArray("_SpotAngles", _lightSpotData);
        }

        // render to the camera target
        cmd.SetRenderTarget(camTarget);
        cmd.ClearRenderTarget(true, true, Color.black);

        // Fullscreen triangle (no Blit)
        cmd.DrawProcedural(
            Matrix4x4.identity,
            lightingMaterial,
            0,
            MeshTopology.Triangles,
            3,3
        );

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);



        //cmd = CommandBufferPool.Get("ForwardTransparentPass");
        //cmd.SetRenderTarget(camTarget, depth);   // render over lit opaque scene
        //cmd.SetViewport(camera.pixelRect);
        //context.ExecuteCommandBuffer(cmd);
        //cmd.Clear();

        //// Sort + draw transparents
        //var forwardSorting = new SortingSettings(camera)
        //{
        //    criteria = SortingCriteria.CommonTransparent
        //};
        //var forwardDrawing = new DrawingSettings(new ShaderTagId("ForwardTransparent"), forwardSorting);
        //var forwardFiltering = new FilteringSettings(RenderQueueRange.transparent);

        //context.DrawRenderers(cullResults, ref forwardDrawing, ref forwardFiltering);

        //CommandBufferPool.Release(cmd);

        //cmd = CommandBufferPool.Get("FinalBlit");
        //cmd.Blit(lightingResultRT, camTarget);
        //context.ExecuteCommandBuffer(cmd);
        //cmd.Clear();
        //CommandBufferPool.Release(cmd);

#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
#endif
        context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
        context.DrawGizmos(camera, GizmoSubset.PostImageEffects);


        //cmd = CommandBufferPool.Get("Debug GBuffer View");

        //float fullW = camera.pixelWidth;
        //float fullH = camera.pixelHeight;
        //float thirdH = fullH / 3f;

        //// --- GBUFFER 0 (top) ---
        //cmd.SetViewport(new Rect(0, fullH - thirdH, fullW / 2, thirdH));
        //cmd.SetGlobalTexture("_SourceTex", g0);
        //cmd.DrawProcedural(Matrix4x4.identity, debugBlitMaterial, 0, MeshTopology.Triangles, 3, 1);

        //// --- GBUFFER 1 (middle) ---
        //cmd.SetViewport(new Rect(0, fullH - thirdH * 2f, fullW / 2, thirdH));
        //cmd.SetGlobalTexture("_SourceTex", g1);
        //cmd.DrawProcedural(Matrix4x4.identity, debugBlitMaterial, 0, MeshTopology.Triangles, 3, 1);

        //// --- GBUFFER 2 (bottom) ---
        //cmd.SetViewport(new Rect(0, 0, fullW / 2, thirdH));
        //cmd.SetGlobalTexture("_SourceTex", shadowDatas[0].shadowTex);
        //cmd.DrawProcedural(Matrix4x4.identity, debugBlitMaterial, 0, MeshTopology.Triangles, 3, 1);

        //context.ExecuteCommandBuffer(cmd);
        //CommandBufferPool.Release(cmd);








        // 6. --- CLEANUP ---
        RenderTexture.ReleaseTemporary(g0);
        RenderTexture.ReleaseTemporary(g1);
        RenderTexture.ReleaseTemporary(g2);
        RenderTexture.ReleaseTemporary(depth);

        context.Submit();
    }
}
