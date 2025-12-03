//using UnityEngine;
//using UnityEngine.Rendering;
//using static Unity.Burst.Intrinsics.X86.Avx;

//public class DeferredShadow : MonoBehaviour
//{
//    private Camera lightCamera;
//    private RenderTexture shadowMap;
//    private void Update()
//    {
//        //reg all lights
//        var indivLights = GameObject.FindObjectsByType<IndivLightObject>(FindObjectsSortMode.None);

//        CommandBuffer cmd = new CommandBuffer();
//        if (lightCamera == null)
//        {
//            var L = indivLights[0];
//            if (L.type == IndivLightObject.Type.direction)
//            {

//                shadowMap = new RenderTexture(1024, 1024, 24, RenderTextureFormat.Depth);
//                shadowMap.Create();
//                GameObject lightCamObject = new GameObject("Light Camera");
//                lightCamera = lightCamObject.AddComponent<Camera>();
//                //lightCamera.enabled = false;
//                lightCamera.clearFlags = CameraClearFlags.Depth;
//                lightCamera.backgroundColor = Color.white;
//                lightCamera.targetTexture = shadowMap;
//                //lightCamera.targetDisplay = 2;

//                //Configure camera type
//                lightCamera.nearClipPlane = 0.1f;
//                lightCamera.farClipPlane = 10f;
//                lightCamera.orthographic = true;
//                lightCamera.orthographicSize = 30;
//                lightCamObject.transform.SetParent(L.transform, false);
//            }
//        }
            
        
//        //sync shadow camera with light transform
//        lightCamera.transform.position = indivLights[0].transform.position;
//        lightCamera.transform.forward = indivLights[0].GetDirection();

//        //render shadow map manually
//        lightCamera.Render();

//        //Calculate light's view-projection matrix
//        Matrix4x4 lightViewProjMatrix = lightCamera.projectionMatrix * lightCamera.worldToCameraMatrix;

//        //send shadow data to shader
//        cmd.SetGlobalTexture("_ShadowMap", shadowMap);
//        cmd.SetGlobalMatrix("_LightVP", lightViewProjMatrix);
//    }
//    private void OnGUI()
//    {
//        GUI.DrawTexture(new Rect(10, 10, 512, 512), shadowMap, ScaleMode.ScaleToFit, false);
//    }
//}
