using UnityEngine;

public class ShadowMapRenderer : MonoBehaviour
{
    [SerializeField]
    private IndivLightObject lightObject;
    [SerializeField]
    private int shadowMapResolution = 1024;
    [SerializeField]
    private float shadowBias = 0.005f;

    private Camera lightCamera;
    private RenderTexture shadowMap;


    private Camera pointLightCamera;
    private RenderTexture shadowCubeMap;

    private void Start()
    {
        pointLightCamera = new Camera();
        lightObject = GetComponent<IndivLightObject>();
        if (lightObject == null)
        {
            Debug.LogError("ShadowMapper requires a lightObject");
            return;
        }
        CreateLightCamera();
    }
    private void Update()
    {
        if ((lightCamera == null || shadowMap == null)/* || (pointLightCamera == null || shadowCubeMap == null)*/)
            return;

        UpdateLightCamera();
        SendShadowDataToShader();
    }

    private void CreateLightCamera()
    {
        if (lightObject.type != IndivLightObject.Type.point)
        {
            shadowMap = new RenderTexture(shadowMapResolution, shadowMapResolution, 24, RenderTextureFormat.Depth);
            shadowMap.Create();

            //Create shadow camera
            GameObject lightCamObject = new GameObject("Light Camera");
            lightCamera = lightCamObject.AddComponent<Camera>();
            lightCamera.enabled = false;
            lightCamera.clearFlags = CameraClearFlags.Depth;
            lightCamera.backgroundColor = Color.white;
            lightCamera.targetTexture = shadowMap;

            //Configure camera type
            lightCamera.nearClipPlane = 0.1f;
            lightCamera.farClipPlane = 100f;
            lightCamera.orthographic = true;
            lightCamera.orthographicSize = 30;

            lightCamObject.transform.SetParent(lightObject.transform, false);
        }
        else
        {
            shadowCubeMap = new RenderTexture(shadowMapResolution, shadowMapResolution, 24, RenderTextureFormat.Shadowmap);
            shadowCubeMap.dimension = UnityEngine.Rendering.TextureDimension.Cube;

            shadowCubeMap.Create();
            GameObject lightCamObject = new GameObject("Light Camera");
            pointLightCamera = lightCamObject.AddComponent<Camera>();
            pointLightCamera.enabled = false;
            pointLightCamera.clearFlags = CameraClearFlags.Depth;
            pointLightCamera.backgroundColor = Color.white;
            pointLightCamera.targetTexture = shadowCubeMap;
            //Configure camera type
            pointLightCamera.nearClipPlane = 0.1f;
            pointLightCamera.farClipPlane = 100f;
            pointLightCamera.orthographic = true;
            pointLightCamera.orthographicSize = 30;

            pointLightCamera.transform.SetParent(lightObject.transform, false);
            
        }
    }

    private void UpdateLightCamera()
    {
        if (lightObject.type != IndivLightObject.Type.point)
        {
            //sync shadow camera with light transform
            lightCamera.transform.position = lightObject.transform.position;
            lightCamera.transform.forward = lightObject.GetDirection();

            //render shadow map manually
            lightCamera.Render();
        }
        else
        {
            pointLightCamera.transform.position = lightObject.transform.position;
            pointLightCamera.transform.rotation = Quaternion.identity;
            //for (int i = 0; i < 6; i++)
            //{
            //    //sync shadow camera with light transform
            //    switch (i)
            //    {
            //        case 0:
            //            pointLightCamera.transform.forward = lightObject.transform.right;
            //            break;

            //        case 1:
            //            pointLightCamera.transform.forward = -lightObject.transform.right;
            //            break;

            //        case 2:
            //            pointLightCamera.transform.forward = lightObject.transform.up;
            //            break;

            //        case 3:
            //            pointLightCamera.transform.forward = -lightObject.transform.up;
            //            break;

            //        case 4:
            //            pointLightCamera.transform.forward = lightObject.transform.forward;
            //            break;

            //        case 5:
            //            pointLightCamera.transform.forward = -lightObject.transform.forward;
            //            break;
            //    }
            //}
            pointLightCamera.RenderToCubemap(shadowCubeMap, 63);
        }
    }

    private void SendShadowDataToShader()
    {
        Material material = lightObject.GetMaterial();
        if (material == null) return;

        if (lightObject.type != IndivLightObject.Type.point)
        {
            //Calculate light's view-projection matrix
            Matrix4x4 lightViewProjMatrix = lightCamera.projectionMatrix * lightCamera.worldToCameraMatrix;

            //send shadow data to shader
            material.SetTexture("_shadowMap", shadowMap);
            material.SetMatrix("_lightViewProj", lightViewProjMatrix);
        }
        else
        {
            material.SetTexture("_shadowCubeMap", shadowCubeMap);
            material.SetVector("_lightPosition", lightObject.transform.position);
            material.SetFloat("_shadowFarPlane", pointLightCamera.farClipPlane);
        }
        material.SetFloat("_shadowBias", shadowBias);
    }

    private void OnDestroy()
    {
        if (shadowMap != null)
        {
            shadowMap.Release();
        }
        if (lightCamera != null)
        {
            Destroy(lightCamera.gameObject);
        }
        if(shadowCubeMap != null)
        {
            shadowCubeMap.Release();
        }
        //if(pointLightCamera != null)
        //{
        //    Destroy(pointLightCamera.gameObject);
        //}
    }

    private void OnGUI()
    {
        GUI.DrawTexture(new Rect(10, 10, 512, 512),shadowMap, ScaleMode.ScaleToFit, false);
    }
}
