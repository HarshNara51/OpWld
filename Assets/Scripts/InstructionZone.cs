using UnityEngine;

// Generic reusable "show an instruction when the player enters" zone.
// Purely informational - no gameplay effect. Use for things like the
// parking spot or hiding spot markers, here or in any future mission.
public class InstructionZone : MonoBehaviour
{
    [TextArea]
    [SerializeField] private string message = "Instruction goes here";
    [SerializeField] private bool showOnlyOnce = true;

    private bool shown;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (showOnlyOnce && shown) return;

        shown = true;
        Debug.Log(message);
    }
}
