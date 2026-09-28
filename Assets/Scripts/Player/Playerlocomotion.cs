using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerLocomotion : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;

    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 2.0f;
    [SerializeField] private float runSpeed = 5.5f;
    [SerializeField] private float crouchSpeed = 1.2f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Crouch")]
    [SerializeField] private float standingHeight = 1.8f;
    [SerializeField] private float crouchingHeight = 1.0f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isCrouching;
    private bool isRunning;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponent<Animator>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        HandleCrouchToggle();
        HandleMovementAndRotation();
        HandleJumpAndGravity();
    }

    private void HandleCrouchToggle()
    {
        // Toggle crouch on key press (LeftControl or C)
        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
        {
            isCrouching = !isCrouching;
            controller.height = isCrouching ? crouchingHeight : standingHeight;
            controller.center = new Vector3(0f, controller.height / 2f, 0f);
        }
    }

    private void HandleMovementAndRotation()
    {
        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D
        float vertical = Input.GetAxisRaw("Vertical");     // W/S
        Vector3 inputDir = new Vector3(horizontal, 0f, vertical).normalized;

        isRunning = Input.GetKey(KeyCode.LeftShift) && !isCrouching;

        float currentSpeed = 0f;

        if (inputDir.magnitude >= 0.1f && cameraTransform != null)
        {
            // Flatten camera forward/right onto the horizontal plane so pitch doesn't affect movement
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDir = (camForward * vertical + camRight * horizontal).normalized;

            // GTA SA style: character always turns to face wherever you're moving, no strafe animations needed
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            currentSpeed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);
            controller.Move(moveDir * currentSpeed * Time.deltaTime);
        }

        if (animator != null)
        {
            // 0-1 normalized speed for the future Idle/Walk/Run blend tree
            float animSpeed = currentSpeed / runSpeed;
            animator.SetFloat("Speed", animSpeed, 0.1f, Time.deltaTime);
            animator.SetBool("Crouching", isCrouching);
        }
    }

    private void HandleJumpAndGravity()
    {
        bool isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f; // keeps the controller firmly grounded instead of floating

        if (isGrounded && Input.GetButtonDown("Jump") && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null) animator.SetTrigger("Jump");
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        if (animator != null) animator.SetBool("Grounded", isGrounded);
    }
}