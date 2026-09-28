using UnityEngine;
using UnityEngine.Events;

// Generic reusable "stop the moving marker in the target zone" skill
// check. Reuse this for both the steal-the-car puzzle and the
// defuse-the-bomb puzzle - just two separate instances with
// different settings (speed, hits needed, etc).
public class PuzzleMinigame : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform bar;        // the horizontal track
    [SerializeField] private RectTransform marker;      // the moving indicator
    [SerializeField] private RectTransform targetZone;  // the highlighted hit zone
    [SerializeField] private GameObject okButton;       // hidden until solved

    [Header("Settings")]
    [SerializeField] private float markerSpeed = 300f; // pixels per second
    [SerializeField] private int requiredHits = 3;
    [SerializeField] private KeyCode hitKey = KeyCode.Space;
    [SerializeField] private float targetZoneWidth = 60f;

    [Tooltip("Called once the player clicks OK after solving - wire this to the car's EnterVehicle for the steal puzzle, or the bomb's Defuse method for the other one")]
    [SerializeField] private UnityEvent onSolved;

    private bool isOpen;
    private bool solved;
    private int hitCount;
    private float direction = 1f;
    private float barHalfWidth;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void Open()
    {
        isOpen = true;
        solved = false;
        hitCount = 0;

        if (panel != null) panel.SetActive(true);
        if (okButton != null) okButton.SetActive(false);

        barHalfWidth = bar.rect.width / 2f;
        marker.anchoredPosition = new Vector2(-barHalfWidth, marker.anchoredPosition.y);
        direction = 1f;

        RandomizeTargetZone();

        // Freeze gameplay while the puzzle is up, same pattern as
        // MissionBriefing's countdown.
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RandomizeTargetZone()
    {
        float range = bar.rect.width - targetZoneWidth;
        float x = Random.Range(-range / 2f, range / 2f);
        targetZone.anchoredPosition = new Vector2(x, targetZone.anchoredPosition.y);
        targetZone.sizeDelta = new Vector2(targetZoneWidth, targetZone.sizeDelta.y);
    }

    private void Update()
    {
        if (!isOpen || solved) return;

        // Time.timeScale is 0 while this is open, so this uses
        // unscaled time - otherwise the marker would never move.
        float pos = marker.anchoredPosition.x + direction * markerSpeed * Time.unscaledDeltaTime;
        if (pos > barHalfWidth) { pos = barHalfWidth; direction = -1f; }
        if (pos < -barHalfWidth) { pos = -barHalfWidth; direction = 1f; }
        marker.anchoredPosition = new Vector2(pos, marker.anchoredPosition.y);

        if (Input.GetKeyDown(hitKey))
        {
            float distFromZoneCenter = Mathf.Abs(marker.anchoredPosition.x - targetZone.anchoredPosition.x);
            if (distFromZoneCenter <= targetZone.sizeDelta.x / 2f)
            {
                hitCount++;
                RandomizeTargetZone(); // relocate for the next hit
                if (hitCount >= requiredHits)
                {
                    solved = true;
                    if (okButton != null) okButton.SetActive(true);
                }
            }
        }
    }

    // Wire this to the OK button's OnClick
    public void OnOkPressed()
    {
        if (!solved) return;

        isOpen = false;
        if (panel != null) panel.SetActive(false);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        onSolved?.Invoke();
    }
}
