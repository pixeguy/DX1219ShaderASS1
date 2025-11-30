using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class DeferredRendererFeature : RenderPipeline
{
    private Material gbufferMaterial;
    private Material lightingMaterial;

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

    public DeferredRendererFeature(Material gMat, Material lMat)
    {
        gbufferMaterial = gMat;
        lightingMaterial = lMat;
        GraphicsSettings.useScriptableRenderPipelineBatching = false;

    }

    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        foreach (var cam in cameras)
        {
            Debug.Log("SRP rendering camera: " + cam.name + "  (" + cam.cameraType + ")");
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
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);

            // Forward fallback for SceneView
            var sceneSorting = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var sceneDrawing = new DrawingSettings(new ShaderTagId("UniversalForward"), sceneSorting);
            var sceneFiltering = new FilteringSettings(RenderQueueRange.all);

            context.DrawRenderers(cullResults, ref sceneDrawing, ref sceneFiltering);

            // Skybox and gizmos
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
        RenderTexture depth = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.Depth);

        RenderTargetIdentifier[] mrt =
        {
        new RenderTargetIdentifier(g0),
        new RenderTargetIdentifier(g1)
    };


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
        //shadow
        {
            //shadowCount = 0;

            //for (int i = 0; i < indivLights.Length; i++) //only render directional shadows for now
            //{
            //    var L = indivLights[i];

            //    if (L.type != IndivLightObject.Type.direction) continue;

            //    // --- Create shadow texture ---
            //    var rt = new RenderTexture(2048, 2048, 32, RenderTextureFormat.Depth);
            //    rt.filterMode = FilterMode.Bilinear;

            //    // --- Build view + projection ---
            //    float3 pos = L.transform.position;
            //    float3 dir = L.direction.normalized; // your custom vector
            //    float3 up = Vector3.up;

            //    Matrix4x4 view = Matrix4x4.LookAt(pos, pos + dir, up);
            //    Matrix4x4 proj = Matrix4x4.Ortho(-50, 50, -50, 50, 0.1f, 200f);

            //    Matrix4x4 vp = proj * view;

            //    // --- Fill shadowData ---
            //    shadowDatas[shadowCount].type = ShadowTextureType.Shadow2D;
            //    shadowDatas[shadowCount].shadowTex = rt;
            //    shadowDatas[shadowCount].lightVP = vp;

            //    // --- Render depth map ---
            //    cmd.SetRenderTarget(rt);
            //    cmd.ClearRenderTarget(true, true, Color.white);

            //    var shadowSort = new SortingSettings(camera);
            //    var shadowDraw = new DrawingSettings(new ShaderTagId("ShadowCaster"), shadowSort);
            //    var shadowFilter = new FilteringSettings(RenderQueueRange.opaque);

            //    context.DrawRenderers(cullResults, ref shadowDraw, ref shadowFilter);

            //    shadowCount++;
            //}
        }



        cmd = CommandBufferPool.Get("GBufferPass");

        // Make sure viewport covers the full camera
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
        context.DrawSkybox(camera);






        // 5. --- LIGHTING PASS (Fullscreen) ---
        cmd = CommandBufferPool.Get("LightingPass");

        // Make sure viewport is correct again
        cmd.SetViewport(camera.pixelRect);

        cmd.SetGlobalMatrix("_CameraInverseProjection", camera.projectionMatrix.inverse);
        cmd.SetGlobalMatrix("_CameraInverseView", camera.cameraToWorldMatrix);

        cmd.SetGlobalTexture("_GBuffer0", g0);
        cmd.SetGlobalTexture("_GBuffer1", g1);
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

                // RANGE (radius) for point & spot:
                radii[i] = new Vector4(L.attenuation.x, L.attenuation.y, L.attenuation.z, 0);

                // SPOTLIGHT extra data:
                spotData[i] = new Vector4(L.spotLightInnerCutOff, L.spotLightCutOff, 0, 0);

                // ★ NEW: import from your IndivLightObject exactly:
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
        cmd.ClearRenderTarget(true, true, Color.blue);

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

        // 6. --- CLEANUP ---
        RenderTexture.ReleaseTemporary(g0);
        RenderTexture.ReleaseTemporary(g1);
        RenderTexture.ReleaseTemporary(depth);

        context.Submit();
    }
}
