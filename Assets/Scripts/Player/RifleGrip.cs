using UnityEngine;

// Put this on a rifle that's a child of a humanoid character's hand.
// Every frame, after the animation has posed the hands, it lines the
// rifle up between them: pistol grip in the right palm, barrel pointing
// through the left hand. Each rifle animation holds the hands a little
// differently, so a rifle glued to the right hand alone points the
// wrong way in some clips - this keeps it right in all of them.
[DefaultExecutionOrder(100)] // after PlayerAimIK has tilted the spine
public class RifleGrip : MonoBehaviour
{
    [Tooltip("Where the pistol grip is, in the rifle's own space (x = towards the muzzle)")]
    [SerializeField] private Vector3 gripPoint = new Vector3(-0.02f, -0.025f, 0f);
    [Tooltip("0 = wrist, 1 = knuckles - where in the hand the grip sits")]
    [Range(0f, 1f)][SerializeField] private float palmPosition = 0.6f;

    private Animator animator;
    private Transform rightHand, rightKnuckle, leftHand, leftKnuckle;

    private void Awake()
    {
        FindHands();
    }

    private void FindHands()
    {
        animator = GetComponentInParent<Animator>();
        if (animator == null || !animator.isHuman) return;
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        rightKnuckle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        leftKnuckle = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
    }

    private bool heldInRightHand;
    private Vector3 handLocalPos;
    private Quaternion handLocalRot;

    private void LateUpdate()
    {
        // While reloading, the left hand goes to the magazine - keep the rifle
        // where it was in the right hand instead of following the left one
        bool reloading = animator != null && animator.GetBool(ReloadingParam);
        if (reloading && rightHand != null)
        {
            if (!heldInRightHand)
            {
                handLocalPos = rightHand.InverseTransformPoint(transform.position);
                handLocalRot = Quaternion.Inverse(rightHand.rotation) * transform.rotation;
                heldInRightHand = true;
            }
            transform.SetPositionAndRotation(rightHand.TransformPoint(handLocalPos), rightHand.rotation * handLocalRot);
            return;
        }
        heldInRightHand = false;
        Align();
    }

    private static readonly int ReloadingParam = Animator.StringToHash("Reloading");

    // Public so editor tools / previews can line it up for a sampled pose
    public void Align()
    {
        if (rightHand == null) FindHands();
        if (rightHand == null || leftHand == null || rightKnuckle == null || leftKnuckle == null) return;

        Vector3 rightPalm = Vector3.Lerp(rightHand.position, rightKnuckle.position, palmPosition);
        Vector3 leftPalm = Vector3.Lerp(leftHand.position, leftKnuckle.position, palmPosition);

        Vector3 barrel = leftPalm - rightPalm;
        if (barrel.sqrMagnitude < 0.0001f) return;
        barrel.Normalize();
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, barrel).normalized;

        // The rifle model points along +X (muzzle) with +Y up
        Quaternion rotation = Quaternion.LookRotation(barrel, up) * Quaternion.Euler(0f, -90f, 0f);
        transform.rotation = rotation;
        transform.position = rightPalm - rotation * Vector3.Scale(gripPoint, transform.lossyScale);
    }
}
