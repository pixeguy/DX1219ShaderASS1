using UnityEngine;

public class FreeFlyCamera : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;
    public float fastMoveMultiplier = 3f;

    [Header("Mouse Look")]
    public float lookSensitivity = 2f;
    public bool requireRightMouse = true;

    float yaw;
    float pitch;

    void Start()
    {
        // Start from current rotation
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
    }

    void HandleMouseLook()
    {
        if (requireRightMouse && !Input.GetMouseButton(1))
            return;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        yaw += mouseX * lookSensitivity;
        pitch -= mouseY * lookSensitivity;

        // Clamp vertical look
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Lock cursor while rotating
        if (requireRightMouse)
        {
            if (Input.GetMouseButtonDown(1))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (Input.GetMouseButtonUp(1))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    void HandleMovement()
    {
        // WASD plane
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        Vector3 move = Vector3.zero;
        move += transform.forward * v;
        move += transform.right * h;

        // Q/E for vertical
        if (Input.GetKey(KeyCode.E))
            move += Vector3.up;
        if (Input.GetKey(KeyCode.Q))
            move += Vector3.down;

        move = move.normalized;

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            speed *= fastMoveMultiplier;

        transform.position += move * speed * Time.deltaTime;
    }
}