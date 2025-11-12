using UnityEngine;

[ExecuteInEditMode]
public class LightObject : MonoBehaviour
{
    [SerializeField]
    private Vector3 direction = new Vector3(0, -1, 0);
    [SerializeField]
    private Material material;
    [SerializeField]
    private Color lightColor;
    [SerializeField]
    [Range(0f, 1f)]
    private float smoothness;


    public enum Type
    {
        direction = 0,
        point = 1,
        spot = 2,
    }

    [SerializeField]
    private Type type;
    [SerializeField]
    private float intensity;

    // Update is called once per frame
    void Update()
    {
        direction = transform.rotation * new Vector3(0, -1, 0);
        direction = direction.normalized;

        SendToShader();
    }

    private void SendToShader()
    {
        material.SetVector("_lightPosition", transform.position);
        material.SetVector("_lightDirection", direction);
        material.SetColor("_lightColor", lightColor);
        material.SetFloat("_smoothness", smoothness);
        material.SetInteger("_lightType", (int)type);
        material.SetFloat("_lightIntensity", intensity);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1);
        Gizmos.DrawRay(transform.position, direction * 10.0f);
    }
}
