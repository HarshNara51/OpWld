using UnityEngine;

// Put ONE of these in each scene (e.g. on the Directional Light) and pick
// the scene's time of day. Everything with a NightOnly component - the
// dino uplights, the aircraft lights, and anything else you add it to -
// only switches on when this says Night.
//
// A scene without one keeps all those lights on (how it was before).
public class SceneTimeOfDay : MonoBehaviour
{
    public enum TimeOfDay { Morning, Day, Evening, Night }

    [SerializeField] private TimeOfDay timeOfDay = TimeOfDay.Day;

    private static SceneTimeOfDay current;

    // True when the night-only lights should be on
    public static bool LightsOn => current == null || current.timeOfDay == TimeOfDay.Night;

    private void Awake() => current = this;

    private void OnDestroy()
    {
        if (current == this) current = null;
    }
}
