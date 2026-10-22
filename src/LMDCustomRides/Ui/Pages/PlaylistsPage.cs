using LMDCustomRides.Game;
using LMDCustomRides.Ui.Kit;
using UnityEngine;

namespace LMDCustomRides.Ui.Pages;

/// <summary>
/// Playlists: the playlist is a title to step through with chevrons (like Progression's pages), "+ New Playlist" after
/// the last (both ends wrap); its personal best under it as a Statistics figure; its rides in a leaderboard table on
/// the right. Like the game's Leaderboards tab the page has two places for the input: the title (Left/Right choose the
/// playlist, Enter rides it as a timed run, [Space] edits all its rides on New Ride, [R] renames it, [T] deletes it)
/// and the table (Up/Down choose a ride, wrapping; Enter rides that ride alone; [Space] edits it on New Ride, [Z] moves
/// it, [T] removes it), entered with Down (on the chosen ride) or Up (on the last) and left with Left, Right or Escape.
/// A gamepad has the sticks' buttons the other way round (LS moves and renames, RS deletes). [C] (X on a gamepad)
/// flips Speedrun Mode anywhere on the page: the figure is the sum of best without it, the speedrun PB with it. After a
/// run the page shows its result. Ctrl+C / Ctrl+V copy and paste a playlist (no hint).
/// </summary>
internal sealed class PlaylistsPage : Page
{
    // Sixteen rows (the 16 trails of the game's own speedrun); a chevron row stands in for rides out of view.
    private const int VisibleRides = 16;

    private int _playlist = -1;
    private bool _onTable;
    private int _ride;
    private bool _moving;
    private ChevronTitle _title;
    private StatBlock _stat;
    private Table _table;
    private TextField _name;
    private HintBar.Hint _remove;
    private HintBar.Hint _move;
    private HintBar.Hint _space;
    private Row _clock;

    protected override int Index => CustomTab.PlaylistsPage;

    protected override string ActionKey =>
        _moving ? "LMDMOD_ACTION_DROPRIDE"
        : _onTable ? CustomTab.ActionStartRideKey
        : OnNew ? CustomTab.ActionCreatePlaylistKey
        : ShowingResult ? CustomTab.ActionRideAgainKey
        : CustomTab.ActionStartPlaylistKey;

    protected override string BackKey => _onTable ? "LMDMOD_BACKTOPLAYLIST" : null;

    private static List<Playlist> All => PlaylistStore.All;

    /// <summary>The playlist chosen on the page (the last one chosen, also after a restart), or null on "+ New Playlist".</summary>
    public Playlist Selected
    {
        get
        {
            if (_playlist < 0)
            {
                _playlist = Math.Max(0, All.FindIndex(p => p.Name == Settings.Playlist.Value));
            }

            return _playlist < All.Count ? All[_playlist] : null;
        }
    }

    /// <summary>The title's position: a playlist's index, or the count for "+ New Playlist".</summary>
    private int Position => Selected == null ? All.Count : _playlist;

    /// <summary>On "+ New Playlist", after the last playlist.</summary>
    private bool OnNew => Selected == null;

    private int RideCount => Selected?.Rides.Count ?? 0;

    /// <summary>The run just finished, shown until the player chooses another playlist or leaves the page.</summary>
    private RunResult Result
    {
        get
        {
            Look();
            return _result;
        }
    }

    // The record and the result are looked up once a frame (a lookup hashes the playlist's rides).
    private int _lookedFrame = -1;
    private PlaylistRecord _record;
    private RunResult _result;

    private void Look()
    {
        if (_lookedFrame == Time.frameCount)
        {
            return;
        }

        _lookedFrame = Time.frameCount;
        var selected = Selected;
        _record = selected == null ? null : Records.Of(selected);
        _result = PlaylistRunner.Result is { } r && r.Playlist == selected && r.Signature == Records.Signature(r.Playlist) ? r : null;
    }

    private bool ShowingResult => Result != null;

    private bool Editing => _name.Editing;

    public void Select(Playlist playlist)
    {
        var index = All.IndexOf(playlist);
        if (index >= 0)
        {
            Choose(index);
            _ride = Math.Max(0, playlist.Rides.Count - 1);
        }
    }

    /// <summary>Back from editing a ride on New Ride: the table has the input on that ride.</summary>
    public void ShowRide(Playlist playlist, int ride)
    {
        Select(playlist);
        _ride = ride;
        _onTable = playlist.Rides.Count > 0;
    }

    private void Choose(int index)
    {
        if (index != _playlist)
        {
            PlaylistRunner.Result = null;
        }

        _playlist = index;
        _ride = 0;
        _followed = -1;
        _moving = false;
        _onTable = false;
        if (Selected != null)
        {
            Settings.Playlist.Value = Selected.Name;
            Settings.Save();
        }
    }

    protected override void Build()
    {
        _title = new ChevronTitle(Screen.transform, Tokens.ChevronPosition);
        _stat = new StatBlock(Screen.transform, Tokens.StatBlockPosition, Tokens.StatBlockWidth, Tokens.StatBlockHeight);

        // Under the figure, stacked without gaps: what a run brought or what the figure waits for; in Speedrun Mode the
        // real time, the runs and the last run too.
        for (var i = 0; i < LineCount; i++)
        {
            var line = i;
            Stats.Text(() => Lines()[line].Text, () => Lines()[line].Color);
        }
        _table = new Table(Screen.transform, Tokens.TablePosition, Tokens.TableWidth, VisibleRides, RideIcons.Columns, "LMDMOD_COLBEST", "LMDMOD_COLDELTA");
        _name = new TextField(Screen.transform, ChevronTitle.HeadlinePosition, new Vector2(_title.HeadlineWidth, Tokens.ChevronHeadlineHeight),
            _title.HeadlineFontSize);

        // Speedrun Mode: an Options checkbox under the figure it switches, its key before its label ([C], X on a gamepad,
        // or a click), anywhere on the page; it shows lit (white) while the page has the input. A run's result belongs to
        // its mode: switching goes back to the figure.
        _clock = AddRows(Tokens.OptionPosition, Tokens.StatsWidth)
            .Checkbox("LMDMOD_SPEEDRUNCLOCK", () => Settings.SpeedrunMode.Value, v =>
            {
                Settings.SpeedrunMode.Value = v;
                Settings.Save();
                PlaylistRunner.Result = null;
            })
            .WithKey(ClockKey.ToString(), GameAction.PadExtra, "LMDMOD_HINT_SPEEDRUNCLOCK")
            .Lit(() => Interactive && !Editing);

        // The title's keys, then the table's; keyboard-only ones go with a gamepad. [Space] edits: all rides on the
        // title, one in the table.
        Hints.Add(GameAction.Cancel, "LMDMOD_HINT_CANCEL").When(() => Editing);
        _space = Hints.Add(GameAction.Secondary, "LMDMOD_HINT_EDITALL").When(() => !_moving && !Editing && RideCount > 0);
        // Rename: [R], or the left stick's button on a gamepad (the name is typed on an on-screen keyboard).
        Hints.AddKeys("R", "LMDMOD_RENAME").When(() => OnTitle && Selected != null);
        Hints.Add(GameAction.Tertiary, "LMDMOD_HINT_RENAME").When(() => GameInput.UsingGamepad && OnTitle && Selected != null);
        // [T] deletes (the playlist on the title, a ride in the table), [Z] moves a ride; a gamepad has them the other
        // way round.
        _move = Hints.Add(MoveAction, "LMDMOD_HINT_MOVERIDE").When(() => _onTable && RideCount > 1);
        _remove = Hints.Add(DeleteAction, "LMDMOD_HINT_REMOVERIDE").When(() => _onTable ? RideCount > 0 && !_moving : CanDelete());
    }

    // Moving (and restoring) on Z, deleting on T; on a gamepad the sticks the other way round: the left stick's button
    // picks up the ride the left stick then moves.
    private static GameAction MoveAction => GameInput.UsingGamepad ? GameAction.Tertiary : GameAction.Quaternary;

    private static GameAction DeleteAction => GameInput.UsingGamepad ? GameAction.Quaternary : GameAction.Tertiary;

    private bool OnTitle => !_onTable && !_moving && !Editing;

    private bool CanDelete() => OnTitle && Selected != null;

    // The speedrun clock's key: C, or X on a gamepad (a click works too).
    private const KeyCode ClockKey = KeyCode.C;

    private void ToggleClock() => _clock.Activate();

    // ---------------------------------------------------------------- the figures, left

    private PlaylistRecord Record
    {
        get
        {
            Look();
            return _record;
        }
    }

    // ---- The figure follows Speedrun Mode (its checkbox is right under it). Normal: the sum of the rides' best times
    // (game time, from any attempt). Speedrun: the speedrun PB (full time of one whole run), or after one, that run.

    private static bool SpeedrunMode => Settings.SpeedrunMode.Value;

    private string StatTime()
    {
        if (Result is { Speedrun: true } run)
        {
            return Times.Format(run.Times.GameMs);
        }

        var time = SpeedrunMode ? Record?.Pb?.GameMs : Records.SumOfBest(Selected);
        return time is { } ms ? Times.Format(ms) : "-";
    }

    /// <summary>"Sum of best", "Speedrun PB · 15 Oct" or, after a speedrun, "This run · 16 Oct" (a date goes with its figure).</summary>
    private string StatCaption()
    {
        if (Result is { Speedrun: true } run)
        {
            return $"{Texts.Get("LMDMOD_THISRUN")}  ·  {Date(run.Times.Date)}";
        }

        if (!SpeedrunMode)
        {
            return Texts.Get("LMDMOD_SUMOFBESTTITLE");
        }

        var best = Record?.Pb;
        return best == null ? Texts.Get("LMDMOD_SPEEDRUNPB") : $"{Texts.Get("LMDMOD_SPEEDRUNPB")}  ·  {Date(best.Date)}";
    }

    /// <summary>After a run, the figure's difference: the speedrun's to the old PB, the bests the pass improved.</summary>
    private (string Text, Color Color)? StatChange()
    {
        switch (Result)
        {
            case { Speedrun: true, OldBest: { } old } run:
                return (Times.Delta(run.Times.GameMs - old.GameMs), run.Times.GameMs <= old.GameMs ? Tokens.Faster : Tokens.Slower);
            case { Speedrun: false, Improvement: < 0 } pass:
                return (Times.Delta(pass.Improvement), Tokens.Faster);
            default:
                return null;
        }
    }

    /// <summary>Under the figure: what a run brought, what "+ New Playlist" does, or what the figure still waits for.</summary>
    private string StatLine()
    {
        if (OnNew)
        {
            return Texts.Get("LMDMOD_NEWPLAYLISTINFO");
        }

        if (RideCount == 0)
        {
            return Texts.Get("LMDMOD_EMPTYPLAYLIST");
        }

        switch (Result)
        {
            case { Speedrun: true } run:
                return run.NewBest || run.OldBest == null ? Texts.Get("LMDMOD_NEWBEST") : string.Format(Texts.Get("LMDMOD_BESTWAS"), Times.Format(run.OldBest.GameMs));
            case { Speedrun: false } pass:
                return pass.NewRideBests switch
                {
                    0 => Texts.Get("LMDMOD_NONEWRIDEBEST"),
                    1 => Texts.Get("LMDMOD_NEWRIDEBEST"),
                    _ => string.Format(Texts.Get("LMDMOD_NEWRIDEBESTS"), pass.NewRideBests),
                };
        }

        if (SpeedrunMode)
        {
            return Record?.Pb != null ? null : Texts.Get("LMDMOD_NOFINISH");
        }

        var withBest = Selected.Rides.Count(r => Records.RideBest(r) != null);
        return withBest < RideCount ? string.Format(Texts.Get("LMDMOD_RIDESWITHBEST"), withBest, RideCount) : null;
    }

    private int _linesFrame = -1;
    private List<(string Text, Color Color)> _lines;

    private const int LineCount = 5;

    /// <summary>
    /// The lines under the figure (the empty ones last), worked out once a frame: what the figure brought or waits for,
    /// then the mode's own lines (each mode shows only its own times): the speedrun's real time, runs and last run, or
    /// what the best sections could bring the sum of best down to.
    /// </summary>
    private List<(string Text, Color Color)> Lines()
    {
        if (_linesFrame == Time.frameCount && _lines != null)
        {
            return _lines;
        }

        _linesFrame = Time.frameCount;
        var lines = new List<(string, Color)> { (StatLine(), Result is { NewBest: true } or { NewRideBests: > 0 } ? Tokens.Orange : Tokens.White) };
        if (SpeedrunMode)
        {
            lines.Add((RealTimeLine(), Tokens.White));
            lines.Add((BestSplitsLine(), Tokens.White));
            lines.Add((RunsLine(), Tokens.White));
            lines.Add((LastLine(), Tokens.Grey));
        }
        else
        {
            lines.Add((BestSectionsLine(), Tokens.White));
        }

        _lines = lines.Where(l => !string.IsNullOrEmpty(l.Item1)).ToList();
        while (_lines.Count < LineCount)
        {
            _lines.Add(("", Tokens.White));
        }

        return _lines;
    }

    /// <summary>The speedrun figure's real time and pauses.</summary>
    private string RealTimeLine()
    {
        var times = Result?.Times ?? Record?.Pb;
        if (OnNew || times == null)
        {
            return "";
        }

        var line = string.Format(Texts.Get("LMDMOD_REALLINE"), Times.Format(times.RealMs));
        return times.PausedMs >= 1000 ? line + "  ·  " + string.Format(Texts.Get("LMDMOD_PAUSED"), Times.Short(times.PausedMs)) : line;
    }

    /// <summary>The sum of best's own goal: the rides' best sections added up, while they beat it.</summary>
    private string BestSectionsLine()
    {
        if (OnNew || Records.SumOfBestSections(Selected) is not { } sections || Records.SumOfBest(Selected) is not { } sum ||
            Times.Hundredths(sections) >= Times.Hundredths(sum))
        {
            return "";
        }

        return string.Format(Texts.Get("LMDMOD_BESTSECTIONSLINE"), Times.Format(sections));
    }

    /// <summary>The speedrun PB's own goal: the best splits of finished runs added up, while they beat it.</summary>
    private string BestSplitsLine()
    {
        // Compared as shown (hundredths): splits kept to the millisecond can add up a hair under the PB they come from.
        if (OnNew || Records.SumOfBestSplits(Selected) is not { } splits || Record?.Pb is not { } pb ||
            Times.Hundredths(splits) >= Times.Hundredths(pb.GameMs))
        {
            return "";
        }

        return string.Format(Texts.Get("LMDMOD_BESTSPLITSLINE"), Times.Format(splits));
    }

    private string RunsLine()
    {
        var record = Record;
        return record == null || record.Runs == 0 ? "" : string.Format(Texts.Get(record.Runs == 1 ? "LMDMOD_ONERUNLINE" : "LMDMOD_RUNSLINE"), record.Runs, record.Completed);
    }

    private string LastLine()
    {
        if (Result is { NewBest: true, OldBest: { } old })
        {
            return string.Format(Texts.Get("LMDMOD_PREVBEST"), Times.Format(old.GameMs));
        }

        if (Result != null)
        {
            return "";
        }

        var last = Record?.Last;
        if (last == null)
        {
            return "";
        }

        return last.Completed
            ? string.Format(Texts.Get("LMDMOD_LASTDONE"), Times.Format(last.GameMs), Date(last.Date))
            : string.Format(Texts.Get("LMDMOD_LASTLEFT"), last.Reached, Date(last.Date));
    }

    private static string Date(DateTime date) => date.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- page

    protected override void OnHide()
    {
        _name.Cancel();
        StopMoving();
        _onTable = false;
        PlaylistRunner.Result = null;
    }

    protected override void Draw()
    {
        _name.Update();
        if (Editing)
        {
            BottomBar.SetAction("LMDMOD_ACTION_CONFIRM");
        }

        if (_onTable && RideCount == 0)
        {
            _onTable = false;
        }

        _remove.SetText(_onTable ? "LMDMOD_HINT_REMOVERIDE" : "LMDMOD_HINT_DELETEPLAYLIST");
        _remove.SetAction(DeleteAction);
        _move.SetAction(MoveAction);
        _move.SetText(_moving ? "LMDMOD_HINT_DROPRIDE" : "LMDMOD_HINT_MOVERIDE");
        _space.SetText(_onTable ? "LMDMOD_HINT_EDITRIDE" : "LMDMOD_HINT_EDITALL");
        DrawTitle();
        var change = StatChange();
        // No figure where there is nothing to time ("+ New Playlist", a playlist without rides).
        var figure = !OnNew && RideCount > 0;
        _stat.Set(figure ? StatCaption() : null, figure ? StatTime() : null, change?.Text, change?.Color);
        DrawTable();
    }

    /// <summary>"Playlist 2 / 7 · 16 rides" over the name; the chevrons lit while the title has the input.</summary>
    private void DrawTitle()
    {
        if (OnNew)
        {
            _title.Set(Texts.Get("LMDMOD_NEWPLAYLISTSUB"), Texts.Get("LMDMOD_NEWPLAYLIST"));
        }
        else
        {
            var playlist = Selected;
            var line = string.Format(Texts.Get("LMDMOD_PLAYLISTOF"), _playlist + 1, All.Count) + "  ·  " +
                       Texts.Rides(playlist.Rides.Count);
            _title.Set(line, playlist.Name);
        }

        _title.HideHeadline(Editing);
        // The list wraps around, so both chevrons are lit while the title has the input.
        var input = Interactive && !_onTable && !Editing;
        _title.SetArrows(input, input);
    }

    // The ride and ride count the table window last followed (scrolling moves it away until the ride changes).
    private int _followed = -1;
    private int _followedCount = -1;

    /// <summary>
    /// The rides as leaderboard rows in a window following the chosen one: each with its best time from any attempt, or
    /// after a run its time in it and the difference to what it went against (green: faster): its best before a pass, or
    /// its split in the old speedrun PB.
    /// </summary>
    private void DrawTable()
    {
        var playlist = Selected;
        var rides = playlist?.Rides ?? new List<RideCode>();
        var keys = playlist == null ? new List<string>() : Records.RideKeys(playlist);
        var result = Result;

        // The window follows the chosen ride; the wheel scrolls it (the chosen ride goes along while the table has the input).
        _ride = Math.Clamp(_ride, 0, Math.Max(0, rides.Count - 1));
        if (_ride != _followed || rides.Count != _followedCount)
        {
            _table.Follow(_ride, rides.Count);
            _followed = _ride;
            _followedCount = rides.Count;
        }

        if (Interactive && !_moving && _table.Scroll() && _onTable)
        {
            var (top, bottom) = _table.InView;
            _ride = _followed = Math.Clamp(_ride, top, bottom);
        }

        for (var i = 0; i < _table.Count; i++)
        {
            var index = _table.EntryAt(i);
            if (index == Table.ArrowUp || index == Table.ArrowDown)
            {
                _table.SetArrow(i, index == Table.ArrowUp);
                continue;
            }

            if (index < 0 || index >= rides.Count)
            {
                _table.Set(i, null, null);
                continue;
            }

            var code = rides[index];
            var style = index != _ride || !_onTable ? TableStyle.Normal : _moving ? TableStyle.Held : TableStyle.Selected;
            string time = "-", delta = null;
            Color? deltaColor = null;
            if (result != null && result.Times.Segments.TryGetValue(keys[index], out var ms))
            {
                time = Times.Format(ms);
                if (result.Before.TryGetValue(keys[index], out var old))
                {
                    delta = Times.Delta(ms - old);
                    deltaColor = ms <= old ? Tokens.Faster : Tokens.Slower;
                }
            }
            else if (TableTime(code, keys[index]) is { } shown)
            {
                time = Times.Format(shown);
            }

            _table.Set(i, RideIcons.Cells(code, index + 1), time, style, delta, deltaColor);
        }

        _table.Finish(_table.RowsShown);
        _table.SetShown(playlist != null);
        _table.SetTimeHeader(result != null ? "LMDMOD_COLRUN" : SpeedrunMode ? "LMDMOD_COLPBSPLIT" : "LMDMOD_COLBEST");
        _table.ShowDelta(result != null);

        // A click on a ride gives the table the input on it; a click on the title gives it back.
        if (!Interactive || _moving)
        {
            return;
        }

        var clicked = _table.Clicked();
        if (clicked >= 0 && clicked < rides.Count)
        {
            _onTable = true;
            _ride = clicked;
        }
        else if (_onTable && Input.GetMouseButtonDown(0) && _title.MouseOver)
        {
            _onTable = false;
        }
    }

    /// <summary>
    /// A ride's time in the table without a run to show, each mode its own: its split in the speedrun PB (the column adds
    /// up to the PB), or its best time from any finish (it adds up to the sum of best).
    /// </summary>
    private long? TableTime(RideCode ride, string key)
    {
        if (!SpeedrunMode)
        {
            return Records.RideBest(ride);
        }

        return Record?.Pb is { } pb && pb.Segments.TryGetValue(key, out var split) ? split : null;
    }

    protected override void OnEnter()
    {
        if (_moving)
        {
            StopMoving();
            return;
        }

        if (OnNew)
        {
            // The pop-up asks for the name, "Playlist N" filled in: confirming right away keeps it (a gamepad's way).
            Popup.OpenInput(Texts.Get("LMDMOD_NEWPLAYLISTTITLE"), PlaylistStore.FreeName(), "LMDMOD_ACTION_CREATE",
                name =>
                {
                    var taken = PlaylistStore.NameTaken(name);
                    Created(PlaylistStore.Create(named: name), taken);
                });
            return;
        }

        if (RideCount == 0)
        {
            Say(Texts.Get("LMDMOD_EMPTYPLAYLIST"));
            return;
        }

        // On a ride: that ride alone, as a plain custom ride (no run, no record).
        if (_onTable ? !PlaylistRunner.StartSingle(Selected.Rides[_ride]) : !PlaylistRunner.Start(Selected))
        {
            Say(PlaylistRunner.Problem ?? Texts.Get("LMDMOD_NOTAVAILABLE"));
        }
    }

    protected override bool OnBack()
    {
        if (_moving)
        {
            StopMoving();
            return true;
        }

        if (_onTable)
        {
            _onTable = false;
            return true;
        }

        return false;
    }

    protected override void HandleInput()
    {
        if ((Input.GetMouseButtonDown(0) && UiTools.MouseOver(_clock.ClickTarget)) || GameInput.Key(ClockKey) || GameInput.PadPressed(GameAction.PadExtra))
        {
            ToggleClock();
        }
        else if (_onTable)
        {
            TableInput();
        }
        else
        {
            TitleInput();
        }

        var playlist = Selected;
        if (GameInput.Copy && playlist != null && !_moving)
        {
            GUIUtility.systemCopyBuffer = playlist.Text;
            Say(string.Format(Texts.Get("LMDMOD_PLAYLISTCOPIED"), Texts.Rides(playlist.Rides.Count)));
        }
        else if (GameInput.Paste && !_onTable)
        {
            var pasted = Playlist.Parse(GUIUtility.systemCopyBuffer, null);
            if (pasted == null)
            {
                Say(Texts.Get("LMDMOD_NOCODE"));
                return;
            }

            var created = PlaylistStore.Create(pasted);
            Select(created);
            _ride = 0;
            Say(string.Format(Texts.Get("LMDMOD_PLAYLISTPASTEDAS"), created.Name, Texts.Rides(created.Rides.Count)));
        }
    }

    /// <summary>
    /// The title has the input: Left/Right (or a chevron) choose the playlist, Down or Up go to its rides, [Space] edits
    /// all rides, [R] (the left stick's button on a gamepad) renames, [T] deletes the playlist.
    /// </summary>
    private void TitleInput()
    {
        var step = _title.Clicked();
        if (GameInput.Step(GameAction.Left))
        {
            step = -1;
        }
        else if (GameInput.Step(GameAction.Right))
        {
            step = 1;
        }

        if (step != 0)
        {
            // Wraps around, "+ New Playlist" included (like the game's mountain selector).
            Choose((Position + step + All.Count + 1) % (All.Count + 1));
            return;
        }

        var playlist = Selected;
        if (GameInput.Pressed(GameAction.Down) && RideCount > 0)
        {
            _onTable = true;
        }
        else if (GameInput.Pressed(GameAction.Up) && RideCount > 0)
        {
            // Up comes into the table from below, as the lists wrap: on the last ride.
            _onTable = true;
            _ride = RideCount - 1;
        }
        else if (playlist != null && (GameInput.Key(KeyCode.R) || GameInput.PadPressed(GameAction.Tertiary)))
        {
            _name.Begin(playlist.Name, Rename);
        }
        else if (GameInput.Pressed(GameAction.Secondary) && RideCount > 0)
        {
            NewRideTab.EditAll(Selected);
        }
        else if (GameInput.Pressed(DeleteAction) && CanDelete())
        {
            ConfirmDelete(playlist);
        }
    }

    /// <summary>
    /// The table has the input: Up/Down choose the ride (wrapping around), Left/Right go back to the title (dropping a
    /// held ride), [Space] edits the ride on New Ride, [Z] picks it up and drops it (Up/Down carry it, also around the
    /// ends), [T] removes it (asking first).
    /// </summary>
    private void TableInput()
    {
        var rides = Selected.Rides;
        var step = GameInput.Step(GameAction.Up) ? -1 : GameInput.Step(GameAction.Down) ? 1 : 0;
        if (step != 0)
        {
            var target = (_ride + step + rides.Count) % rides.Count;
            if (_moving)
            {
                var ride = rides[_ride];
                rides.RemoveAt(_ride);
                rides.Insert(target, ride);
            }

            _ride = target;
            return;
        }

        // Left/Right leave the table (a held ride is dropped where it is).
        if (GameInput.Pressed(GameAction.Left) || GameInput.Pressed(GameAction.Right))
        {
            StopMoving();
            _onTable = false;
            return;
        }

        if (GameInput.Pressed(MoveAction) && rides.Count > 1)
        {
            if (_moving)
            {
                StopMoving();
            }
            else
            {
                _moving = true;
                PlaylistRunner.Result = null;
            }
        }
        else if (GameInput.Pressed(DeleteAction) && !_moving)
        {
            ConfirmRemove(Selected, _ride);
        }
        else if (GameInput.Pressed(GameAction.Secondary) && !_moving)
        {
            NewRideTab.EditRide(Selected, _ride);
        }
    }

    private void Created(Playlist created, bool nameWasTaken)
    {
        Select(created);
        _ride = 0;
        Say(string.Format(Texts.Get(nameWasTaken ? "LMDMOD_NAMETAKENUSED" : "LMDMOD_CREATED"), created.Name));
    }

    private void StopMoving()
    {
        if (_moving)
        {
            _moving = false;
            if (Selected != null)
            {
                PlaylistStore.Save(Selected);
            }
        }
    }

    private void Rename(string name)
    {
        var playlist = Selected;
        if (playlist != null && PlaylistStore.Rename(playlist, name))
        {
            // The list goes by name: the renamed playlist may have moved.
            _playlist = All.IndexOf(playlist);
            Settings.Playlist.Value = playlist.Name;
            Settings.Save();
            Say(string.Format(Texts.Get("LMDMOD_RENAMED"), playlist.Name));
        }
        else
        {
            Say(Texts.Get(playlist != null && !PlaylistStore.NameTaken(name, playlist) ? "LMDMOD_NOTSAVED" : "LMDMOD_NAMETAKEN"));
        }
    }

    /// <summary>Deletes the playlist after a confirmation; its times stay (the same rides find them again).</summary>
    private void ConfirmDelete(Playlist playlist)
    {
        Popup.List.Clear();
        var count = playlist.Rides.Count;
        var message = count == 0
            ? string.Format(Texts.Get("LMDMOD_DELETEEMPTYMESSAGE"), playlist.Name)
            : string.Format(Texts.Get("LMDMOD_DELETEMESSAGE"), playlist.Name, Texts.Rides(count));
        Popup.Open(Texts.Get("LMDMOD_DELETETITLE"), message, "LMDMOD_ACTION_DELETE", () =>
        {
            PlaylistStore.Delete(playlist);
            Choose(Math.Max(0, Math.Min(_playlist, All.Count) - 1));
            Say(Texts.Get("LMDMOD_DELETED"));
        });
    }

    /// <summary>Removes a ride from the playlist after a confirmation.</summary>
    private void ConfirmRemove(Playlist playlist, int index)
    {
        var ride = playlist.Rides[index];
        var name = RideIcons.TrailText(ride);
        Popup.List.Clear();
        // With a personal best the pop-up says that it goes with the current rides.
        var best = Records.Of(playlist)?.Pb;
        var message = best == null
            ? string.Format(Texts.Get("LMDMOD_REMOVEMESSAGE"), name, playlist.Name)
            : string.Format(Texts.Get("LMDMOD_REMOVEBESTMESSAGE"), name, playlist.Name, Times.Format(best.GameMs));
        Popup.Open(Texts.Get("LMDMOD_REMOVETITLE"), message, "LMDMOD_ACTION_REMOVE", () =>
        {
            playlist.Rides.RemoveAt(index);
            _ride = Math.Max(0, Math.Min(index, playlist.Rides.Count - 1));
            PlaylistStore.Save(playlist);
            PlaylistRunner.Result = null;
            Say(Texts.Get("LMDMOD_REMOVED"));
        });
    }
}
