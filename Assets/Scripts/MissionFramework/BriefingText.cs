using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

// The mission briefing's body text. Write it in the Template box with key names in curly
// braces - {Interact}, {PhotoMode}, {EquipKnife}, {FireStab}, {EMP}... (any GameAction name) -
// and they're replaced with the player's CURRENT key, highlighted, every time the briefing
// opens. So the briefing stays right even after the player rebinds keys.
[RequireComponent(typeof(TMP_Text))]
public class BriefingText : MonoBehaviour
{
    [TextArea(8, 30)]
    [SerializeField] private string template;
    [SerializeField] private Color keyColor = new Color(1f, 0.72f, 0.3f);

    private static readonly Regex Token = new Regex(@"\{(\w+)\}");

    private void OnEnable() => Refresh();

    public void Refresh()
    {
        var text = GetComponent<TMP_Text>();
        string hex = ColorUtility.ToHtmlStringRGB(keyColor);
        text.text = Token.Replace(template ?? "", m =>
            System.Enum.TryParse(m.Groups[1].Value, out GameAction action)
                ? $"<b><color=#{hex}>{GameKeys.Label(action)}</color></b>"
                : m.Value);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (isActiveAndEnabled) Refresh();
    }
#endif
}
