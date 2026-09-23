using UnityEngine;

// Drives a simple fill-bar Image from a Health component's Percent01.
public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private UnityEngine.UI.Image fillImage;

    private void Update()
    {
        if (health == null || fillImage == null) return;
        fillImage.fillAmount = health.Percent01;
    }
}
