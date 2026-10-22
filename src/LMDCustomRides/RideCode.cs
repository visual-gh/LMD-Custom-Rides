using System.Text.RegularExpressions;

namespace LMDCustomRides;

/// <summary>
/// A whole ride in the daily ride's own format, e.g. "IS_01_A1/D/N/7/33979052/221790175527461/MB/MM/US/BAC":
/// trail, time of day (D/N), mode (N = Normal, O = Free Rider), bike (1-7, 7 = Super Bike), obstacle seed,
/// checkpoint path digits, then the modifier IDs. Daily ride codes can be pasted as they are.
/// </summary>
internal sealed class RideCode
{
    private static readonly Regex Format = new(
        @"^(?<route>[A-Z]{2}_\d{2}_[A-Z]\d)/(?<tod>[DN])/(?<mode>[NO])/(?<bike>[1-7])/(?<seed>\d{1,9})/(?<path>\d{1,30})(?<mods>(/[A-Z]{1,4})*)$",
        RegexOptions.Compiled);

    // In the daily ride's order.
    private static readonly string[] KnownModifiers =
    {
        Loadout.SuperBikeId, Loadout.MirrorId, Loadout.FewObstaclesId, Loadout.ManyObstaclesId,
        Loadout.ChangedCheckpointsId, Loadout.UnlimitedSprintingId, Loadout.BetterFlyingControlId,
    };

    public string Route { get; init; }
    public bool Night { get; init; }
    public RideMode Mode { get; init; }

    /// <summary>1 to 7, the bike's vehicle ID / 1000 (7 is the Super Bike).</summary>
    public int Bike { get; init; }

    public int Seed { get; init; }
    public string Path { get; init; }
    public IReadOnlyList<string> Modifiers { get; init; }

    public bool Has(string id) => Modifiers.Contains(id);

    /// <summary>
    /// The code as one ride always has it, so it keeps one set of times: the modifiers in the daily ride's order, the
    /// Super Bike's "MB" exactly when the bike is 7, no changed checkpoints in Free Rider (it keeps the standard ones);
    /// without obstacles the seed is layout #1's, without changed checkpoints the path is route #1's (a daily ride's
    /// code carries both anyway).
    /// </summary>
    public RideCode Normalized()
    {
        var mods = KnownModifiers.Where(id => id == Loadout.SuperBikeId ? Bike == 7
            : Has(id) && !(id == Loadout.ChangedCheckpointsId && Mode == RideMode.FreeRider)).ToList();
        return new RideCode
        {
            Route = Route,
            Night = Night,
            Mode = Mode,
            Bike = Bike,
            Seed = mods.Contains(Loadout.FewObstaclesId) || mods.Contains(Loadout.ManyObstaclesId) ? Seed : Loadout.LayoutSeed(1),
            Path = mods.Contains(Loadout.ChangedCheckpointsId) ? Path : Loadout.RoutePath(1),
            Modifiers = mods,
        };
    }

    // A code never changes once made (init-only): its text is built once (the Playlists page asks every frame).
    private string _text;

    public override string ToString() => _text ??= Build();

    private string Build()
    {
        var parts = new List<string>
        {
            Route,
            Night ? "N" : "D",
            Mode == RideMode.FreeRider ? "O" : "N",
            Bike.ToString(),
            Seed.ToString(),
            Path,
        };
        parts.AddRange(Modifiers);
        return string.Join("/", parts);
    }

    /// <summary>Reads a code; spaces, case and a leading "#" or surrounding quotes are ignored.</summary>
    public static bool TryParse(string text, out RideCode code)
    {
        code = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var clean = Regex.Replace(text, @"\s+", "").Trim('"', '\'', '#').ToUpperInvariant();
        var match = Format.Match(clean);
        if (!match.Success || !int.TryParse(match.Groups["seed"].Value, out var seed))
        {
            return false;
        }

        var mods = match.Groups["mods"].Value.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(KnownModifiers.Contains).Distinct().ToList();
        code = new RideCode
        {
            Route = match.Groups["route"].Value,
            Night = match.Groups["tod"].Value == "N",
            Mode = match.Groups["mode"].Value == "O" ? RideMode.FreeRider : RideMode.Normal,
            Bike = match.Groups["bike"].Value[0] - '0',
            Seed = seed,
            // The game reads one digit per checkpoint branch; daily rides always have 15.
            Path = match.Groups["path"].Value.PadRight(15, '0'),
            Modifiers = mods,
        }.Normalized();
        return true;
    }

    /// <summary>All codes in a text, one per line or separated by spaces or commas; anything else is skipped.</summary>
    public static List<RideCode> ParseAll(string text)
    {
        var codes = new List<RideCode>();
        if (string.IsNullOrEmpty(text))
        {
            return codes;
        }

        foreach (Match m in Regex.Matches(text.ToUpperInvariant(), @"[A-Z]{2}_\d{2}_[A-Z]\d/[DN]/[NO]/[1-7]/\d{1,9}/\d{1,30}(/[A-Z]{1,4})*"))
        {
            if (TryParse(m.Value, out var code))
            {
                codes.Add(code);
            }
        }

        return codes;
    }
}
