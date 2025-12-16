using UnityEngine;

public class ClownCircleMove : MonoBehaviour
{
    public Transform center;      // set this in inspector (or leave null to use start position)
    public float radius = 3f;
    public float moveSpeed = 1.5f;   // circles per second-ish (higher = faster)
    public float rotateSpeed = 180f; // degrees per second (spins while moving)

    private Vector3 centerPos;
    private float angle; // radians

    void Start()
    {
        centerPos = center != null ? center.position : transform.position;
    }

    void Update()
    {
        // Move around the center (translate in a circle)
        angle += moveSpeed * Time.deltaTime; // radians/sec
        float x = Mathf.Cos(angle) * radius;
        float z = Mathf.Sin(angle) * radius;
        transform.position = centerPos + new Vector3(x, 0f, z);

        Vector3 dir = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)); // tangent direction
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }
}
