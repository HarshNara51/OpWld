using UnityEngine;

// Put this on a small Sphere Collider on the enemy's head - ideally a
// child of the character's Head bone, so it follows the animation. A
// raycast that hits this specific collider counts as a headshot. Leave
// "Is Trigger" unchecked - raycasts hit solid colliders fine.
public class HeadHitbox : MonoBehaviour
{
    [Tooltip("Leave empty to use the Health on the enemy this head belongs to")]
    [SerializeField] private Health health;
    public Health Health => health;

    private void Awake()
    {
        if (health == null) health = GetComponentInParent<Health>();
    }
}
