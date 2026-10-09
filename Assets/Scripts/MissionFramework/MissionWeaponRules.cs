using UnityEngine;

// Put one of these in each mission scene. While that mission is running
// (from the briefing until Mission Passed / Failed) only the ticked weapons
// can be drawn. Free roam - the Hub (no rules there) or a mission that's
// already over - allows everything.
public class MissionWeaponRules : MonoBehaviour
{
    [Tooltip("This scene's MissionXManager. Left empty, it's found automatically")]
    [SerializeField] private MonoBehaviour missionManager;

    [Header("Weapons this mission allows while it's running")]
    [SerializeField] private bool allowRifle;
    [SerializeField] private bool allowKnife;

    private static MissionWeaponRules current;
    private System.Reflection.PropertyInfo stateProperty;

    private void Awake()
    {
        current = this;
        if (missionManager == null)
        {
            // Mission1Manager ... Mission5Manager
            foreach (MonoBehaviour b in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (System.Text.RegularExpressions.Regex.IsMatch(b.GetType().Name, @"^Mission\d+Manager$")) { missionManager = b; break; }
        }
        if (missionManager != null) stateProperty = missionManager.GetType().GetProperty("CurrentState");
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }

    // Every mission manager has CurrentState: Setup, InProgress, (Escaping), Success, Failed
    private bool MissionRunning
    {
        get
        {
            if (missionManager == null || stateProperty == null) return false;
            string state = stateProperty.GetValue(missionManager).ToString();
            return state != "Success" && state != "Failed";
        }
    }

    public static bool IsAllowed(WeaponHolster.WeaponKind kind)
    {
        if (current == null || !current.MissionRunning) return true; // free roam
        return kind == WeaponHolster.WeaponKind.Rifle ? current.allowRifle : current.allowKnife;
    }
}
