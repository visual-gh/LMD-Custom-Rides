using Il2CppRewired;
using UnityEngine;
using RewiredAction = Il2CppRewiredConsts.Action;

namespace LMDCustomRides.Ui.Kit;

/// <summary>The game's menu actions (Rewired), so keyboard, gamepad and the player's rebinds all work.</summary>
internal enum GameAction
{
    Submit,
    Up,
    Down,
    Left,
    Right,

    /// <summary>UI_ACTION_2: Space on the keyboard. (UI_ACTION_1 is Left Ctrl, which Ctrl+key combos would set off.)</summary>
    Secondary,

    /// <summary>UI_SECONDARY_ACTION_1: T on the keyboard (the leaderboards' "First").</summary>
    Tertiary,

    /// <summary>UI_SECONDARY_ACTION_2: Z on the keyboard (the leaderboards' "You").</summary>
    Quaternary,

    /// <summary>UI_CANCEL: Escape on the keyboard (back).</summary>
    Cancel,

    /// <summary>UI_ACTION_1: X on a gamepad. Left Ctrl on the keyboard (Ctrl+key combos would set it off): gamepad only.</summary>
    PadExtra,
}

/// <summary>
/// Menu input for the mod's pages: the game's actions through Rewired, the mod's keyboard-only combos (clipboard),
/// which kind of device the player uses, and a block while a text field takes the keyboard.
/// </summary>
internal static class GameInput
{
    private static Player _player;

    private static Player Player
    {
        get
        {
            if (_player == null)
            {
                try
                {
                    _player = ReInput.players?.GetPlayer(0);
                }
                catch
                {
                    _player = null;
                }
            }

            return _player;
        }
    }

    /// <summary>The Rewired action ID behind a game action (what the game's key hints show).</summary>
    public static int Id(GameAction action) => action switch
    {
        GameAction.Submit => RewiredAction.UI_SUBMIT,
        GameAction.Up => RewiredAction.UI_UP,
        GameAction.Down => RewiredAction.UI_DOWN,
        GameAction.Left => RewiredAction.UI_LEFT,
        GameAction.Right => RewiredAction.UI_RIGHT,
        GameAction.Secondary => RewiredAction.UI_ACTION_2,
        GameAction.Tertiary => RewiredAction.UI_SECONDARY_ACTION_1,
        GameAction.Quaternary => RewiredAction.UI_SECONDARY_ACTION_2,
        GameAction.Cancel => RewiredAction.UI_CANCEL,
        GameAction.PadExtra => RewiredAction.UI_ACTION_1,
        _ => -1,
    };

    /// <summary>True while a text field has the keyboard: no game or mod input then.</summary>
    public static bool Blocked { get; private set; }

    /// <summary>The mod's own pages ignore input while a text field has the keyboard (the game is told by the field).</summary>
    public static void Block(bool block) => Blocked = block;

    // Pop-ups and text fields open now, and the frame the last one closed.
    private static int _holds;
    private static int _releasedFrame = -10;

    /// <summary>
    /// A pop-up or text field has (or just had) the input: the game menu's back button and the pages wait, so the key
    /// that closed it (Escape, Enter) does nothing else.
    /// </summary>
    public static bool MenuHeld => _holds > 0 || Time.frameCount - _releasedFrame <= 2;

    public static void HoldMenu() => _holds++;

    public static void ReleaseMenu()
    {
        _holds = Math.Max(0, _holds - 1);
        _releasedFrame = Time.frameCount;
    }

    /// <summary>Back (Escape) for the page showing: true if it used it, so the game menu does not go back.</summary>
    public static Func<bool> Back { get; set; }

    [HarmonyLib.HarmonyPatch(typeof(Il2CppMegagon.Downhill.UI.Screens.GameMenu), nameof(Il2CppMegagon.Downhill.UI.Screens.GameMenu.OnBackButton))]
    private static class BackButton
    {
        private static bool Prefix() => !MenuHeld && !(Back?.Invoke() ?? false);
    }

    public static bool Pressed(GameAction action)
    {
        try
        {
            return !Blocked && Player != null && Player.GetButtonDown(Id(action));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Pressed even while a text field has the keyboard (the field's own confirm and cancel, from any device).</summary>
    public static bool PressedThrough(GameAction action)
    {
        try
        {
            return Player != null && Player.GetButtonDown(Id(action));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>The gamepad-only action (<see cref="GameAction.PadExtra"/>): pressed on a gamepad, never by Left Ctrl.</summary>
    public static bool PadPressed(GameAction action) => UsingGamepad && Pressed(action);

    public static bool Held(GameAction action)
    {
        try
        {
            return !Blocked && Player != null && Player.GetButton(Id(action));
        }
        catch
        {
            return false;
        }
    }

    // One step per press; held down, it steps again after a pause, then at a steady pace (like holding a key in a text field).
    internal const float RepeatDelay = 0.45f;
    internal const float RepeatInterval = 0.11f;
    private static readonly Dictionary<GameAction, float> NextRepeat = new();

    /// <summary>Pressed, or held long enough to repeat (moving through rows).</summary>
    public static bool Step(GameAction action)
    {
        if (Pressed(action))
        {
            NextRepeat[action] = Time.unscaledTime + RepeatDelay;
            return true;
        }

        if (!Held(action))
        {
            NextRepeat.Remove(action);
            return false;
        }

        if (NextRepeat.TryGetValue(action, out var next) && Time.unscaledTime >= next)
        {
            NextRepeat[action] = Time.unscaledTime + RepeatInterval;
            return true;
        }

        return false;
    }

    /// <summary>True when the player last used a gamepad (keyboard-only hints hide then).</summary>
    public static bool UsingGamepad
    {
        get
        {
            try
            {
                var controller = Player?.controllers.GetLastActiveController();
                return controller != null && controller.type == ControllerType.Joystick;
            }
            catch
            {
                return false;
            }
        }
    }

    // ---- the mod's keyboard combos (clipboard), not game actions
    private static bool Control => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

    public static bool Copy => !Blocked && Control && Input.GetKeyDown(KeyCode.C);

    public static bool Paste => !Blocked && Control && Input.GetKeyDown(KeyCode.V);

    /// <summary>A keyboard-only key of the mod's (R for rename: renaming needs a keyboard anyway), not a game action.</summary>
    public static bool Key(KeyCode key) => !Blocked && !Control && Input.GetKeyDown(key);
}
