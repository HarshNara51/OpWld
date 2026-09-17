// Shared by any mission manager that SuspicionManager (or anything
// else generic) needs to be able to fail, without hardcoding a
// specific mission class.
public interface IFailableMission
{
    void FailMission(string reason);
}
