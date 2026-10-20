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
    [Tooltip("Police colours: caption and digits run red -> white -> blue across the letters (e.g. 'Cops arrive in')")]
    public bool policeColors;

    private static readonly Color PoliceRed = new Color(1f, 0.22f, 0.22f);
    private static readonly Color PoliceBlue = new Color(0.25f, 0.5f, 1f);

    private float seconds;

    public void Show(string captionText)
    {
        if (caption != null) caption.text = policeColors ? PoliceGradient(captionText) : captionText;
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
        string text = $"{total / 60:00}:{total % 60:00}";
        if (digits != null) digits.text = policeColors ? PoliceGradient(text) : text;
    }

    // Each letter coloured along red -> white -> blue (TMP gradients only work per letter, not across a word)
    private static string PoliceGradient(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            float t = text.Length > 1 ? i / (float)(text.Length - 1) : 0.5f;
            Color c = t < 0.5f ? Color.Lerp(PoliceRed, Color.white, t * 2f) : Color.Lerp(Color.white, PoliceBlue, (t - 0.5f) * 2f);
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append('>').Append(text[i]).Append("</color>");
        }
        return sb.ToString();
    }

    private void Update()
    {
        if (digits == null) return;
        bool warn = seconds <= warningSeconds;
        digits.color = policeColors ? Color.white : warn ? warningColor : normalColor; // police colours live in the text itself
        // Clock ticking under the last seconds
        AudioManager am = AudioManager.Instance;
        if (am != null) am.Loop(this, am.timerTick, warn && seconds > 0f && Time.timeScale > 0f, 0.6f);
        // Unscaled time, so the pulse doesn't freeze when the game pauses time
        float pulse = warn ? 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f)) : 1f;
        digits.transform.localScale = Vector3.one * pulse;
    }
}
