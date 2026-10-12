using System.Collections.Generic;
using UnityEngine;

// Muzzle flash for any gun: MuzzleFlash.Play(muzzle) on every shot.
// 'muzzle' is the empty at the barrel tip (the rifles have one called
// "Muzzle"; the barrel points along its +X).
//
// The effect is Resources/VFX/MuzzleFlash.prefab - swap or rescale that
// prefab to change every gun's flash. One copy per gun, reused each shot.
public static class MuzzleFlash
{
    private const float Size = 1f; // world size; the prefab itself holds the real scale

    private static GameObject prefab;
    private static bool loaded;
    private static readonly Dictionary<Transform, ParticleSystem> Flashes = new Dictionary<Transform, ParticleSystem>();

    public static void Play(Transform muzzle)
    {
        if (muzzle == null) return;

        if (!Flashes.TryGetValue(muzzle, out ParticleSystem flash) || flash == null)
        {
            if (!loaded)
            {
                loaded = true;
                prefab = Resources.Load<GameObject>("VFX/MuzzleFlash");
                if (prefab == null) Debug.LogWarning("MuzzleFlash: Resources/VFX/MuzzleFlash.prefab is missing.");
            }
            if (prefab == null) return;

            // Child of the muzzle, so it moves with the gun; facing down the barrel (+X)
            GameObject go = Object.Instantiate(prefab, muzzle);
            go.transform.localPosition = Vector3.zero;
            go.transform.rotation = Quaternion.LookRotation(muzzle.right, muzzle.up);
            Vector3 s = muzzle.lossyScale;
            go.transform.localScale = new Vector3(Size / Safe(s.x), Size / Safe(s.y), Size / Safe(s.z));

            flash = go.GetComponentInChildren<ParticleSystem>();
            Flashes[muzzle] = flash;
        }

        if (flash == null) return;
        flash.Clear(true);
        flash.Play(true);
    }

    // The "Muzzle" empty somewhere under this gun or character (null if there isn't one)
    public static Transform Find(Transform root)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == "Muzzle") return t;
        return null;
    }

    private static float Safe(float v) => Mathf.Abs(v) < 1e-4f ? 1f : Mathf.Abs(v);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Flashes.Clear();
        loaded = false;
        prefab = null;
    }
}
