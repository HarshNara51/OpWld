using UnityEngine;

// Put this on the player's character model (next to its Animator).
// Aim correction: while aiming/firing, after the Animator has posed the
// body, it measures where the rifle actually points (right hand -> left
// hand) and twists the spine just enough to put it on the crosshair.
// Works on top of any clip - aim idle, walking, strafing, backing up -
// even when a walk turns the hips (the upper body would aim off-side).
//
// (Unity's built-in LookAt IK isn't used on purpose: the aiming pose has
// the head tilted down onto the sights, and LookAt "levels" the head,
// which lifts the whole upper body and points the rifle at the sky.)
[RequireComponent(typeof(Animator))]
public class PlayerAimIK : MonoBehaviour
{
    [Tooltip("Never twist further than this left/right or up/down (degrees)")]
    [SerializeField] private float maxCorrection = 70f;
    [Tooltip("How fast the correction blends in/out (per second)")]
    [SerializeField] private float blendSpeed = 6f;
    [Tooltip("Aim at the point this far down the camera's view (m)")]
    [SerializeField] private float aimDistance = 30f;

    private Transform cam;
    private Transform[] spine;
    private Transform rightHand, rightKnuckle, leftHand, leftKnuckle;
    private float weight;

    private void Awake()
    {
        Animator animator = GetComponent<Animator>();
        if (Camera.main != null) cam = Camera.main.transform;
        spine = new[]
        {
            animator.GetBoneTransform(HumanBodyBones.Spine),
            animator.GetBoneTransform(HumanBodyBones.Chest),
            animator.GetBoneTransform(HumanBodyBones.UpperChest)
        };
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        rightKnuckle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        leftKnuckle = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
    }

    // After the Animator has posed the body (RifleGrip runs after this)
    private void LateUpdate()
    {
        if (cam == null || rightHand == null || leftHand == null) return;

        weight = Mathf.MoveTowards(weight, PlayerLocomotion.IsAimMode ? 1f : 0f, blendSpeed * Time.deltaTime);
        if (weight <= 0f) return;

        int count = 0;
        foreach (Transform bone in spine) if (bone != null) count++;
        if (count == 0) return;

        Vector3 aimPoint = cam.position + cam.forward * aimDistance;

        // Two passes: the first gets it almost there, the second cleans up
        for (int pass = 0; pass < 2; pass++)
        {
            Vector3 rightPalm = Vector3.Lerp(rightHand.position, rightKnuckle.position, 0.6f);
            Vector3 leftPalm = Vector3.Lerp(leftHand.position, leftKnuckle.position, 0.6f);
            Vector3 barrel = (leftPalm - rightPalm).normalized;
            Vector3 wanted = (aimPoint - rightPalm).normalized;

            // Left/right error around world up, up/down error around the body's right
            Vector3 flatBarrel = Vector3.ProjectOnPlane(barrel, Vector3.up);
            Vector3 flatWanted = Vector3.ProjectOnPlane(wanted, Vector3.up);
            float yaw = Vector3.SignedAngle(flatBarrel, flatWanted, Vector3.up);
            float pitch = Mathf.Asin(Mathf.Clamp(wanted.y, -1f, 1f)) * Mathf.Rad2Deg
                        - Mathf.Asin(Mathf.Clamp(barrel.y, -1f, 1f)) * Mathf.Rad2Deg;
            yaw = Mathf.Clamp(yaw, -maxCorrection, maxCorrection) * weight;
            pitch = Mathf.Clamp(pitch, -maxCorrection, maxCorrection) * weight;

            Vector3 right = Vector3.Cross(Vector3.up, flatWanted).normalized;
            Quaternion step = Quaternion.AngleAxis(yaw / count, Vector3.up) * Quaternion.AngleAxis(-pitch / count, right);
            foreach (Transform bone in spine)
                if (bone != null) bone.rotation = step * bone.rotation;
        }
    }
}
