using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerLocomotion : MonoBehaviour
{
    // Read by CameraOrbit (shoulder zoom) and AimCrosshair
    public static bool IsAiming { get; private set; }
    // Aiming OR shooting from the hip - the body faces where you shoot (PlayerAimIK)
    public static bool IsAimMode { get; private set; }

    // Set by PlayerKnife while stabbing: no walking, turning or jumping
    public bool MovementLocked { get; set; }

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;

    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 2.0f;
    [SerializeField] private float runSpeed = 5.5f;
    [SerializeField] private float crouchSpeed = 1.2f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Rifle")]
    [Tooltip("Hold this to aim: over-the-shoulder camera, face where you aim, strafe")]
    [SerializeField] private KeyCode aimKey = KeyCode.Mouse1;
    [Tooltip("Walking speed with the rifle out (not aiming)")]
    [SerializeField] private float armedWalkSpeed = 1.8f;
    [Tooltip("Running speed with the rifle out - the Rifle Run clip itself moves at ~4 m/s")]
    [SerializeField] private float armedRunSpeed = 4.2f;
    [Tooltip("Walking speed while aiming or firing")]
    [SerializeField] private float aimMoveSpeed = 1.5f;
    [Tooltip("The rifle aiming pose holds the barrel this many degrees left of the body - turn this much extra so the barrel points where you aim")]
    [SerializeField] private float aimYawOffset = 23f;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Crouch")]
    [SerializeField] private float standingHeight = 1.8f;
    [SerializeField] private float crouchingHeight = 1.0f;

    [Header("Animation")]
    [Tooltip("Natural speed of the Running clip (m/s). Faster than this, the run animation plays faster so the feet don't slide")]
    [SerializeField] private float runClipSpeed = 5.06f;
    [Tooltip("Natural speed of the Rifle Run clip (m/s)")]
    [SerializeField] private float rifleRunClipSpeed = 3.98f;
    [Tooltip("Natural speed of the Rifle Walk clip (m/s), used while aiming")]
    [SerializeField] private float rifleWalkClipSpeed = 1.07f;
    [Tooltip("Length of the Reloading clip (s) - it's sped up/slowed down to fit WeaponFire's Reload Seconds")]
    [SerializeField] private float reloadClipLength = 3.3f;

    private CharacterController controller;
    private WeaponHolster holster;
    private WeaponFire weaponFire;
    private Vector3 velocity;
    private bool isCrouching;
    private bool isRunning;
    private int upperAimLayer = -1;
    private float upperAimWeight;
    private float lastJumpTime = -10f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        holster = GetComponent<WeaponHolster>();
        weaponFire = GetComponent<WeaponFire>();
        if (animator == null) animator = GetComponentInChildren<Animator>(); // the character model is a child
        if (animator != null) upperAimLayer = animator.GetLayerIndex("Upper Aim");

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void OnDisable()
    {
        IsAiming = false; // e.g. getting into a car
        IsAimMode = false;
    }

    private void Update()
    {
        bool armed = holster != null && holster.IsRifleEquipped;
        IsAiming = armed && Input.GetKey(aimKey);
        bool firing = armed && weaponFire != null && weaponFire.IsFiring;
        bool aimMode = IsAiming || firing; // shooting from the hip also faces where you shoot
        IsAimMode = aimMode;

        HandleCrouchToggle(armed);
        HandleMovementAndRotation(armed, aimMode, firing);
        HandleJumpAndGravity();
    }

    private void HandleCrouchToggle(bool armed)
    {
        if (armed && isCrouching) SetCrouch(false); // no rifle crouch animations - stand up when the rifle comes out
        if (MovementLocked && isCrouching) SetCrouch(false); // the knife stab is a standing animation

        // Toggle crouch on key press (LeftControl or C)
        if (!armed && !MovementLocked && (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C)))
            SetCrouch(!isCrouching);
    }

    private void SetCrouch(bool crouch)
    {
        isCrouching = crouch;
        controller.height = isCrouching ? crouchingHeight : standingHeight;
        controller.center = new Vector3(0f, controller.height / 2f, 0f);
    }

    private void HandleMovementAndRotation(bool armed, bool aimMode, bool firing)
    {
        float horizontal = MovementLocked ? 0f : Input.GetAxisRaw("Horizontal"); // A/D
        float vertical = MovementLocked ? 0f : Input.GetAxisRaw("Vertical");     // W/S
        Vector3 inputDir = new Vector3(horizontal, 0f, vertical).normalized;

        isRunning = Input.GetKey(KeyCode.LeftShift) && !isCrouching && !aimMode;

        float currentSpeed = 0f;
        Vector3 moveDir = Vector3.zero;

        if (cameraTransform != null)
        {
            // Flatten camera forward/right onto the horizontal plane so pitch doesn't affect movement
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            if (aimMode)
            {
                // Aiming: always face where the camera looks, and strafe
                Quaternion aimRotation = Quaternion.LookRotation(camForward) * Quaternion.Euler(0f, aimYawOffset, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, aimRotation, rotationSpeed * 1.5f * Time.deltaTime);
            }

            if (inputDir.magnitude >= 0.1f)
            {
                moveDir = (camForward * vertical + camRight * horizontal).normalized;

                // GTA SA style: character always turns to face wherever you're moving, no strafe animations needed
                if (!aimMode)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }

                if (aimMode) currentSpeed = aimMoveSpeed;
                else if (isCrouching) currentSpeed = crouchSpeed;
                else if (armed) currentSpeed = isRunning ? armedRunSpeed : armedWalkSpeed;
                else currentSpeed = isRunning ? runSpeed : walkSpeed;
                controller.Move(moveDir * currentSpeed * Time.deltaTime);
            }
        }

        if (animator == null) return;

        // Real speed in m/s - the blend tree thresholds are each clip's own
        // speed, so walk/jog/run always match how fast you actually move
        animator.SetFloat("Speed", currentSpeed, 0.1f, Time.deltaTime);
        animator.SetBool("Crouching", isCrouching);
        animator.SetBool("Armed", armed);
        animator.SetBool("Aiming", aimMode);
        animator.SetBool("Firing", firing);

        // Strafe direction relative to the body (aiming blend tree)
        Vector3 local = transform.InverseTransformDirection(moveDir * currentSpeed);
        animator.SetFloat("MoveX", local.x, 0.1f, Time.deltaTime);
        animator.SetFloat("MoveZ", local.z, 0.1f, Time.deltaTime);

        float clipSpeed = aimMode ? rifleWalkClipSpeed : armed ? rifleRunClipSpeed : runClipSpeed;
        animator.SetFloat("AnimSpeed", aimMode ? Mathf.Clamp(currentSpeed / clipSpeed, 1f, 2f) : Mathf.Max(1f, currentSpeed / clipSpeed));

        // Reload plays on the upper body, so you can keep moving
        bool reloading = armed && weaponFire != null && weaponFire.IsReloading;
        animator.SetBool("Reloading", reloading);
        if (weaponFire != null) animator.SetFloat("ReloadSpeed", reloadClipLength / Mathf.Max(0.1f, weaponFire.ReloadSeconds));

        // Upper body: aiming pose + firing recoil while aiming or shooting, or the reload
        if (upperAimLayer >= 0)
        {
            upperAimWeight = Mathf.MoveTowards(upperAimWeight, aimMode || reloading ? 1f : 0f, 8f * Time.deltaTime);
            animator.SetLayerWeight(upperAimLayer, upperAimWeight);
        }
    }

    private void HandleJumpAndGravity()
    {
        bool isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f; // keeps the controller firmly grounded instead of floating

        if (isGrounded && Input.GetButtonDown("Jump") && !isCrouching && !MovementLocked)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastJumpTime = Time.time;
            if (animator != null) animator.SetTrigger("Jump");
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // The jump animation goes back to walking/running the moment we land.
        // For a short moment after pressing jump we still count as airborne,
        // or the take-off frame (still touching the ground) would end it at once.
        bool animGrounded = isGrounded && Time.time - lastJumpTime > 0.2f;
        if (animator != null) animator.SetBool("Grounded", animGrounded);
    }
}
