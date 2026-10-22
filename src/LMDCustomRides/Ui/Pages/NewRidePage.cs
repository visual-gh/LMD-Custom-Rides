using LMDCustomRides.Game;
using LMDCustomRides.Ui.Kit;
using UnityEngine;

namespace LMDCustomRides.Ui.Pages;

/// <summary>
/// New Ride: the trail map as background, and the whole ride on the row list (mountain, trail, bike, time of day, mode,
/// modifiers). Enter always starts the ride; [Space] (X on a gamepad, or a click) flips the selected checkbox; [T] (Y)
/// adds the ride to a playlist (also while editing one). Ctrl+C / Ctrl+V copy and paste ride codes (no hint). A playlist ride can be edited
/// here: the page shows it, Enter saves it back, Escape returns (asking first when it changed), and the player's own setup
/// comes back after. All rides of a playlist can be edited at once the same way (<see cref="EditAll"/>).
/// </summary>
internal sealed class NewRidePage : Page
{
    private float _saveAt = -1f;

    // The last trail picked on each mountain: switching back to a mountain returns to it.
    private readonly Dictionary<string, string> _lastTrail = new();

    // The playlist ride being edited (null: none; index -1: all its rides), the code it had, and the player's own setup
    // to put back after.
    private Playlist _editPlaylist;
    private int _editIndex;
    private string _editOriginal;
    // (Changed Checkpoints and its route apart: a Free Rider code leaves them out, while the setup keeps them for Normal.)
    private (RideCode Code, int Seed, string Path, int Layout, int Route, int Bike, bool Checkpoints) _ownSetup;

    // Editing all rides: the fields the player changed (only those go into every ride).
    private readonly HashSet<RideField> _changed = new();

    private bool EditingRide => _editPlaylist != null;

    private bool EditingAll => _editPlaylist != null && _editIndex < 0;

    /// <summary>A row was changed: while editing all rides, that field goes into every ride.</summary>
    private void Touch(RideField field)
    {
        if (EditingAll)
        {
            _changed.Add(field);
        }
    }

    private void Touch(RideField field, Action set)
    {
        set();
        Touch(field);
    }

    /// <summary>While editing all rides: the rides differ on this field and the player has not set it.</summary>
    private bool Mixed(RideField field) => EditingAll && !_changed.Contains(field) && RideFields.Differ(_editPlaylist.Rides, field);

    protected override int Index => CustomTab.NewRidePage;

    protected override string ActionKey => EditingAll ? "LMDMOD_ACTION_SAVEALL" : EditingRide ? "LMDMOD_ACTION_SAVERIDE" : CustomTab.ActionStartRideKey;

    protected override string BackKey => EditingRide ? "LMDMOD_BACKTOPLAYLIST" : null;

    protected override void Build()
    {
        TrailMap.Build(Screen.transform);
        List.Changed += () => _saveAt = Time.unscaledTime + 1f;

        List.Choice("LMDMOD_MOUNTAIN",
            () => CustomRides.Mountains().Select(m => m.m_mountainInfo.m_mountainNameLocaKey).ToArray(),
            () => CustomRides.Mountains().FindIndex(m => m.m_mountainID == CustomRides.ActiveMountain?.m_mountainID),
            SelectMountain)
            .When(() => !EditingAll);
        List.Choice("LMDMOD_TRAIL",
            () => CustomRides.Trails(CustomRides.ActiveMountain).Select(Texts.TrailKey).ToArray(),
            () => CustomRides.Trails(CustomRides.ActiveMountain).IndexOf(GameState.ActiveRoute),
            v => SelectTrail(CustomRides.Trails(CustomRides.ActiveMountain)[v]))
            .When(() => !EditingAll);
        // In the Playlists table's order: bike, time of day, mode; then the modifiers, the two that open more rows first.
        List.Choice("LMDMOD_BIKE",
            () => CustomRides.Bikes().Select(b => b.nameLocaKey).ToArray(),
            () => Math.Max(0, CustomRides.Bikes().FindIndex(b => b.ID == GameState.ActiveVehicle)),
            v =>
            {
                var id = CustomRides.Bikes()[v].ID;
                CustomRides.SetBike(id);
                Settings.Bike.Value = id;
                Touch(RideField.Bike);
            })
            .MixedWhen(() => Mixed(RideField.Bike));
        List.Choice("LMDMOD_TIMEOFDAY", () => new[] { "GENERIC_DAY", "GENERIC_NIGHT" },
            () => GameState.TimeOfDay == 2 ? 1 : 0,
            v =>
            {
                CustomRides.SetTimeOfDay(v == 1);
                Settings.Night.Value = v == 1;
                Touch(RideField.TimeOfDay);
            })
            .MixedWhen(() => Mixed(RideField.TimeOfDay));
        List.Choice("LMDMOD_MODE", () => new[] { "GENERIC_MODE_NORMAL", "GENERIC_MODE_ONELIFE" },
            () => (int)Settings.Mode.Value,
            v =>
            {
                Settings.Mode.Value = (RideMode)v;
                CustomSession.ApplyMode();
                Touch(RideField.Mode);
            })
            .MixedWhen(() => Mixed(RideField.Mode));

        List.Heading("LMDMOD_MODIFIERS");
        // A modifier turned off takes its number back to #1 (a pasted one goes): two presses home from #500.
        List.Choice("LMDMOD_OBSTACLES", () => new[] { "LMDMOD_NONE", "LMDMOD_FEW", "LMDMOD_MANY" },
                () => (int)Settings.Obstacles.Value,
                v => Touch(RideField.Obstacles, () =>
                {
                    Settings.Obstacles.Value = (Obstacles)v;
                    if (Settings.Obstacles.Value == Obstacles.Off)
                    {
                        Settings.ObstacleLayout.Value = 1;
                        Settings.Seed.Value = 0;
                        RefreshRows();
                    }
                }))
            .MixedWhen(() => Mixed(RideField.Obstacles))
            .WithIcon(() => Icon(Settings.Obstacles.Value == Obstacles.Many ? Loadout.ManyObstaclesId : Loadout.FewObstaclesId),
                () => Settings.Obstacles.Value != Obstacles.Off);
        List.Choice("LMDMOD_LAYOUT", () => Numbered(HasSeed),
                () => FromNumber(Settings.ObstacleLayout.Value, HasSeed),
                v => Touch(RideField.Layout, () => Settings.ObstacleLayout.Value = ToNumber(v, HasSeed)))
            .Wrapping()
            .MixedWhen(() => Mixed(RideField.Layout))
            .ShowWhen(() => Settings.Obstacles.Value != Obstacles.Off || Mixed(RideField.Obstacles));
        List.Checkbox(ModifierKey(Loadout.ChangedCheckpointsId, "LMDMOD_CHANGEDCHECKPOINTS"),
                () => Settings.ChangedCheckpoints.Value, v => Touch(RideField.Checkpoints, () =>
                {
                    Settings.ChangedCheckpoints.Value = v;
                    if (!v)
                    {
                        Settings.CheckpointRoute.Value = 1;
                        Settings.Path.Value = "";
                        RefreshRows();
                    }
                }))
            .When(() => Settings.Mode.Value != RideMode.FreeRider || Mixed(RideField.Mode))
            .MixedWhen(() => Mixed(RideField.Checkpoints))
            .WithIcon(() => Icon(Loadout.ChangedCheckpointsId), () => Settings.ChangedCheckpoints.Value);
        List.Choice("LMDMOD_ROUTE", () => Numbered(HasPath),
                () => FromNumber(Settings.CheckpointRoute.Value, HasPath),
                v => Touch(RideField.Route, () => Settings.CheckpointRoute.Value = ToNumber(v, HasPath)))
            .Wrapping()
            .MixedWhen(() => Mixed(RideField.Route))
            .ShowWhen(() => (Settings.ChangedCheckpoints.Value || Mixed(RideField.Checkpoints)) && (Settings.Mode.Value != RideMode.FreeRider || Mixed(RideField.Mode)));
        List.Checkbox(ModifierKey(Loadout.MirrorId, "LMDMOD_MIRRORED"), () => Settings.Mirrored.Value, v => Touch(RideField.Mirrored, () => Settings.Mirrored.Value = v))
            .MixedWhen(() => Mixed(RideField.Mirrored))
            .WithIcon(() => Icon(Loadout.MirrorId), () => Settings.Mirrored.Value);
        List.Checkbox(ModifierKey(Loadout.UnlimitedSprintingId, "LMDMOD_UNLIMITEDSPRINTING"),
                () => Settings.UnlimitedSprinting.Value, v => Touch(RideField.Sprinting, () => Settings.UnlimitedSprinting.Value = v))
            .MixedWhen(() => Mixed(RideField.Sprinting))
            .WithIcon(() => Icon(Loadout.UnlimitedSprintingId), () => Settings.UnlimitedSprinting.Value);
        List.Checkbox(ModifierKey(Loadout.BetterFlyingControlId, "LMDMOD_BETTERFLYING"),
                () => Settings.BetterFlyingControl.Value, v => Touch(RideField.Flying, () => Settings.BetterFlyingControl.Value = v))
            .MixedWhen(() => Mixed(RideField.Flying))
            .WithIcon(() => Icon(Loadout.BetterFlyingControlId), () => Settings.BetterFlyingControl.Value);

        _toggle = Hints.Add(ToggleAction, "LMDMOD_HINT_TOGGLE").When(() => List.Selected is CheckboxRow);
        _add = Hints.Add(AddAction, "LMDMOD_HINT_ADDTOPLAYLIST").When(() => !EditingAll);
    }

    private HintBar.Hint _toggle;
    private HintBar.Hint _add;

    // [Space] flips a checkbox, [T] adds to a playlist; a gamepad has X and Y for them.
    private static GameAction ToggleAction => GameInput.UsingGamepad ? GameAction.PadExtra : GameAction.Secondary;

    private static GameAction AddAction => GameInput.UsingGamepad ? GameAction.Secondary : GameAction.Tertiary;

    /// <summary>The modifier's name as the game's daily rides show it, or this mod's own text.</summary>
    private static string ModifierKey(string id, string fallback) => GameState.Modifier(id)?.m_nameLocaKey ?? fallback;

    private static Sprite Icon(string id) => GameState.Modifier(id)?.m_modifierIcon;

    private static bool HasSeed => Settings.Seed.Value > 0;

    private static bool HasPath => !string.IsNullOrEmpty(Settings.Path.Value);

    // Layouts and routes: "Pasted" first when a pasted ride code gave one none of the numbers gives (number 0), then #1 to #999 (their rows wrap
    // around: the ends are far apart).
    private static string[] Numbered(bool fromCode)
    {
        var keys = Enumerable.Range(1, Loadout.MaxLayout).Select(i => Texts.LayoutKeyPrefix + i);
        return (fromCode ? keys.Prepend("LMDMOD_FROMCODE") : keys).ToArray();
    }

    private static int FromNumber(int number, bool fromCode) => fromCode ? number : Math.Max(0, number - 1);

    private static int ToNumber(int index, bool fromCode) => fromCode ? index : index + 1;

    private void SelectMountain(int index)
    {
        var mountains = CustomRides.Mountains();
        if (index < 0 || index >= mountains.Count)
        {
            return;
        }

        var mountain = mountains[index];
        var trails = CustomRides.Trails(mountain);
        if (trails.Count > 0)
        {
            SelectTrail(_lastTrail.TryGetValue(mountain.m_mountainID, out var last) && trails.Contains(last) ? last : trails[0]);
        }
    }

    private void SelectTrail(string route)
    {
        if (CustomRides.SelectRoute(route))
        {
            _lastTrail[route.Substring(0, 2)] = route;
            Settings.Route.Value = route;
        }

        // The trail row's list follows the mountain.
        RefreshRows();
    }

    // ---------------------------------------------------------------- page

    protected override void OnShow() => TrailMap.Show();

    protected override void OnHide()
    {
        // Leaving the page another way (tabs, F/G) while editing a ride drops the edit.
        if (EditingRide)
        {
            EndEdit(false, false);
        }

        TrailMap.Hide();
        SaveIfDirty(true);
    }

    protected override void Draw()
    {
        _toggle.SetAction(ToggleAction);
        _add.SetAction(AddAction);
        TrailMap.Update();
        SaveIfDirty(false);
    }

    protected override void OnEnter()
    {
        if (EditingRide)
        {
            // Saving changed rides of a playlist with a personal best asks first.
            if (Unchanged())
            {
                EndEdit(false, true);
            }
            else
            {
                ChangeRides(_editPlaylist, () => EndEdit(true, true));
            }
        }
        else
        {
            Start();
        }
    }

    protected override bool OnBack()
    {
        if (!EditingRide)
        {
            return false;
        }

        // As the game's settings do: unsaved changes are confirmed before they go.
        if (Unchanged())
        {
            EndEdit(false, true);
        }
        else
        {
            Popup.List.Clear();
            Popup.Open(Texts.Get("LMDMOD_DISCARDTITLE"), Texts.Get(EditingAll ? "LMDMOD_DISCARDALLMESSAGE" : "LMDMOD_DISCARDMESSAGE"),
                "LMDMOD_ACTION_DISCARD", () => EndEdit(false, true));
        }

        return true;
    }

    /// <summary>The edit changes nothing (the ride, or every ride, as it was).</summary>
    private bool Unchanged() =>
        EditingAll ? EditedRides().Select(r => r.ToString()).SequenceEqual(_editPlaylist.Rides.Select(r => r.ToString()))
            : CurrentCode().ToString() == _editOriginal;

    /// <summary>Shows a playlist ride here to edit (from the Playlists table).</summary>
    public void EditRide(Playlist playlist, int index) => BeginEdit(playlist, index, playlist.Rides[index]);

    /// <summary>
    /// Edits all of a playlist's rides at once (from its title): the rows show what the rides share ("Mixed" where they
    /// differ, the trail rows dimmed), and only the rows the player changes go into every ride.
    /// </summary>
    public void EditAll(Playlist playlist) => BeginEdit(playlist, -1, RideFields.Base(playlist.Rides));

    private void BeginEdit(Playlist playlist, int index, RideCode code)
    {
        SaveIfDirty(true);
        _ownSetup = (CurrentCode(), Settings.Seed.Value, Settings.Path.Value, Settings.ObstacleLayout.Value, Settings.CheckpointRoute.Value,
            Settings.Bike.Value, Settings.ChangedCheckpoints.Value);
        CustomRides.Apply(code);
        Settings.Bike.Value = code.Bike * 1000;
        _editPlaylist = playlist;
        _editIndex = index;
        _changed.Clear();
        _editOriginal = CurrentCode().ToString();
        RefreshRows();
        CustomTab.Menu.OnPreviousScreenButton();
    }

    /// <summary>Editing all rides: each ride with the changed fields from the setup here (changed checkpoints as set, whatever the mode).</summary>
    private List<RideCode> EditedRides()
    {
        var current = CurrentCode();
        var edit = new RideCode
        {
            Route = current.Route,
            Night = current.Night,
            Mode = current.Mode,
            Bike = current.Bike,
            Seed = current.Seed,
            Path = current.Path,
            Modifiers = Settings.ChangedCheckpoints.Value ? current.Modifiers.Append(Loadout.ChangedCheckpointsId).ToList() : current.Modifiers,
        };
        return _editPlaylist.Rides.Select(r => RideFields.Merge(r, edit, _changed)).ToList();
    }

    /// <summary>Ends editing: saves the ride(s) back (or not), puts the player's own setup back, and maybe returns to the playlist.</summary>
    private void EndEdit(bool save, bool back)
    {
        var playlist = _editPlaylist;
        string message = null;
        if (save && _editIndex < 0)
        {
            var edited = EditedRides();
            var changed = edited.Where((r, i) => r.ToString() != playlist.Rides[i].ToString()).Count();
            playlist.Rides.Clear();
            playlist.Rides.AddRange(edited);
            PlaylistStore.Save(playlist);
            PlaylistRunner.Result = null;
            message = string.Format(Texts.Get("LMDMOD_ALLSAVED"), Texts.Rides(changed));
        }
        else if (save)
        {
            playlist.Rides[_editIndex] = CurrentCode();
            PlaylistStore.Save(playlist);
            PlaylistRunner.Result = null;
        }

        _editPlaylist = null;

        CustomRides.Apply(_ownSetup.Code);
        Settings.Seed.Value = _ownSetup.Seed;
        Settings.Path.Value = _ownSetup.Path;
        Settings.ObstacleLayout.Value = _ownSetup.Layout;
        Settings.CheckpointRoute.Value = _ownSetup.Route;
        Settings.Bike.Value = _ownSetup.Bike;
        Settings.ChangedCheckpoints.Value = _ownSetup.Checkpoints;
        Settings.Save();
        RefreshRows();
        if (back)
        {
            // Back on the playlist: on the ride edited, or on the title after editing all rides.
            if (_editIndex < 0)
            {
                PlaylistsTab.Select(playlist);
            }
            else
            {
                PlaylistsTab.ShowRide(playlist, _editIndex);
            }

            CustomTab.Menu.OnNextScreenButton();
            if (message != null)
            {
                PlaylistsTab.Say(message);
            }
        }
    }

    private void Start()
    {
        SaveIfDirty(true);
        CustomSession.Override = null;
        if (!CustomRides.Start(CurrentCode()))
        {
            Say(Texts.Get("LMDMOD_NOTAVAILABLE"));
        }
    }

    protected override void HandleInput()
    {
        if (GameInput.Pressed(ToggleAction))
        {
            List.Activate();
        }
        else if (GameInput.Pressed(AddAction) && !EditingAll)
        {
            AddToPlaylist();
        }
        else if (GameInput.Copy)
        {
            GUIUtility.systemCopyBuffer = CurrentCode().ToString();
            Say(Texts.Get("LMDMOD_COPIED"));
        }
        else if (GameInput.Paste && !EditingAll)
        {
            var codes = RideCode.ParseAll(GUIUtility.systemCopyBuffer);
            if (codes.Count == 0)
            {
                Say(Texts.Get("LMDMOD_NOCODE"));
                return;
            }

            Say(CustomRides.Apply(codes[0])
                ? Texts.Get("LMDMOD_PASTED")
                : Texts.Get("LMDMOD_NOTAVAILABLE") + ": " + GameState.TrailHeadline(codes[0].Route));
            Settings.Bike.Value = GameState.ActiveVehicle;
            RefreshRows();
        }
    }

    /// <summary>
    /// The picker: every playlist (full ones dimmed; the last used one selected), and "+ New Playlist".
    /// </summary>
    private void AddToPlaylist()
    {
        var list = Popup.List;
        list.Clear();
        Row preselect = null;
        foreach (var playlist in PlaylistStore.All.ToList())
        {
            var p = playlist;
            var row = list.Item(() => p.Name, () => p.Rides.Count.ToString(), () => AddTo(p))
                .When(() => p.Rides.Count < Playlist.MaxRides);
            if (p.Name == Settings.Playlist.Value)
            {
                preselect = row;
            }
        }

        list.Item(() => Texts.Get("LMDMOD_NEWPLAYLIST"), null, NewPlaylist);
        Popup.Open(Texts.Get("LMDMOD_ADDTOPLAYLIST"), null, "LMDMOD_ACTION_ADD");
        if (preselect != null)
        {
            list.Select(preselect);
        }
    }

    /// <summary>"+ New Playlist": the pop-up asks for its name ("Playlist N" filled in), then the ride goes into it.</summary>
    private void NewPlaylist()
    {
        Popup.OpenInput(Texts.Get("LMDMOD_NEWPLAYLISTTITLE"), PlaylistStore.FreeName(), "LMDMOD_ACTION_CREATE", name =>
        {
            var taken = PlaylistStore.NameTaken(name);
            var playlist = PlaylistStore.Create(named: name);
            AddTo(playlist);
            if (taken)
            {
                Say(string.Format(Texts.Get("LMDMOD_NAMETAKENUSED"), playlist.Name));
            }
        });
    }

    private void AddTo(Playlist playlist)
    {
        Popup.Close();
        if (playlist.Rides.Count >= Playlist.MaxRides)
        {
            Say(string.Format(Texts.Get("LMDMOD_FULL"), Playlist.MaxRides));
            return;
        }

        // Adding to a playlist with a personal best asks first (the best is for its rides as they are).
        var ride = CurrentCode();
        ChangeRides(playlist, () =>
        {
            playlist.Rides.Add(ride);
            PlaylistStore.Save(playlist);
            PlaylistsTab.Select(playlist);
            Say(string.Format(Texts.Get("LMDMOD_ADDED"), playlist.Name, playlist.Rides.Count, Playlist.MaxRides));
        });
    }

    public static RideCode CurrentCode()
    {
        var (seed, path) = Loadout.SeedAndPath();
        var bike = GameState.ActiveVehicle / 1000;
        return new RideCode
        {
            Route = GameState.ActiveRoute ?? "",
            Night = GameState.TimeOfDay == 2,
            Mode = Settings.Mode.Value,
            Bike = bike is >= 1 and <= 7 ? bike : 1,
            Seed = seed,
            Path = path,
            Modifiers = Loadout.ModifierList(GameState.SuperBikeSelected),
        }.Normalized();
    }

    private void SaveIfDirty(bool now)
    {
        if (_saveAt >= 0f && (now || Time.unscaledTime >= _saveAt))
        {
            _saveAt = -1f;
            Settings.Save();
        }
    }
}
