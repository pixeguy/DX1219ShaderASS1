using UnityEngine;

public class BallMove : MonoBehaviour
{
    public float speed = 2f;      // how fast it moves
    public float distance = 3f;   // how far it moves

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        float offset = Mathf.Sin(Time.time * speed) * distance;
        transform.position = startPos + transform.right * offset;
    }
}
