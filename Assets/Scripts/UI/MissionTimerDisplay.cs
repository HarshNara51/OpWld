using TMPro;
using UnityEngine;

// The shared look for mission countdowns (Farm "Trucks arrive in", Railway train timer...):
// a dark plate at the top-centre with a small caption and big amber digits that turn red
// and pulse for the last few seconds. Countdown scripts just call Show / SetSeconds / Hide.
public class MissionTimerDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text caption;
    [SerializeField] private TMP_Text digits;
    [SerializeField] private Color normalColor = new Color(1f, 0.72f, 0.3f);
    [SerializeField] private Color warningColor = new Color(1f, 0.3f, 0.22f);
    [Tooltip("Seconds left when the digits turn red and pulse")]
    [SerializeField] private float warningSeconds = 10f;

    private float seconds;

    public void Show(string captionText)
    {
        if (caption != null) caption.text = captionText;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetSeconds(float value)
    {
        seconds = Mathf.Max(0f, value);
        int total = Mathf.CeilToInt(seconds);
        if (digits != null) digits.text = $"{total / 60:00}:{total % 60:00}";
    }

    private void Update()
    {
        if (digits == null) return;
        bool warn = seconds <= warningSeconds;
        digits.color = warn ? warningColor : normalColor;
        // Unscaled time, so the pulse doesn't freeze when the game pauses time
        float pulse = warn ? 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f)) : 1f;
        digits.transform.localScale = Vector3.one * pulse;
    }
}
