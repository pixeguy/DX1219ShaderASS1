using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ReflectionCamera : MonoBehaviour
{
    // ======================================================
    // References
    // ======================================================
    public Camera reflectionCamera;
    private RenderTexture reflectionRenderTexture;
    public int reflectionResolution = 512;
    public float clipPlaneOffset = 0.05f;

    private Material mat;

    private void Awake()
    {
        mat = GetComponent<Renderer>().material;

        // ------------------------------
        // Create RT (match main aspect)
        // ------------------------------
        var main = Camera.main;

        int h = reflectionResolution;
        int w = Mathf.RoundToInt(h * (main.pixelWidth / (float)main.pixelHeight));

        reflectionRenderTexture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
        reflectionRenderTexture.name = "ReflectionRT";
        reflectionRenderTexture.wrapMode = TextureWrapMode.Clamp;
        reflectionRenderTexture.Create();
    }

    private void LateUpdate()
    {
        // Set RT Target
        reflectionCamera.targetTexture = reflectionRenderTexture;

        // ------------------------------
        // Mirror camera transform across water plane
        // ------------------------------
        float waterY = transform.position.y;
        var camPos = Camera.main.transform.position;

        reflectionCamera.transform.position = new Vector3(camPos.x,2f * waterY - camPos.y,camPos.z);

        var camRot = Camera.main.transform.eulerAngles;
        reflectionCamera.transform.rotation = Quaternion.Euler(-camRot.x, camRot.y, 0);

        // ------------------------------
        // Oblique clip plane (cull below water)
        // ------------------------------
        Vector3 planePoint = new Vector3(0f, waterY, 0f);
        Vector3 planeNormal = Vector3.up;

        Vector4 clipPlane = CameraSpacePlane(
            reflectionCamera,
            planePoint,
            planeNormal,
            1.0f,
            clipPlaneOffset
        );

        reflectionCamera.projectionMatrix = reflectionCamera.CalculateObliqueMatrix(clipPlane);

        // ------------------------------
        // Render + assign
        // ------------------------------
        reflectionCamera.Render();
        mat.SetTexture("_reflectionTexture", reflectionRenderTexture);
    }

    private static Vector4 CameraSpacePlane(Camera cam, Vector3 planePoint, Vector3 planeNormal, float sideSign, float offset)
    {
        Vector3 pos = planePoint + planeNormal * offset;

        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cPos = m.MultiplyPoint(pos);
        Vector3 cNormal = m.MultiplyVector(planeNormal).normalized * sideSign;

        return new Vector4(cNormal.x, cNormal.y, cNormal.z, -Vector3.Dot(cPos, cNormal));
    }

    // ======================================================
    // Debug
    // ======================================================
    private void OnGUI()
    {
        if (reflectionRenderTexture != null)
        {
            //GUI.DrawTexture(new Rect(10, 10, 512, 512), reflectionRenderTexture, ScaleMode.ScaleToFit, false);
        }
    }
}
