using UnityEngine;

// Generic reusable "show an instruction when something enters" zone.
// Purely informational - no gameplay effect. Set Required Tag to
// "PlayerCar" for things like a parking spot, or leave it "Player"
// for on-foot markers - reusable for any future mission's beats.
public class InstructionZone : MonoBehaviour
{
    [TextArea]
    [SerializeField] private string message = "Instruction goes here";
    [SerializeField] private bool showOnlyOnce = true;
    [Tooltip("Tag that triggers this zone - e.g. \"Player\" or \"PlayerCar\"")]
    [SerializeField] private string requiredTag = "Player";

    private bool shown;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(requiredTag)) return;
        if (showOnlyOnce && shown) return;

        shown = true;
        Debug.Log(message);
    }
}