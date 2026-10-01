using UnityEngine;
using UnityEngine.UI;

// HUD bar for the suspicion meter. Put it in the Managers prefab's
// Canvas so every mission gets it automatically - it only appears in
// scenes that have an active SuspicionManager.
//
// Put this script on an always-active object (e.g. "HUD"), with the
// bar itself as a child assigned to Bar Root.
public class SuspicionBar : MonoBehaviour
{
    [Tooltip("The whole bar (background + fill + label) - shown/hidden as needed")]
    [SerializeField] private GameObject barRoot;

    [Tooltip("The fill Image - set its Image Type to Filled, Fill Method Horizontal")]
    [SerializeField] private Image fill;

    [SerializeField] private Color lowColor = new Color(1f, 0.85f, 0.2f);  // yellow
    [SerializeField] private Color highColor = new Color(0.9f, 0.1f, 0.1f); // red

    [Tooltip("Hide the bar while the meter is empty, so it only appears when you're being noticed")]
    [SerializeField] private bool hideWhenEmpty = true;

    private void Update()
    {
        SuspicionManager sm = SuspicionManager.Instance;

        bool show = sm != null
                    && sm.isActiveAndEnabled
                    && sm.IsActive
                    && (!hideWhenEmpty || sm.Percent01 > 0.001f || sm.IsBusted);

        if (barRoot != null && barRoot.activeSelf != show) barRoot.SetActive(show);
        if (!show || fill == null) return;

        float p = sm.Percent01;
        fill.fillAmount = p;
        fill.color = Color.Lerp(lowColor, highColor, p);
    }
}
