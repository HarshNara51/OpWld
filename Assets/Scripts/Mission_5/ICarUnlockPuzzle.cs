using System;

// Any puzzle that can unlock a locked car (lockpick, hotwire, ...).
// VehicleInteraction opens it and enters the car once it's solved.
public interface ICarUnlockPuzzle
{
    bool IsOpen { get; }
    void Open(Action onSolved);
}
