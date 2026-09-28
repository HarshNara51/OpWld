using UnityEngine;

// Put this on a small Sphere Collider positioned at head height, as
// a child of the enemy. A raycast that hits this specific collider
// counts as a headshot. Leave "Is Trigger" unchecked - raycasts hit
// solid colliders fine, no trigger needed here.
public class HeadHitbox : MonoBehaviour
{
    [SerializeField] private Health health;
    public Health Health => health;
}
