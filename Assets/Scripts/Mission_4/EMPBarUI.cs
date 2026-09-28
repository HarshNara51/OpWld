using UnityEngine;
using UnityEngine.UI;

// Put this on a UI object holding a Filled Image (Image Type: Filled,
// Fill Method: Horizontal) that represents the EMP charge bar.
public class EMPBarUI : MonoBehaviour
{
    [SerializeField] private TruckEMP emp;
    [SerializeField] private Image fillImage;

    [Header("Colors by state")]
    [SerializeField] private Color chargingColor = Color.green;
    [SerializeField] private Color disruptedColor = Color.red;
    [SerializeField] private Color idleColor = Color.gray;

    private void Update()
    {
        if (emp == null || fillImage == null) return;

        fillImage.fillAmount = emp.Percent01;

        if (emp.IsDisrupted) fillImage.color = disruptedColor;
        else if (emp.IsCharging) fillImage.color = chargingColor;
        else fillImage.color = idleColor;
    }
}
