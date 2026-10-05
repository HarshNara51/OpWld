using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Ammo display ("24 / 30", "Reloading..."). Put it on the HUD object in
// the Managers prefab, so every mission with a gun gets it automatically.
// Only shows while a weapon is actually equipped, on foot, and not in
// camera mode.
public class AmmoHUD : MonoBehaviour
{
    [Tooltip("The whole ammo display (background + text) - shown/hidden as needed")]
    [SerializeField] private GameObject ammoRoot;
    [SerializeField] private TMP_Text ammoText;

    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lowAmmoColor = new Color(1f, 0.3f, 0.3f);
    [Tooltip("At or below this fraction of the magazine, the text turns red")]
    [Range(0f, 1f)] [SerializeField] private float lowAmmoFraction = 0.25f;

    private WeaponFire weapon;
    private WeaponHolster holster;
    private float nextSearchTime;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        weapon = null;
        holster = null;
        nextSearchTime = 0f;
    }

    private void Update()
    {
        // Missions without guns simply never find one - the HUD stays hidden
        if (weapon == null && Time.unscaledTime >= nextSearchTime)
        {
            nextSearchTime = Time.unscaledTime + 1f;
            weapon = FindAnyObjectByType<WeaponFire>();
            holster = FindAnyObjectByType<WeaponHolster>();
        }

        bool show = weapon != null
                    && weapon.isActiveAndEnabled            // false while driving (player hidden)
                    && holster != null && holster.IsWeaponEquipped
                    && !CameraMode.IsActive;

        if (ammoRoot != null && ammoRoot.activeSelf != show) ammoRoot.SetActive(show);
        if (!show || ammoText == null) return;

        if (weapon.IsReloading)
        {
            ammoText.text = "Reloading...";
            ammoText.color = normalColor;
            return;
        }

        ammoText.text = $"{weapon.CurrentAmmo} / {weapon.MagazineSize}";
        bool low = weapon.CurrentAmmo <= Mathf.CeilToInt(weapon.MagazineSize * lowAmmoFraction);
        ammoText.color = low ? lowAmmoColor : normalColor;
    }
}
