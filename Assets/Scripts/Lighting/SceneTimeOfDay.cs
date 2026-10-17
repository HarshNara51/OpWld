using UnityEngine;

// Put ONE of these in each scene (e.g. on the Directional Light) and pick
// the scene's time of day. Everything with a NightOnly component - the
// dino uplights, street lights, the aircraft lights, and anything else you
// add it to - plus car headlights only switch on when it's dark enough:
// Dawn, Evening, Night or Overcast. Morning and Day keep them off.
//
// A scene without one keeps all those lights on (how it was before).
public class SceneTimeOfDay : MonoBehaviour
{
    // New values go at the end so scenes keep their saved choice.
    public enum TimeOfDay { Morning, Day, Evening, Night, Dawn, Overcast }

    [SerializeField] private TimeOfDay timeOfDay = TimeOfDay.Day;

    private static SceneTimeOfDay current;

    // True when the "dark scene" lights should be on
    public static bool LightsOn => current == null || (current.timeOfDay != TimeOfDay.Morning && current.timeOfDay != TimeOfDay.Day);

    private void Awake() => current = this;

    private void OnDestroy()
    {
        if (current == this) current = null;
    }
}
