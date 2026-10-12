using System.Collections;
using UnityEngine;

// Blood when a character is killed (called by NPCDeath.Die):
//  - a spray of droplets from the chest, right away
//  - a pool on the floor under the body once it has fallen, frozen at its
//    fullest so it stays until the body disappears (it's a child of the
//    character, so it goes away with it)
//
// The effects are Resources/VFX/BloodSpray.prefab and BloodPool.prefab
// (Vefects Free Blood VFX) - swap or rescale those prefabs to change them.
public static class Blood
{
    private const float PoolDelay = 0.8f;    // seconds after death: the body is on the floor
    private const float PoolFreezeAt = 1.2f; // seconds into the pool effect: it's at its fullest

    private static GameObject sprayPrefab, poolPrefab;
    private static bool loaded;

    public static void OnDeath(MonoBehaviour character, Animator animator)
    {
        Load();
        Transform chest = Bone(animator, HumanBodyBones.Chest);
        Vector3 sprayAt = chest != null ? chest.position : character.transform.position + Vector3.up * 1.3f;
        Spawn(sprayPrefab, sprayAt, character.transform.rotation, null);

        character.StartCoroutine(Pool(character.transform, animator));
    }

    private static IEnumerator Pool(Transform character, Animator animator)
    {
        yield return new WaitForSeconds(PoolDelay);
        if (character == null || !character.gameObject.activeInHierarchy) yield break;

        // Under the hips (where the body landed), on the floor
        Transform hips = Bone(animator, HumanBodyBones.Hips);
        Vector3 at = hips != null ? hips.position : character.position;
        at = Floor(at, character);

        ParticleSystem pool = Spawn(poolPrefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), character);
        if (pool == null) yield break;

        yield return new WaitForSeconds(PoolFreezeAt);
        if (pool != null) pool.Pause(true);
    }

    private static ParticleSystem Spawn(GameObject prefab, Vector3 at, Quaternion rotation, Transform parent)
    {
        if (prefab == null) return null;
        GameObject go = Object.Instantiate(prefab, at, rotation, parent);
        if (parent != null)
        {
            // Same size whatever the character's scale
            Vector3 s = parent.lossyScale;
            go.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, Mathf.Abs(s.x)), 1f / Mathf.Max(1e-4f, Mathf.Abs(s.y)), 1f / Mathf.Max(1e-4f, Mathf.Abs(s.z)));
        }
        ParticleSystem ps = go.GetComponentInChildren<ParticleSystem>();
        if (ps == null) return null;
        ps.Play(true);
        if (parent == null) Object.Destroy(go, 6f); // the spray isn't tied to the body
        return ps;
    }

    // First surface under the point that isn't the character itself
    private static Vector3 Floor(Vector3 from, Transform character)
    {
        float best = float.MaxValue;
        Vector3 result = from;
        foreach (RaycastHit hit in Physics.RaycastAll(from + Vector3.up * 1f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(character)) continue;
            if (hit.distance < best) { best = hit.distance; result = hit.point + Vector3.up * 0.02f; }
        }
        return result;
    }

    private static Transform Bone(Animator animator, HumanBodyBones bone)
    {
        return animator != null && animator.isHuman ? animator.GetBoneTransform(bone) : null;
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        sprayPrefab = Resources.Load<GameObject>("VFX/BloodSpray");
        poolPrefab = Resources.Load<GameObject>("VFX/BloodPool");
        if (sprayPrefab == null || poolPrefab == null) Debug.LogWarning("Blood: Resources/VFX/BloodSpray or BloodPool is missing.");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        loaded = false;
        sprayPrefab = poolPrefab = null;
    }
}
