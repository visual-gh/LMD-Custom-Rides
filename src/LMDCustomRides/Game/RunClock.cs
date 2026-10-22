using Il2CppMegagon.Downhill;
using Il2CppMegagon.Downhill.Managers;
using Il2CppMegagon.Downhill.UI.Screens;
using UnityEngine;

namespace LMDCustomRides.Game;

/// <summary>
/// The speedrun clock of a playlist run. Game time counts while the player is in control on a trail: from gaining
/// control after the intro until that ride's finish line, crashes, respawns and restarts included; loading, menus,
/// fades, the intro, the pause menu and the time after the finish line are not counted. It adds the game's own
/// (scaled) frame time, as the game's ride timer does, so slow motion counts as the game counts it. Real time runs
/// from the first control to the last finish line, everything included; pause time is the time in the pause menu.
/// </summary>
internal sealed class RunClock
{
    private double _game;
    private double _paused;
    private double _rideStart;
    private float _realStart = -1f;
    private float _realEnd = -1f;
    private readonly List<long> _segments = new();

    public long GameMs => (long)(_game * 1000.0);
    public long PausedMs => (long)(_paused * 1000.0);
    public long RealMs => _realStart < 0f ? 0 : (long)(((_realEnd >= 0f ? _realEnd : Time.unscaledTime) - _realStart) * 1000f);

    /// <summary>Each finished ride's game time, in run order.</summary>
    public IReadOnlyList<long> Segments => _segments;

    /// <summary>The current ride's game time so far.</summary>
    public long RideMs => (long)((_game - _rideStart) * 1000.0);

    public DateTime Started { get; } = DateTime.Now;

    /// <summary>The player has control of a ride for the first time (the clock starts on the first ride).</summary>
    public void RideBegins()
    {
        _rideStart = _game;
        if (_realStart < 0f)
        {
            _realStart = Time.unscaledTime;
        }
    }

    /// <summary>Per frame while a ride is in control.</summary>
    public void Tick()
    {
        if (Paused)
        {
            _paused += Time.unscaledDeltaTime;
        }
        else
        {
            _game += Time.deltaTime;
        }
    }

    /// <summary>The ride's finish line: its game time.</summary>
    public long RideFinished()
    {
        var ms = RideMs;
        _segments.Add(ms);
        _realEnd = Time.unscaledTime;
        return ms;
    }

    /// <summary>Riding on after a finish line (real time keeps going until the next one).</summary>
    public void Continue() => _realEnd = -1f;

    // ---------------------------------------------------------------- game signals

    private static PauseScreen[] _pauseScreens;
    private static float _lookAgainAt;

    /// <summary>The pause menu is open (the game stops its time there).</summary>
    public static bool Paused
    {
        get
        {
            if (Time.timeScale <= 0.001f)
            {
                return true;
            }

            if (_pauseScreens == null || (_pauseScreens.Length == 0 && Time.unscaledTime > _lookAgainAt) || _pauseScreens.Any(p => p == null))
            {
                _pauseScreens = Resources.FindObjectsOfTypeAll<PauseScreen>().Where(p => p.gameObject.scene.IsValid()).ToArray();
                _lookAgainAt = Time.unscaledTime + 2f;
            }

            return _pauseScreens.Any(p => p.gameObject.activeInHierarchy);
        }
    }

    /// <summary>
    /// The player controls the bike: the play manager's flag set when the intro hands over (and cleared while the
    /// bike lies crashed).
    /// </summary>
    public static bool InControl
    {
        get
        {
            try
            {
                var manager = Manager<PlayManager>.prop_T_0;
                return manager != null && manager.field_Private_Boolean_0;
            }
            catch
            {
                return false;
            }
        }
    }
}
