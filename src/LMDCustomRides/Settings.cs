using MelonLoader;
using MelonLoader.Utils;

namespace LMDCustomRides;

internal enum Obstacles
{
    Off,
    Few,
    Many,
}

/// <summary>The daily rides' two modes: Normal, and Free Rider (the game's "one life" mode).</summary>
internal enum RideMode
{
    Normal,
    FreeRider,
}

/// <summary>The custom ride setup (modifiers, mode, layout), kept in UserData\LMDCustomRides.cfg between sessions.</summary>
internal static class Settings
{
    private static MelonPreferences_Category _category;

    public static MelonPreferences_Entry<bool> Mirrored { get; private set; }
    public static MelonPreferences_Entry<Obstacles> Obstacles { get; private set; }
    public static MelonPreferences_Entry<bool> ChangedCheckpoints { get; private set; }
    public static MelonPreferences_Entry<bool> UnlimitedSprinting { get; private set; }
    public static MelonPreferences_Entry<bool> BetterFlyingControl { get; private set; }
    public static MelonPreferences_Entry<RideMode> Mode { get; private set; }
    public static MelonPreferences_Entry<int> ObstacleLayout { get; private set; }
    public static MelonPreferences_Entry<int> CheckpointRoute { get; private set; }
    public static MelonPreferences_Entry<int> Seed { get; private set; }
    public static MelonPreferences_Entry<string> Path { get; private set; }
    public static MelonPreferences_Entry<int> Bike { get; private set; }
    public static MelonPreferences_Entry<string> Route { get; private set; }
    public static MelonPreferences_Entry<bool> Night { get; private set; }
    public static MelonPreferences_Entry<string> MenuRoute { get; private set; }
    public static MelonPreferences_Entry<bool> MenuNight { get; private set; }
    public static MelonPreferences_Entry<string> Playlist { get; private set; }
    public static MelonPreferences_Entry<bool> SpeedrunMode { get; private set; }
    public static MelonPreferences_Entry<bool> ModPlaylistsWritten { get; private set; }

    public static void Init()
    {
        _category = MelonPreferences.CreateCategory("LMDCustomRides", "LMD Custom Rides");
        _category.SetFilePath(System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, "LMDCustomRides.cfg"));

        Mirrored = _category.CreateEntry("Mirrored", false, "Mirrored Mode", "The trail and steering are mirrored.");
        Obstacles = _category.CreateEntry("Obstacles", LMDCustomRides.Obstacles.Off, "Obstacles",
            "Off, Few or Many: extra obstacles on the trail, placed by the layout.");
        ChangedCheckpoints = _category.CreateEntry("ChangedCheckpoints", false, "Changed Checkpoints",
            "The checkpoints take other paths down the mountain, picked by the layout.");
        UnlimitedSprinting = _category.CreateEntry("UnlimitedSprinting", false, "Unlimited Sprinting", "Sprinting never runs out.");
        BetterFlyingControl = _category.CreateEntry("BetterFlyingControl", false, "Better Flying Control", "More control over the bike in the air.");
        Mode = _category.CreateEntry("Mode", RideMode.Normal, "Mode", "Normal, or FreeRider (one life, like the Free Rider challenges).");
        ObstacleLayout = _category.CreateEntry("ObstacleLayout", 1, "Obstacle layout",
            "Where the extra obstacles stand (1 - 999); the same number gives the same obstacles on a trail. 0 means the Seed below, from a ride code.");
        CheckpointRoute = _category.CreateEntry("CheckpointRoute", 1, "Checkpoint route",
            "Which way the changed checkpoints go (1 - 999); the same number gives the same route on a trail. 0 means the Path below, from a ride code.");
        Seed = _category.CreateEntry("Seed", 0, "Seed", "Obstacle seed from a ride code (used when ObstacleLayout is 0).");
        Path = _category.CreateEntry("Path", "", "Path", "Checkpoint path digits from a ride code (used when CheckpointRoute is 0).");
        Bike = _category.CreateEntry("Bike", 0, "Bike",
            "The custom ride's bike (vehicle ID, 7000 = Super Bike), chosen again when the Custom Rides tab opens. 0 = the game's selected bike.");
        Route = _category.CreateEntry("Route", "", "Trail",
            "New Ride's trail (e.g. AL_01_A2), its own: the game's Mountains tab keeps its selection. Empty = the game's selected trail.");
        Night = _category.CreateEntry("Night", false, "Night", "New Ride's time of day: true for night.");
        MenuRoute = _category.CreateEntry("MenuRoute", "", "Mountains tab trail",
            "The Mountains tab's trail when the Custom Rides tab last opened: a different one there is taken over by New Ride.");
        MenuNight = _category.CreateEntry("MenuNight", false, "Mountains tab night", "The Mountains tab's time of day then.");
        Playlist = _category.CreateEntry("Playlist", "", "Playlist", "The playlist last chosen on the Playlists page.");
        SpeedrunMode = _category.CreateEntry("SpeedrunClock", false, "Speedrun mode",
            "Playlist runs are speedruns: one attempt timed from the first input to the last finish line (crashes included), no result screens, the run clock in the HUD; the Playlists page shows the speedrun PB. Off: result screens after each ride (Restart Run rides it again), and the page shows the sum of the rides' best times.");
        ModPlaylistsWritten = _category.CreateEntry("ModPlaylistsWritten", false, "Mod playlists written",
            "The mod's own playlists were written to the Playlists folder once (deleted ones stay gone).");
        Clamp();
        _category.SaveToFile(false);
    }

    public static void Save()
    {
        Clamp();
        _category.SaveToFile(false);
    }

    /// <summary>Picks up edits made to LMDCustomRides.cfg while the game runs.</summary>
    public static void Reload()
    {
        _category.LoadFromFile(false);
        Clamp();
    }

    private static void Clamp()
    {
        ObstacleLayout.Value = Math.Clamp(ObstacleLayout.Value, 0, Loadout.MaxLayout);
        CheckpointRoute.Value = Math.Clamp(CheckpointRoute.Value, 0, Loadout.MaxLayout);
        if (ObstacleLayout.Value == 0 && Seed.Value <= 0)
        {
            ObstacleLayout.Value = 1;
        }

        if (CheckpointRoute.Value == 0 && string.IsNullOrEmpty(Path.Value))
        {
            CheckpointRoute.Value = 1;
        }
    }
}