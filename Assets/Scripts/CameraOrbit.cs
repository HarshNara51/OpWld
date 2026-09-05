using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    [Header("Follow Target")]
    [SerializeField] private Transform target;                              // Drag the mannequin here
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.6f, 0f); // roughly chest/head height

    [Header("Orbit Distance")]
    [SerializeField] private float distance = 4f;
    [SerializeField] private Vector3 extraOffset = new Vector3(0f, 0.3f, 0f); // small lift above the pivot point

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private float topClamp = -40f;   // how far up you can look
    [SerializeField] private float bottomClamp = 70f; // how far down you can look

    private float yaw;
    private float pitch;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        yaw = transform.eulerAngles.y;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Mouse input drives yaw/pitch directly - this NEVER reads the mannequin's rotation,
        // so there's no feedback loop no matter how the character's body turns.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, topClamp, bottomClamp);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Position the camera "distance" units behind the target along the rotated look direction
        Vector3 pivotPosition = target.position + targetOffset;
        Vector3 desiredPosition = pivotPosition - (rotation * Vector3.forward * distance) + extraOffset;

        transform.position = desiredPosition;
        transform.rotation = rotation;
    }
}