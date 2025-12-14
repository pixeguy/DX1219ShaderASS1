using UnityEngine;

public class DirectionalCam : MonoBehaviour
{
    public Transform cam;
    public Vector3 offset;

    private void Update()
    {
        transform.position = cam.position + offset;
    }
}
