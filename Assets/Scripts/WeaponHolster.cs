using UnityEngine;

// Put this on the player. Assign each weapon GameObject in order -
// press its key to equip it, press the same key again to holster it.
// Pressing a different weapon's key switches directly. Weapons
// should already be parented to the correct hand socket transform
// and start inactive in the scene.
public class WeaponHolster : MonoBehaviour
{
    [System.Serializable]
    public class WeaponSlot
    {
        public KeyCode key;
        public GameObject weaponObject;
    }

    [SerializeField] private WeaponSlot[] weapons;

    private int equippedIndex = -1; // -1 = nothing equipped

    public bool IsWeaponEquipped => equippedIndex != -1;
    public GameObject CurrentWeapon => equippedIndex != -1 ? weapons[equippedIndex].weaponObject : null;

    private void Awake()
    {
        foreach (var slot in weapons)
        {
            if (slot.weaponObject != null) slot.weaponObject.SetActive(false);
        }
    }

    private void Update()
    {
        for (int i = 0; i < weapons.Length; i++)
        {
            if (Input.GetKeyDown(weapons[i].key))
            {
                ToggleWeapon(i);
                break;
            }
        }
    }

    private void ToggleWeapon(int index)
    {
        if (equippedIndex == index)
        {
            weapons[index].weaponObject.SetActive(false);
            equippedIndex = -1;
        }
        else
        {
            if (equippedIndex != -1) weapons[equippedIndex].weaponObject.SetActive(false);
            weapons[index].weaponObject.SetActive(true);
            equippedIndex = index;
        }
    }
}
