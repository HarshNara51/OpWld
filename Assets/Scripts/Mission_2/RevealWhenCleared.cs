using UnityEngine;

// Put this on any empty GameObject (NOT on the object it reveals).
// Keeps the "Reveal" objects hidden until every object in
// "Must Be Taken Down" is gone (deactivated) - e.g. the bedroom
// entrance gizmo only appears once both guards are down.
// Reusable anywhere: locked doors, gates, next objectives...
public class RevealWhenCleared : MonoBehaviour
{
    [Tooltip("These must all be taken down / deactivated first (e.g. Guard1, Guard2)")]
    [SerializeField] private GameObject[] mustBeTakenDown;

    [Tooltip("Hidden until then (e.g. the BedroomEntrance gizmo)")]
    [SerializeField] private GameObject[] reveal;

    [Tooltip("Shown on screen when they appear. Leave empty for none.")]
    [TextArea(2, 4)]
    [SerializeField] private string note = "Both guards are down. The way to the bedroom is clear.";

    private bool revealed;

    private void Start()
    {
        SetRevealed(false);
    }

    private void Update()
    {
        if (revealed) return;

        foreach (GameObject obj in mustBeTakenDown)
        {
            if (obj != null && obj.activeInHierarchy) return; // someone's still standing
        }

        revealed = true;
        SetRevealed(true);
        NotePopup.Show(note, 4f);
    }

    private void SetRevealed(bool show)
    {
        foreach (GameObject obj in reveal)
        {
            if (obj != null) obj.SetActive(show);
        }
    }
}
