using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ReflectionCamera : MonoBehaviour
{
    public Camera reflectionCamera;
    public RenderTexture reflectionRenderTexture;
    public int reflectionResolution = 512;
    public float clipPlaneOffset = 0.05f;
    public LayerMask reflectionMask = ~0; // exclude Water layer here

    Material mat;
    int lastW = -1, lastH = -1;

    void Awake()
    {
        mat = GetComponent<Renderer>().material;
        
        var main = Camera.main;
        // --- Ensure RT exists and is created ---
        int h = reflectionResolution;
        int w = Mathf.RoundToInt(h * (main.pixelWidth / (float)main.pixelHeight));

        reflectionRenderTexture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
        reflectionRenderTexture.name = "ReflectionRT";
        reflectionRenderTexture.wrapMode = TextureWrapMode.Clamp;
        reflectionRenderTexture.Create();
        lastW = w; lastH = h;
        // URP: make sure reflection camera has this component
        //if (reflectionCamera && reflectionCamera.GetComponent<UniversalAdditionalCameraData>() == null)
        //{
        //    var data = reflectionCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        //    data.renderShadows = false;
        //    data.renderPostProcessing = false;
        //    data.requiresColorTexture = false;
        //    data.requiresDepthTexture = false;
        //}
    }

    void LateUpdate()
    {
        reflectionCamera.targetTexture = reflectionRenderTexture;
        float waterY = transform.position.y;
        var camPos = Camera.main.transform.position;

        reflectionCamera.transform.position = new Vector3(camPos.x, 2f * waterY - camPos.y, camPos.z);

        var e = Camera.main.transform.eulerAngles;
        reflectionCamera.transform.rotation = Quaternion.Euler(-e.x, e.y, 0);

        // oblique clip plane so it doesn't render below water
        Vector3 planePoint = new Vector3(0f, waterY, 0f);
        Vector3 planeNormal = Vector3.up;

        Vector4 clipPlane = CameraSpacePlane(reflectionCamera, planePoint, planeNormal, 1.0f, clipPlaneOffset);
        reflectionCamera.projectionMatrix = reflectionCamera.CalculateObliqueMatrix(clipPlane);

        reflectionCamera.Render();

        mat.SetTexture("_reflectionTexture", reflectionRenderTexture);
    }

    static Vector4 CameraSpacePlane(Camera cam, Vector3 planePoint, Vector3 planeNormal, float sideSign, float offset)
    {
        Vector3 pos = planePoint + planeNormal * offset;
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cPos = m.MultiplyPoint(pos);
        Vector3 cNormal = m.MultiplyVector(planeNormal).normalized * sideSign;

        return new Vector4(cNormal.x, cNormal.y, cNormal.z, -Vector3.Dot(cPos, cNormal));
    }
    private void OnGUI()
    {
        if (reflectionRenderTexture != null)
        {
            GUI.DrawTexture(new Rect(10, 10, 512, 512), reflectionRenderTexture, ScaleMode.ScaleToFit, false);
        }
    }
}
