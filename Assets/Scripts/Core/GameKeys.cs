using System;
using UnityEngine;

// Every rebindable action in the game.
public enum GameAction
{
    // On foot
    MoveForward, MoveBack, MoveLeft, MoveRight, Sprint, Jump, Crouch,
    Interact,
    // Combat & gadgets
    EquipRifle, EquipKnife, Aim, FireStab, Reload, EMP,
    // Vehicles
    EnterExitCar, Accelerate, Reverse, SteerLeft, SteerRight, Handbrake, ResetCar, SummonCar,
    // Puzzles
    BackOff, TurnLock, TimingHit,
    // Photo mode
    PhotoMode, TakePicture,
}

// The one place that knows which key does what. Scripts ask
// GameKeys.Down(GameAction.Interact) instead of checking KeyCode.E, so the
// player can change keys on the Controls page. Choices are saved in
// PlayerPrefs and survive restarts.
//
// Keys that are NOT rebindable (Esc, mouse look, bomb keypad, F9/F10 debug)
// are still read directly by their scripts.
public static class GameKeys
{
    // When an action is used. Two actions may share a key only if they are
    // never used at the same time (e.g. W = walk forward AND accelerate).
    [Flags]
    private enum Context { Foot = 1, Car = 2, Puzzle = 4, Photo = 8 }

    private struct Info
    {
        public KeyCode defaultKey;
        public Context context;
        public Info(KeyCode key, Context context) { defaultKey = key; this.context = context; }
    }

    private static readonly Info[] Infos = BuildInfos();

    private static Info[] BuildInfos()
    {
        var infos = new Info[Enum.GetValues(typeof(GameAction)).Length];
        void Add(GameAction a, KeyCode k, Context c) => infos[(int)a] = new Info(k, c);

        Add(GameAction.MoveForward, KeyCode.W, Context.Foot);
        Add(GameAction.MoveBack, KeyCode.S, Context.Foot);
        Add(GameAction.MoveLeft, KeyCode.A, Context.Foot);
        Add(GameAction.MoveRight, KeyCode.D, Context.Foot);
        Add(GameAction.Sprint, KeyCode.LeftShift, Context.Foot);
        Add(GameAction.Jump, KeyCode.Space, Context.Foot);
        Add(GameAction.Crouch, KeyCode.C, Context.Foot);
        Add(GameAction.Interact, KeyCode.E, Context.Foot);

        Add(GameAction.EquipRifle, KeyCode.Alpha1, Context.Foot);
        Add(GameAction.EquipKnife, KeyCode.Alpha2, Context.Foot);
        Add(GameAction.Aim, KeyCode.Mouse1, Context.Foot);
        Add(GameAction.FireStab, KeyCode.Mouse0, Context.Foot);
        Add(GameAction.Reload, KeyCode.R, Context.Foot);
        Add(GameAction.EMP, KeyCode.G, Context.Foot | Context.Car);

        Add(GameAction.EnterExitCar, KeyCode.F, Context.Foot | Context.Car);
        Add(GameAction.Accelerate, KeyCode.W, Context.Car);
        Add(GameAction.Reverse, KeyCode.S, Context.Car);
        Add(GameAction.SteerLeft, KeyCode.A, Context.Car);
        Add(GameAction.SteerRight, KeyCode.D, Context.Car);
        Add(GameAction.Handbrake, KeyCode.Space, Context.Car);
        Add(GameAction.ResetCar, KeyCode.R, Context.Car);
        Add(GameAction.SummonCar, KeyCode.V, Context.Foot);

        Add(GameAction.BackOff, KeyCode.Q, Context.Puzzle);
        Add(GameAction.TurnLock, KeyCode.D, Context.Puzzle);
        Add(GameAction.TimingHit, KeyCode.Space, Context.Puzzle);

        Add(GameAction.PhotoMode, KeyCode.P, Context.Foot | Context.Car | Context.Photo);
        Add(GameAction.TakePicture, KeyCode.Mouse0, Context.Photo);
        return infos;
    }

    private static KeyCode[] keys;

    // Fired whenever a key changes (the Controls page refreshes its labels)
    public static event Action Changed;

    private static string PrefKey(GameAction a) => "Key." + a;

    private static void Load()
    {
        if (keys != null) return;
        keys = new KeyCode[Infos.Length];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = (KeyCode)PlayerPrefs.GetInt(PrefKey((GameAction)i), (int)Infos[i].defaultKey);
    }

    // ================= reading keys =================

    public static KeyCode Get(GameAction a)
    {
        Load();
        return keys[(int)a];
    }

    // While the pause menu is open no gameplay key does anything - so
    // pressing E or 1 to rebind it can't start a mission or draw the rifle
    private static bool Silenced => PauseManager.IsPaused;

    public static bool Held(GameAction a) => !Silenced && Input.GetKey(Get(a));
    public static bool Down(GameAction a) => !Silenced && Input.GetKeyDown(Get(a));
    public static bool Up(GameAction a) => !Silenced && Input.GetKeyUp(Get(a));

    // -1, 0 or 1 - like Input.GetAxisRaw, but from two rebindable keys
    public static float Axis(GameAction negative, GameAction positive)
    {
        return (Held(positive) ? 1f : 0f) - (Held(negative) ? 1f : 0f);
    }

    // What to show the player, e.g. "E", "Left Click", "Shift"
    public static string Label(GameAction a) => KeyName(Get(a));

    public static string KeyName(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.Mouse0: return "Left Click";
            case KeyCode.Mouse1: return "Right Click";
            case KeyCode.Mouse2: return "Middle Click";
            case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
            case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
            case KeyCode.LeftAlt: case KeyCode.RightAlt: return "Alt";
            case KeyCode.Return: return "Enter";
            case KeyCode.UpArrow: return "Up";
            case KeyCode.DownArrow: return "Down";
            case KeyCode.LeftArrow: return "Left";
            case KeyCode.RightArrow: return "Right";
            case KeyCode.BackQuote: return "`";
            case KeyCode.Minus: return "-";
            case KeyCode.Equals: return "=";
            case KeyCode.LeftBracket: return "[";
            case KeyCode.RightBracket: return "]";
            case KeyCode.Semicolon: return ";";
            case KeyCode.Quote: return "'";
            case KeyCode.Comma: return ",";
            case KeyCode.Period: return ".";
            case KeyCode.Slash: return "/";
            case KeyCode.Backslash: return "\\";
        }
        if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((int)(k - KeyCode.Alpha0)).ToString();
        if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return "Num " + (int)(k - KeyCode.Keypad0);
        if (k >= KeyCode.Mouse3 && k <= KeyCode.Mouse6) return "Mouse " + (int)(k - KeyCode.Mouse0 + 1);
        return k.ToString();
    }

    // ================= changing keys =================

    // Keys the player can't take: Esc is pause/back (and cancels a rebind)
    public static bool CanBind(KeyCode k)
    {
        return k != KeyCode.None && k != KeyCode.Escape && k < KeyCode.JoystickButton0;
    }

    // Gives the action a new key. If another action used at the same time
    // already had that key, the two swap - that action is returned so the
    // page can say so. Null = no swap was needed.
    public static GameAction? Set(GameAction a, KeyCode key)
    {
        Load();
        KeyCode old = keys[(int)a];
        if (old == key) return null;

        GameAction? swapped = null;
        for (int i = 0; i < keys.Length; i++)
        {
            if (i == (int)a || keys[i] != key) continue;
            if ((Infos[i].context & Infos[(int)a].context) == 0) continue; // never used together - sharing is fine
            keys[i] = old;
            PlayerPrefs.SetInt(PrefKey((GameAction)i), (int)old);
            swapped = (GameAction)i;
            break;
        }

        keys[(int)a] = key;
        PlayerPrefs.SetInt(PrefKey(a), (int)key);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return swapped;
    }

    public static void ResetAll()
    {
        Load();
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = Infos[i].defaultKey;
            PlayerPrefs.DeleteKey(PrefKey((GameAction)i));
        }
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // Player-facing name, e.g. EnterExitCar -> "Enter / exit car"
    public static string ActionName(GameAction a)
    {
        switch (a)
        {
            case GameAction.MoveForward: return "Move forward";
            case GameAction.MoveBack: return "Move back";
            case GameAction.MoveLeft: return "Move left";
            case GameAction.MoveRight: return "Move right";
            case GameAction.EquipRifle: return "Rifle";
            case GameAction.EquipKnife: return "Knife";
            case GameAction.FireStab: return "Fire / stab";
            case GameAction.EnterExitCar: return "Enter / exit car";
            case GameAction.SteerLeft: return "Steer left";
            case GameAction.SteerRight: return "Steer right";
            case GameAction.ResetCar: return "Reset car";
            case GameAction.SummonCar: return "Summon car";
            case GameAction.BackOff: return "Back off";
            case GameAction.TurnLock: return "Turn lock";
            case GameAction.TimingHit: return "Timing: hit";
            case GameAction.PhotoMode: return "Photo mode";
            case GameAction.TakePicture: return "Take picture";
            default: return a.ToString();
        }
    }
}
