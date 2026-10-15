// Answers one question from anywhere: "is a mission running right now?"
// Used to keep free-roam activities (like the photo hunt) out of missions.
public static class MissionStatus
{
    public static bool IsMissionInProgress()
    {
        Mission1Manager m1 = Mission1Manager.Instance;
        if (m1 != null && m1.CurrentState == Mission1Manager.MissionState.InProgress) return true;

        Mission2Manager m2 = Mission2Manager.Instance;
        if (m2 != null && (m2.CurrentState == Mission2Manager.MissionState.InProgress ||
                           m2.CurrentState == Mission2Manager.MissionState.Escaping)) return true;

        Mission3Manager m3 = Mission3Manager.Instance;
        if (m3 != null)
        {
            var s = m3.CurrentState;
            if (s != Mission3Manager.MissionState.Setup &&
                s != Mission3Manager.MissionState.Success &&
                s != Mission3Manager.MissionState.Failed) return true;
        }

        Mission4Manager m4 = Mission4Manager.Instance;
        if (m4 != null && m4.CurrentState == Mission4Manager.MissionState.InProgress) return true;

        Mission5Manager m5 = Mission5Manager.Instance;
        if (m5 != null && m5.CurrentState == Mission5Manager.MissionState.InProgress) return true;

        return false;
    }
}
