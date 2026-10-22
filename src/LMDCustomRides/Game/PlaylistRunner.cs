using Il2CppMegagon.Downhill;
using Il2CppMegagon.Downhill.UI.Screens;
using LMDCustomRides.Ui.Kit;
using UnityEngine;

namespace LMDCustomRides.Game;

/// <summary>Where a playlist run is.</summary>
internal enum RunState
{
    /// <summary>No run.</summary>
    Idle,

    /// <summary>A ride is loading, or its intro plays: the clock waits for control.</summary>
    Loading,

    /// <summary>The player rides (paused or not: the clock tells).</summary>
    InControl,

    /// <summary>Past a finish line: the HUD shows the ride's time for a moment.</summary>
    Finishing,

    /// <summary>Behind the fade: the menu hop to the next ride, or back to the Playlists page.</summary>
    Transition,
}

/// <summary>A run's result, for the Playlists page's results view: a speedrun, or a pass through the rides.</summary>
internal sealed class RunResult
{
    public Playlist Playlist { get; init; }
    public string Signature { get; init; }
    public bool Speedrun { get; init; }

    /// <summary>The rides' times (by ride key; a speedrun's full times, a pass's game times) and, for a speedrun, its totals.</summary>
    public RunTimes Times { get; init; }

    /// <summary>What each ride is compared with (by ride key): the old speedrun PB's split, or the ride's best before the pass.</summary>
    public Dictionary<string, long> Before { get; init; }

    /// <summary>A speedrun: the PB before it (null if there was none), and whether it is the new one.</summary>
    public RunTimes OldBest { get; init; }

    public bool NewBest { get; init; }

    /// <summary>A pass: how many rides got a new best, and by how much the rides that had one improved (ms, 0 or less).</summary>
    public long Improvement { get; init; }

    public int NewRideBests { get; init; }
}

/// <summary>
/// Rides a playlist: rides one after the other in the playlist's order, each ride's setup from its ride code. Every
/// finished ride's game time (the result screen's total) can be its new best, whatever the mode. Two modes, chosen by
/// Speedrun Mode when the run starts:
/// - A pass (normal): past each finish line the game's result screen shows the ride (see <see cref="RideResults"/>);
///   Restart Run rides it again, Next Ride goes on. The Playlists page then shows each ride's last time against its
///   best before the pass.
/// - A speedrun: one timed attempt, by a <see cref="RunClock"/> (full riding time, crashes included). No result screens:
///   the HUD shows the ride's time for a moment, then the next ride loads. The run is recorded (its PB is the playlist's
///   speedrun PB) and the page shows it. Quitting to the menu during a ride leaves the run ("left at ride n").
/// Either way the run hops through the menu behind a fade (the game only starts trails from there).
/// </summary>
internal static class PlaylistRunner
{
    private const int PlayState = 100;
    private const float BannerSeconds = 3f;
    private const float SettleSeconds = 0.5f;

    private static List<string> _keys;
    private static RunTimes _best;
    private static bool _sawPlay;
    private static bool _done;
    private static float _since;
    private static float _fadeOutAt = -1f;
    private static bool _liftFade;

    public static RunState State { get; private set; }

    public static Playlist Active { get; private set; }

    /// <summary>The ride being ridden (0-based).</summary>
    public static int Index { get; private set; }

    public static RunClock Clock { get; private set; }

    /// <summary>The difference to the personal best at the last finish line (null without one).</summary>
    public static long? Delta { get; private set; }

    /// <summary>The last ride's time, shown while <see cref="RunState.Finishing"/>.</summary>
    public static long LastRideMs { get; private set; }

    /// <summary>The run just finished (the Playlists page's results view), until the page lets it go.</summary>
    public static RunResult Result { get; set; }

    /// <summary>Why the last start failed, for the page.</summary>
    public static string Problem { get; private set; }

    public static bool Running => State != RunState.Idle;

    /// <summary>The run is a speedrun (Speedrun Mode was on when it started); else a pass with result screens.</summary>
    public static bool Speedrun { get; private set; }

    // The player left the result screen with its Continue.
    private static bool _continue;

    // A pass: each ride's last finished time, and each ride's best before the pass.
    private static readonly Dictionary<string, long> PassTimes = new();
    private static Dictionary<string, long> _bestsBefore;

    /// <summary>The result screen's Continue during a pass: on to the next ride (or the end). False when no pass waits.</summary>
    public static bool Continue()
    {
        if (State != RunState.Finishing || Speedrun)
        {
            return false;
        }

        _continue = true;
        return true;
    }

    /// <summary>Restart Run on a pass's result screen: the ride is ridden again, the run waits for its finish line.</summary>
    public static void RideAgain()
    {
        if (State == RunState.Finishing && !Speedrun)
        {
            _sawPlay = false;
            Enter(RunState.Loading);
        }
    }

    /// <summary>"Next Ride (2/16)" or, after the last, the run's end: for the result screen's Continue.</summary>
    public static string ContinueText => _done ? Texts.Get("LMDMOD_RUNRESULTS") : string.Format(Texts.Get("LMDMOD_NEXTRIDE"), Index + 2, Active?.Rides.Count ?? 0);

    public static bool Start(Playlist playlist)
    {
        Problem = null;
        if (Running || playlist == null || playlist.Rides.Count == 0 || !CustomSession.Active)
        {
            return false;
        }

        // Every ride is checked first: a run never stops halfway at a ride that cannot be ridden here.
        Problem = playlist.Rides.Select(CustomRides.Unavailable).FirstOrDefault(p => p != null);
        if (Problem != null)
        {
            return false;
        }

        Active = playlist;
        Index = 0;
        Clock = new RunClock();
        Delta = null;
        Result = null;
        Speedrun = Settings.SpeedrunMode.Value;
        _keys = Records.RideKeys(playlist);
        _best = Records.Of(playlist)?.Pb;
        PassTimes.Clear();
        _bestsBefore = _keys.Select((key, i) => (key, best: Records.RideBest(playlist.Rides[i])))
            .Where(r => r.best != null).ToDictionary(r => r.key, r => r.best.Value);
        _done = false;
        KeepNewRide();
        Mod.Log.Msg($"run start: {playlist.Name} ({playlist.Rides.Count} rides)");
        return StartCurrent();
    }

    // A single playlist ride started from the table: its code applies until the player is back in a menu. Its time is
    // the game's own (a restart starts a new attempt), or in Free Rider (no checkpoints, no game time) the clock since
    // control came back last.
    private static bool _single;
    private static RideCode _singleCode;
    private static RunClock _singleClock;
    private static bool _singleControl;

    // New Ride's trail, time of day and bike (the game's own selection, which playlist rides change), put back after.
    private static (string Route, bool Night, int Vehicle) _newRide;

    private static void KeepNewRide() => _newRide = (GameState.ActiveRoute, GameState.TimeOfDay == 2, GameState.ActiveVehicle);

    private static void PutBackNewRide()
    {
        CustomSession.Override = null;
        CustomSession.ApplyMode();
        CustomRides.SetTimeOfDay(_newRide.Night);
        CustomRides.SetBike(_newRide.Vehicle);
        CustomRides.SelectRoute(_newRide.Route);
    }

    /// <summary>
    /// One playlist ride on its own: a plain custom ride with the game's own result screen, no run (its time still
    /// counts for the ride's best).
    /// </summary>
    public static bool StartSingle(RideCode code)
    {
        Problem = null;
        if (Running || !CustomSession.Active)
        {
            return false;
        }

        Problem = CustomRides.Unavailable(code);
        if (Problem != null)
        {
            return false;
        }

        KeepNewRide();
        CustomSession.Override = code;
        if (!CustomRides.ApplyGameState(code) || !CustomRides.Start(code))
        {
            PutBackNewRide();
            Problem = Texts.Get("LMDMOD_NOTAVAILABLE") + ": " + GameState.TrailHeadline(code.Route);
            return false;
        }

        _single = true;
        _singleCode = code;
        _singleClock = new RunClock();
        _singleControl = false;
        _sawPlay = false;
        return true;
    }

    /// <summary>After a single ride, back in a menu: the New Ride setup applies again.</summary>
    private static void EndSingle()
    {
        if (GameStateId == PlayState)
        {
            _sawPlay = true;
            var control = RunClock.InControl;
            if (control && !_singleControl)
            {
                _singleClock.RideBegins();
            }

            _singleControl = control;
            _singleClock.Tick();
        }
        else if (_sawPlay && CustomTab.InAnyMenu)
        {
            _single = false;
            PutBackNewRide();
        }
    }

    private static bool StartCurrent()
    {
        var code = Active.Rides[Index];
        CustomSession.Override = code;
        if (!CustomRides.ApplyGameState(code) || !CustomRides.Start(code))
        {
            // Checked before the run, so only the game can refuse here: the run is left, its rides' times kept.
            Mod.Log.Warning("run: ride could not start: " + code.Route);
            Problem = Texts.Get("LMDMOD_NOTAVAILABLE") + ": " + GameState.TrailHeadline(code.Route);
            if (Index == 0)
            {
                End();
            }
            else
            {
                Abandon("ride could not start");
            }

            return false;
        }

        _sawPlay = false;
        Enter(RunState.Loading);
        return true;
    }

    private static void Enter(RunState state)
    {
        State = state;
        _since = Time.unscaledTime;
    }

    private static float Elapsed => Time.unscaledTime - _since;

    private static int GameStateId
    {
        get
        {
            try
            {
                return (int)GameStateManager.prop_EnumNPublicSealedva5vUnique_0;
            }
            catch
            {
                return -1;
            }
        }
    }

    // ---------------------------------------------------------------- events

    /// <summary>
    /// A custom ride's finish line (RideGuard, before the game's own finish handling), with the game's time: the ride's
    /// time, wherever it was started. Free Rider rides have no game total (no checkpoints): their riding time instead.
    /// </summary>
    public static void RideFinished(long gameMs)
    {
        var ms = gameMs > 0 ? gameMs
            : State == RunState.InControl ? Clock.RideMs
            : _single && State == RunState.Idle ? _singleClock.RideMs
            : 0;
        Records.RideFinished(CustomSession.Ride, ms);
        if (State == RunState.Idle)
        {
            return;
        }

        if (!Speedrun)
        {
            // A pass: the ride's last finish is its time (Restart Run on the result screen rides it again).
            if (State is RunState.InControl or RunState.Finishing && ms > 0)
            {
                PassTimes[_keys[Index]] = ms;
                _done = Index >= Active.Rides.Count - 1;
                _continue = false;
                Enter(RunState.Finishing);
            }

            return;
        }

        if (State != RunState.InControl)
        {
            return;
        }

        LastRideMs = Clock.RideFinished();
        Delta = BestUpTo(Index) is { } best ? Clock.GameMs - best : null;
        _done = Index >= Active.Rides.Count - 1;
        Mod.Log.Msg($"run: ride {Index + 1}/{Active.Rides.Count} in {Times.Format(LastRideMs)}, total {Times.Format(Clock.GameMs)}" +
                    (Delta is { } d ? $" ({Times.Delta(d)})" : ""));
        if (_done)
        {
            Record();
        }

        _continue = false;
        Enter(RunState.Finishing);
    }

    /// <summary>The personal best's game time over the rides up to this one (in the current order), or null.</summary>
    private static long? BestUpTo(int index)
    {
        if (_best == null)
        {
            return null;
        }

        long sum = 0;
        for (var i = 0; i <= index; i++)
        {
            if (!_best.Segments.TryGetValue(_keys[i], out var ms))
            {
                return null;
            }

            sum += ms;
        }

        return sum;
    }

    private static void Record()
    {
        var times = CurrentTimes();
        var newBest = Records.Finished(Active, times);
        Result = new RunResult
        {
            Playlist = Active, Signature = Records.Signature(Active), Speedrun = true, Times = times,
            Before = _best?.Segments ?? new Dictionary<string, long>(), OldBest = _best, NewBest = newBest,
        };
        Mod.Log.Msg($"run finished: {Active.Name} in {Times.Format(times.GameMs)} (real {Times.Format(times.RealMs)}, " +
                    $"paused {Times.Short(times.PausedMs)}){(newBest ? ", personal best" : "")}");
    }

    private static RunTimes CurrentTimes()
    {
        var times = new RunTimes { GameMs = Clock.GameMs, RealMs = Clock.RealMs, PausedMs = Clock.PausedMs, Date = Clock.Started };
        for (var i = 0; i < Clock.Segments.Count && i < _keys.Count; i++)
        {
            times.Segments[_keys[i]] = Clock.Segments[i];
        }

        return times;
    }

    /// <summary>The run is given up (quit to the menu): a speedrun is kept as the last run, "left at ride n".</summary>
    private static void Abandon(string why)
    {
        Mod.Log.Msg($"run left at ride {Index + 1}/{Active.Rides.Count} ({why}): {Active.Name}");
        if (Speedrun)
        {
            Records.Left(Active, Index + 1, CurrentTimes());
        }

        End();
    }

    /// <summary>
    /// The run ends (finished, left, or a ride that could not start); a pass leaves its rides' times for the Playlists
    /// page: how many rides got a new best (a ride in twice counts once), and how far the sum of best came down through
    /// the rides that had a best before (a ride in twice is in it twice).
    /// </summary>
    private static void End()
    {
        if (!Speedrun && Active != null && PassTimes.Count > 0)
        {
            var improved = new HashSet<string>();
            long improvement = 0;
            for (var i = 0; i < _keys.Count; i++)
            {
                var ride = Active.Rides[i];
                var now = Records.RideBest(ride);
                var hadBest = _bestsBefore.TryGetValue(_keys[i], out var old);
                if (now != null && (!hadBest || now < old))
                {
                    improved.Add(ride.ToString());
                    improvement += hadBest ? now.Value - old : 0;
                }
            }

            Result = new RunResult
            {
                Playlist = Active, Signature = Records.Signature(Active), Speedrun = false,
                Times = new RunTimes { Date = Clock.Started, Segments = new Dictionary<string, long>(PassTimes) },
                Before = _bestsBefore,
                NewRideBests = improved.Count,
                Improvement = improvement,
            };
        }

        Active = null;
        State = RunState.Idle;
        _liftFade = true;
        PutBackNewRide();
    }

    // ---------------------------------------------------------------- per frame

    public static void Update()
    {
        if (_fadeOutAt >= 0f && Time.unscaledTime >= _fadeOutAt)
        {
            _fadeOutAt = -1f;
            Fade.Out();
        }

        var state = GameStateId;
        switch (State)
        {
            case RunState.Idle:
                if (_single)
                {
                    EndSingle();
                }

                LiftFadeInMenu();
                return;

            case RunState.Loading:
                if (state == PlayState)
                {
                    _sawPlay = true;
                    if (RunClock.InControl)
                    {
                        Clock.RideBegins();
                        Enter(RunState.InControl);
                    }
                }
                else if (_sawPlay && CustomTab.InAnyMenu)
                {
                    Abandon("quit during the intro");
                }

                return;

            case RunState.InControl:
                if (state != PlayState)
                {
                    if (CustomTab.InAnyMenu || !CustomSession.Active)
                    {
                        Abandon("quit to the menu");
                    }

                    return;
                }

                Clock.Tick();
                return;

            case RunState.Finishing:
                // The result screen until its Continue (or the ride's time on the HUD a moment); then the menu hop
                // behind the fade. Out of the ride another way (pause, quit), the run goes on too.
                if (state == PlayState && (Speedrun ? Elapsed < BannerSeconds : !_continue))
                {
                    return;
                }

                Fade.In();
                if (Fade.Covered)
                {
                    if (state == PlayState)
                    {
                        GameStateManager.Method_Public_Static_Void_1();
                    }

                    Enter(RunState.Transition);
                }

                return;

            case RunState.Transition:
                Fade.In();
                if (!CustomTab.InCustomMenu || Elapsed < SettleSeconds)
                {
                    return;
                }

                if (_done)
                {
                    End();
                    return;
                }

                Index++;
                Clock.Continue();
                if (StartCurrent())
                {
                    // The game's loading screen shows the trail coming.
                    _fadeOutAt = Time.unscaledTime + 0.4f;
                }

                return;
        }
    }

    /// <summary>After a run, back in a menu (the tab on the page the run started from, see CustomTab): the fade lifts.</summary>
    private static void LiftFadeInMenu()
    {
        if (_liftFade && CustomTab.InAnyMenu && (!CustomTab.InCustomMenu || CustomTab.CurrentPage >= 0))
        {
            _liftFade = false;
            Fade.Out();
        }
    }
}

/// <summary>
/// No result screen during a speedrun: the play screen opens it a moment after the finish line (its OnFinish
/// starts a game timer of m_resultScreenDelay seconds); then that wait is made too long to ever end. The timer is not
/// cancelled when the run hops to the menu: it keeps counting game time across rides and menus, so a wait of minutes
/// (600 s before) opened a stray result screen over the menu in a long run and left the menu stuck.
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(PlayScreen), nameof(PlayScreen.Method_Private_Void_PDM_4))]
internal static class ResultHold
{
    // About 11 days of game time: never within a session.
    private const float Held = 1e6f;
    private static float _gameDelay = -1f;

    private static void Prefix(PlayScreen __instance)
    {
        if (_gameDelay < 0f)
        {
            _gameDelay = __instance.m_resultScreenDelay;
        }

        __instance.m_resultScreenDelay = PlaylistRunner.Running && PlaylistRunner.Speedrun ? Held : _gameDelay;
    }
}