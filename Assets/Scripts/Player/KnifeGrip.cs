using UnityEngine;

// Put this on a knife that's a child of a humanoid character's hand.
// Most animations (idle, walk, run) hold the hand open, so the knife
// would float between the fingers. Every frame, after the animation, the
// fingers are bent into the fist stored here (captured from the Stabbing
// clip). Only runs while the knife is out.
public class KnifeGrip : MonoBehaviour
{
    [Tooltip("The right hand's finger bones - filled in by the setup tool")]
    [SerializeField] private Transform[] fingers;
    [Tooltip("Each finger bone's local rotation in a fist")]
    [SerializeField] private Quaternion[] fistRotations;

    public void SetFist(Transform[] bones, Quaternion[] rotations)
    {
        fingers = bones;
        fistRotations = rotations;
    }

    private void LateUpdate()
    {
        if (fingers == null || fistRotations == null) return;
        for (int i = 0; i < fingers.Length && i < fistRotations.Length; i++)
            if (fingers[i] != null) fingers[i].localRotation = fistRotations[i];
    }
}
