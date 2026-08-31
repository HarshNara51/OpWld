using UnityEngine;

public class MouseMovement : MonoBehaviour
{

    public float mouseSensitivity = 100f;

    float xRotation = 0f;
    float yRotation = 0f;

    public float topClamp = -90f;
    public float bottomClamp = 90f;

    void Start()
    {
        //Locking the cursor to the middle of the screen and making it invisible
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        //Getting the mouse inputs
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        //Rotation along the X Axis (Look up and down)
        xRotation -= mouseY;

        //Clamp the rotation
        xRotation = Mathf.Clamp(xRotation, topClamp, bottomClamp);

        //Rotation along the Y Axis (Look left and right)
        yRotation += mouseX;

        //Apply rotations to our transform
        transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);

    }

}
