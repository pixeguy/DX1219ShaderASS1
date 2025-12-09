using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class DeferredRendererFeature : RenderPipeline
{
    private Material gbufferMaterial;
    private Material lightingMaterial;
    private Material debugBlitMaterial;


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

        RenderTextureDescriptor lightingDesc = new RenderTextureDescriptor(w, h);
        lightingDesc.colorFormat = RenderTextureFormat.ARGB32;
        lightingDesc.depthBufferBits = 0;               // no depth needed in final color
        lightingDesc.msaaSamples = 1;
        lightingDesc.sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear);

        RenderTexture lightingResultRT = RenderTexture.GetTemporary(lightingDesc);


        //reg all lights
        var indivLights = GameObject.FindObjectsByType<IndivLightObject>(FindObjectsSortMode.None);

        // TEMP: support up to 32 lights
        int count = Mathf.Min(indivLights.Length, 32);

        Vector4[] positions = new Vector4[32];
        Vector4[] directions = new Vector4[32];
        Vector4[] colors = new Vector4[32];
        Vector4[] intensities = new Vector4[32];
        Vector4[] types = new Vector4[32];
        Vector4[] radii = new Vector4[32];     // NEW: range for point/spot lights
        Vector4[] spotData = new Vector4[32];     // NEW: spot angles
        Vector4[] specularStrengths = new Vector4[32];
        Vector4[] smoothnessValues = new Vector4[32];

        CommandBuffer cmd = CommandBufferPool.Get("ShadowPass");

        shadowCount = 0;
        for (int i = 0; i < indivLights.Length; i++)
        {
            var L = indivLights[i];
            if (L.type != IndivLightObject.Type.direction) continue;

            var rt = new RenderTexture(2048, 2048, 0, RenderTextureFormat.RFloat);

            rt.filterMode = FilterMode.Bilinear;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.Create();

            // Build VP from light POV
            float3 pos = L.transform.position;
            float3 target = -L.direction.normalized;
            Vector3 up = Vector3.forward;

            // Choose a point in the scene we care about (around the camera)
            float3 lightPos = pos;
                
            float dist = 50f;

            // Build view from light
            Matrix4x4 view = Matrix4x4.LookAt(lightPos, lightPos + target, up);
            float halfSize = 30f;
            Matrix4x4 proj = Matrix4x4.Ortho(-halfSize, halfSize, -halfSize, halfSize, 0.1f, dist);
            //proj = GL.GetGPUProjectionMatrix(proj, true);
            Matrix4x4 vp = proj * view;

            shadowDatas[shadowCount].shadowTex = rt;
            shadowDatas[shadowCount].lightVP = vp;

            // Bind depth-only target
            cmd.SetRenderTarget(rt);
            cmd.ClearRenderTarget(true, false, Color.clear);
            cmd.SetGlobalMatrix("_LightVP", vp);
            cmd.SetGlobalVector("_LightPos", L.transform.position);
            cmd.SetGlobalMatrix("_LightView", view);

            // MUST EXECUTE this before DrawRenderers
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();


            // Draw all objects using ShadowCaster pass
            var sort = new SortingSettings(camera);
            var draw = new DrawingSettings(new ShaderTagId("ShadowCaster"), sort);
            var filter = new FilteringSettings(RenderQueueRange.all);

            context.DrawRenderers(cullResults, ref draw, ref filter);

            shadowCount++;
        }
        // Set texture + matrix
        if (shadowCount > 0)
        {
            cmd.SetGlobalTexture("_ShadowMap", shadowDatas[0].shadowTex);
            cmd.SetGlobalMatrix("_LightVP", shadowDatas[0].lightVP);
        }

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
            // fill arrays
            for (int i = 0; i < count; i++)
            {
                var L = indivLights[i];

                positions[i] = L.transform.position;
                directions[i] = L.direction.normalized;
                colors[i] = new Vector4(L.lightColor.r, L.lightColor.g, L.lightColor.b, 1);
                intensities[i] = new Vector4(L.intensity, 0, 0, 0);

                types[i] = new Vector4((int)L.type, 0, 0, 0);

                radii[i] = new Vector4(L.attenuation.x, L.attenuation.y, L.attenuation.z, 0);

                spotData[i] = new Vector4(L.spotLightInnerCutOff, L.spotLightCutOff, 0, 0);

                specularStrengths[i] = new Vector4(L.specularStrength, 0, 0, 0);
                smoothnessValues[i] = new Vector4(L.smoothness, 0, 0, 0);
            }

            // send to shader
            cmd.SetGlobalInt("_LightCount", count);
            cmd.SetGlobalVectorArray("_LightPositions", positions);
            cmd.SetGlobalVectorArray("_LightDirections", directions);
            cmd.SetGlobalVectorArray("_LightColors", colors);
            cmd.SetGlobalVectorArray("_LightIntensities", intensities);
            cmd.SetGlobalVectorArray("_LightTypes", types);
            cmd.SetGlobalVectorArray("_LightSpecularStrength", specularStrengths);
            cmd.SetGlobalVectorArray("_LightSmoothness", smoothnessValues);

            // if you want attenuation and spot info:
            cmd.SetGlobalVectorArray("_LightAttenuations", radii);
            cmd.SetGlobalVectorArray("_SpotAngles", spotData);
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



        cmd = CommandBufferPool.Get("ForwardTransparentPass");
        cmd.SetRenderTarget(camTarget, depth);   // render over lit opaque scene
        cmd.SetViewport(camera.pixelRect);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        // Sort + draw transparents
        var forwardSorting = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonTransparent
        };
        var forwardDrawing = new DrawingSettings(new ShaderTagId("ForwardTransparent"), forwardSorting);
        var forwardFiltering = new FilteringSettings(RenderQueueRange.transparent);

        context.DrawRenderers(cullResults, ref forwardDrawing, ref forwardFiltering);

        CommandBufferPool.Release(cmd);

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
        RenderTexture.ReleaseTemporary(lightingResultRT);

        context.Submit();
    }
}
