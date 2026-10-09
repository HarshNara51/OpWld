using UnityEngine;

// Put this on the player. Assign each weapon GameObject in order -
// press its key (Rifle / Knife on the Controls page) to equip it, press the same key again to holster it.
// Pressing a different weapon's key switches directly. Weapons
// should already be parented to the correct hand socket transform
// and start inactive in the scene.
public class WeaponHolster : MonoBehaviour
{
    public enum WeaponKind { Rifle, Knife }

    [System.Serializable]
    public class WeaponSlot
    {
        public WeaponKind kind;
        public GameObject weaponObject;
    }

    [SerializeField] private WeaponSlot[] weapons;

    private int equippedIndex = -1; // -1 = nothing equipped

    public bool IsWeaponEquipped => equippedIndex != -1;
    public GameObject CurrentWeapon => equippedIndex != -1 ? weapons[equippedIndex].weaponObject : null;
    public bool IsRifleEquipped => equippedIndex != -1 && weapons[equippedIndex].kind == WeaponKind.Rifle;
    public bool IsKnifeEquipped => equippedIndex != -1 && weapons[equippedIndex].kind == WeaponKind.Knife;

    private void Awake()
    {
        foreach (var slot in weapons)
        {
            if (slot.weaponObject != null) slot.weaponObject.SetActive(false);
        }
    }

    private float nextLockedNoteTime;

    private void Update()
    {
        // The mission doesn't allow what's in hand (e.g. it just started) - put it away
        if (equippedIndex != -1 && !MissionWeaponRules.IsAllowed(weapons[equippedIndex].kind))
            HolsterAll();

        if (PlayerKnife.IsStabbing) return; // no swapping mid-stab

        for (int i = 0; i < weapons.Length; i++)
        {
            if (GameKeys.Down(weapons[i].kind == WeaponKind.Rifle ? GameAction.EquipRifle : GameAction.EquipKnife))
            {
                ToggleWeapon(i);
                break;
            }
        }
    }

    // Puts away whatever is in hand (e.g. when the player dies)
    public void HolsterAll()
    {
        if (equippedIndex != -1 && weapons[equippedIndex].weaponObject != null)
            weapons[equippedIndex].weaponObject.SetActive(false);
        equippedIndex = -1;
    }

    private void ToggleWeapon(int index)
    {
        if (weapons[index].weaponObject == null) return; // empty slot

        if (equippedIndex == index)
        {
            weapons[index].weaponObject.SetActive(false);
            equippedIndex = -1;
        }
        else
        {
            if (!MissionWeaponRules.IsAllowed(weapons[index].kind))
            {
                if (Time.time >= nextLockedNoteTime)
                {
                    nextLockedNoteTime = Time.time + 2.5f;
                    NotePopup.Show("You can't use that during this mission.", 2f);
                }
                return;
            }

            if (equippedIndex != -1) weapons[equippedIndex].weaponObject.SetActive(false);
            weapons[index].weaponObject.SetActive(true);
            equippedIndex = index;
        }
    }
}
