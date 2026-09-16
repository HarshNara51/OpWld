using UnityEngine;

// Put this on a trigger zone around the body in the farmhouse/barn.
// Just walking in is enough - no key press, it's a discovery moment.
public class FindBodyTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Mission3Manager.Instance.OnBodyFound();
    }
}
