using System.Text;

namespace LMDCustomRides;

/// <summary>
/// What the game's ModifierManager gets for a ride, in the daily ride's terms: modifier IDs ("MM/OM/US"),
/// an obstacle seed and a checkpoint path (one digit per branch of the trail's checkpoint tree).
/// A layout number decides seed and path the same way on every PC, so a layout can be shared.
/// </summary>
internal readonly struct Loadout
{
    public const int MaxLayout = 999;

    // Modifier IDs of the game's ModifierConfiguration assets.
    public const string MirrorId = "MM";
    public const string FewObstaclesId = "OF";
    public const string ManyObstaclesId = "OM";
    public const string ChangedCheckpointsId = "ACP";
    public const string UnlimitedSprintingId = "US";
    public const string BetterFlyingControlId = "BAC";
    public const string SuperBikeId = "MB";

    public string ModifierIds { get; }
    public int Seed { get; }
    public string Path { get; }

    public bool IsModified => ModifierIds.Length > 0;

    private Loadout(string modifierIds, int seed, string path)
    {
        ModifierIds = modifierIds;
        Seed = seed;
        Path = path;
    }

    /// <summary>Modifier IDs in the daily ride's order.</summary>
    public static List<string> ModifierList(bool superBike)
    {
        var ids = new List<string>();
        if (superBike)
        {
            ids.Add(SuperBikeId);
        }

        if (Settings.Mirrored.Value)
        {
            ids.Add(MirrorId);
        }

        switch (Settings.Obstacles.Value)
        {
            case Obstacles.Few:
                ids.Add(FewObstaclesId);
                break;
            case Obstacles.Many:
                ids.Add(ManyObstaclesId);
                break;
        }

        // Free Rider rides keep the standard checkpoints (the New Ride page dims the setting then).
        if (Settings.ChangedCheckpoints.Value && Settings.Mode.Value != RideMode.FreeRider)
        {
            ids.Add(ChangedCheckpointsId);
        }

        if (Settings.UnlimitedSprinting.Value)
        {
            ids.Add(UnlimitedSprintingId);
        }

        if (Settings.BetterFlyingControl.Value)
        {
            ids.Add(BetterFlyingControlId);
        }

        return ids;
    }

    public static Loadout FromSettings(bool superBike)
    {
        var (seed, path) = SeedAndPath();
        return new Loadout(string.Join("/", ModifierList(superBike)), seed, path);
    }

    /// <summary>A ride code's loadout (a playlist ride; codes are normalized, so "MB" goes with bike 7).</summary>
    public static Loadout FromCode(RideCode code) => new(string.Join("/", code.Modifiers), code.Seed, code.Path);

    public static (int Seed, string Path) SeedAndPath() => (Seed: ObstacleSeed(), Path: CheckpointPath());

    /// <summary>The obstacle seed of the setup: from its obstacle layout number, or the one from a ride code.</summary>
    public static int ObstacleSeed() =>
        Settings.ObstacleLayout.Value > 0 ? LayoutSeed(Settings.ObstacleLayout.Value) : Math.Max(1, Settings.Seed.Value);

    /// <summary>The checkpoint path of the setup: from its route number, or the one from a ride code.</summary>
    public static string CheckpointPath() =>
        Settings.CheckpointRoute.Value > 0 ? RoutePath(Settings.CheckpointRoute.Value) : (Settings.Path.Value ?? "").PadRight(15, '0');

    /// <summary>Obstacle seed for an obstacle layout number (SplitMix64, in the daily rides' range).</summary>
    public static int LayoutSeed(int layout)
    {
        var state = (ulong)layout * 0x9E3779B97F4A7C15UL;
        return (int)(Next(ref state) % 99_999_999UL) + 1;
    }

    /// <summary>15-digit checkpoint path for a checkpoint route number (one digit per branch of the trail's tree).</summary>
    public static string RoutePath(int route)
    {
        var state = (ulong)route * 0xD1B54A32D192ED03UL;
        var path = new StringBuilder(15);
        for (var i = 0; i < 15; i++)
        {
            path.Append((char)('0' + (int)(Next(ref state) % 10UL)));
        }

        return path.ToString();
    }

    /// <summary>The obstacle layout number giving this seed, or 0 if none does (a daily ride's seed).</summary>
    public static int FindLayout(int seed)
    {
        for (var n = 1; n <= MaxLayout; n++)
        {
            if (LayoutSeed(n) == seed)
            {
                return n;
            }
        }

        return 0;
    }

    /// <summary>The checkpoint route number giving this path, or 0 if none does.</summary>
    public static int FindRoute(string path)
    {
        for (var n = 1; n <= MaxLayout; n++)
        {
            if (RoutePath(n) == path)
            {
                return n;
            }
        }

        return 0;
    }
    private static ulong Next(ref ulong state)
    {
        var z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public override string ToString() => IsModified ? $"{ModifierIds} (seed {Seed}, path {Path})" : "no modifiers";
}
