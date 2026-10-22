using System.Text.RegularExpressions;
using Il2CppMegagon.Downhill.UI.Screens;
using LMDCustomRides.Game;
using LMDCustomRides.Ui.Kit;
using UnityEngine;

namespace LMDCustomRides.Ui;

/// <summary>
/// A ride as table cells: # / Trail (the mountain and its trail, "Graterhorn · 2/4") / Mode (bike
/// icon, time of day icon, "Normal" or "Free Rider": the game has no mode icon, it writes modes out; then the ride's
/// modifiers in New Ride's order, packed right after the text: base setup, then modifiers). The mode floats between the
/// trail and the time. Icons are the ones the Daily Rides panel shows.
/// </summary>
internal static class RideIcons
{
    // New Ride's order: obstacles (few or many), checkpoints, mirrored, sprinting, flying (the Super Bike is the bike).
    private static readonly string[][] ModifierSlots =
    {
        new[] { Loadout.FewObstaclesId, Loadout.ManyObstaclesId },
        new[] { Loadout.ChangedCheckpointsId },
        new[] { Loadout.MirrorId },
        new[] { Loadout.UnlimitedSprintingId },
        new[] { Loadout.BetterFlyingControlId },
    };

    public static readonly TableColumn[] Columns =
    {
        new("LMDMOD_COLNUMBER", Tokens.NumberColumn),
        // The game's lock after the trail when its DLC is missing (Eldfjall Island).
        new("LMDMOD_COLTRAIL", Tokens.TrailColumn, After: 1),
        new("LMDMOD_COLMODE", Tokens.ModeColumn, 2, Floats: true, After: ModifierSlots.Length),
    };


    private static Sprite _day;
    private static Sprite _night;

    public static TableCell[] Cells(RideCode code, int number)
    {
        if (_day == null)
        {
            var display = Resources.FindObjectsOfTypeAll<DailyChallengeOverviewDisplay>().FirstOrDefault();
            _day = display?.m_timeOfDayDayIcon;
            _night = display?.m_timeOfDayNightIcon;
        }

        return new[]
        {
            new TableCell(number.ToString()),
            new TableCell(TrailText(code), CustomRides.MissingDlc(code.Route) == null ? null : new[] { new TableIcon(GameAssets.Sprite(Tokens.LockSprite)) }),
            new TableCell(Texts.ModeName(code.Mode),
                new[] { new TableIcon(GameState.Vehicle(code.Bike * 1000)?.bikeIconThumbnail), new TableIcon(code.Night ? _night : _day) }
                    .Concat(ModifierSlots.Select(ids => ids.FirstOrDefault(code.Has)).Where(id => id != null)
                        .Select(id => new TableIcon(GameState.Modifier(id)?.m_modifierIcon)))
                    .ToList()),
        };
    }

    /// <summary>A ride's trail as the table writes it: "Graterhorn  ·  2/4".</summary>
    public static string TrailText(RideCode code) => $"{GameState.MountainName(code.Route)}  ·  {ShortTrail(code.Route)}";

    // Every mountain's own trails, Eldfjall's too: trails show as "2/4", the free DLC's fifth trails as "5/4" (extras).
    private const int BaseTrails = 4;

    /// <summary>"Trail 2" as "2/4" (other trail names stay whole).</summary>
    private static string ShortTrail(string route)
    {
        var trail = CustomRides.TrailName(route);
        var numbered = Regex.Match(trail, @"^\p{L}+\s+(\d+)$");
        return numbered.Success ? $"{numbered.Groups[1].Value}/{BaseTrails}" : trail;
    }
}
