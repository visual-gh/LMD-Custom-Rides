using Il2CppMegagon.Downhill;
using Il2CppMegagon.Downhill.Config;
using Il2CppMegagon.Downhill.Managers;
using Il2CppMegagon.Downhill.Vehicle.Configuration;

namespace LMDCustomRides.Game;

/// <summary>
/// The custom ride's trail, time of day and bike, set in the game directly (no stepping through the game's mountain
/// and trail screens), and the start of the ride.
/// </summary>
internal static class CustomRides
{
    /// <summary>The playable mountains in the game's order.</summary>
    public static List<Mountain> Mountains()
    {
        var list = new List<Mountain>();
        try
        {
            var all = Manager<MountainManager>.prop_T_0?.field_Private_List_1_Mountain_0;
            if (all != null)
            {
                foreach (var mountain in all)
                {
                    if (mountain != null && mountain.IsPlayable)
                    {
                        list.Add(mountain);
                    }
                }
            }
        }
        catch
        {
            // not loaded yet
        }

        return list;
    }

    /// <summary>The selected mountain, or null.</summary>
    public static Mountain ActiveMountain
    {
        get
        {
            try
            {
                return MountainManager.Method_Public_Static_Mountain_0();
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>The playable trails of a mountain, in the order its trail screen lists them.</summary>
    public static List<string> Trails(Mountain mountain)
    {
        var list = new List<string>();
        if (mountain == null)
        {
            return list;
        }

        foreach (var part in mountain.m_subMountains)
        {
            if (part == null || !part.IsPlayable)
            {
                continue;
            }

            foreach (var info in part.m_trackRouteInfos)
            {
                if (info != null)
                {
                    list.Add(info.m_fullTrackRouteID);
                }
            }
        }

        return list;
    }

    /// <summary>"Trail 2": the trail part of the game's "Graterhorn | Trail 2".</summary>
    public static string TrailName(string route)
    {
        var headline = GameState.TrailHeadline(route);
        var bar = headline.IndexOf('|');
        return bar >= 0 ? headline.Substring(bar + 1).Trim() : headline;
    }

    /// <summary>
    /// Selects a trail (and its mountain) as the game's screens do; the game's trail index counts across all parts of
    /// the mountain. False if the trail is not playable.
    /// </summary>
    public static bool SelectRoute(string route)
    {
        try
        {
            var mountain = PlayableMountain(route);
            if (mountain == null)
            {
                return false;
            }

            var index = 0;
            foreach (var part in mountain.m_subMountains)
            {
                foreach (var info in part.m_trackRouteInfos)
                {
                    if (info.m_fullTrackRouteID == route)
                    {
                        if (ActiveMountain?.m_mountainID != mountain.m_mountainID)
                        {
                            MountainManager.Method_Public_Static_Boolean_Mountain_0(mountain);
                        }

                        MountainManager.Method_Public_Static_Boolean_Int32_0(index);
                        return GameState.ActiveRoute == route;
                    }

                    index++;
                }
            }
        }
        catch (Exception e)
        {
            Mod.Log.Warning("could not select the trail: " + e.Message);
        }

        return false;
    }

    /// <summary>The trail's mountain if the trail can be ridden here (its DLC owned), else null.</summary>
    private static Mountain PlayableMountain(string route)
    {
        var mountain = string.IsNullOrEmpty(route) || route.Length < 2 ? null : MountainManager.Method_Public_Static_Mountain_String_0(route.Substring(0, 2));
        return mountain != null && mountain.IsPlayable && Trails(mountain).Contains(route) ? mountain : null;
    }

    /// <summary>
    /// The DLC a trail needs when the player does not have it (not owned, or not installed: the Eldfjall Island DLC,
    /// the free fifth trails), else null.
    /// </summary>
    public static DLCConfig MissingDlc(string route)
    {
        try
        {
            var mountain = string.IsNullOrEmpty(route) || route.Length < 2 ? null : MountainManager.Method_Public_Static_Mountain_String_0(route.Substring(0, 2));
            foreach (var part in mountain?.m_subMountains ?? Array.Empty<SubMountain>())
            {
                if (part != null && part.m_dlcConfig != null && !part.IsPlayable && part.m_trackRouteInfos.Any(i => i?.m_fullTrackRouteID == route))
                {
                    return part.m_dlcConfig;
                }
            }
        }
        catch
        {
            // the mountains are not loaded yet
        }

        return null;
    }

    /// <summary>Why a ride code cannot be ridden here (a DLC the player does not have, an unknown trail), or null.
    /// Playlists check every ride before a run starts.</summary>
    public static string Unavailable(RideCode code)
    {
        if (MissingDlc(code.Route) is { } dlc)
        {
            return string.Format(Texts.Get("LMDMOD_NEEDSDLC"), Texts.Get(dlc.m_nameLocaKey));
        }

        return PlayableMountain(code.Route) == null ? Texts.Get("LMDMOD_NOTAVAILABLE") + ": " + GameState.TrailHeadline(code.Route) : null;
    }

    /// <summary>The game's trail index of the selected trail (across the mountain's parts), for the trail map.</summary>
    public static int ActiveRouteIndex
    {
        get
        {
            var route = GameState.ActiveRoute;
            var mountain = ActiveMountain;
            var index = 0;
            if (mountain == null)
            {
                return 0;
            }

            foreach (var part in mountain.m_subMountains)
            {
                foreach (var info in part.m_trackRouteInfos)
                {
                    if (info.m_fullTrackRouteID == route)
                    {
                        return index;
                    }

                    index++;
                }
            }

            return 0;
        }
    }

    /// <summary>The bikes to choose from: every bike of the game (unlocked or not, as in Daily Rides), then the Super Bike.</summary>
    public static List<VehicleConfiguration> Bikes()
    {
        var list = new List<VehicleConfiguration>();
        try
        {
            var all = Il2CppMegagon.Downhill.Config.GameConfig.ActiveConfig?.vehicleDB?.m_allVehicleConfigurations;
            if (all != null)
            {
                foreach (var bike in all)
                {
                    if (bike != null && bike.userSelectable && bike.ID != GameState.SuperBikeVehicleId)
                    {
                        list.Add(bike);
                    }
                }
            }
        }
        catch
        {
            // the game config is not loaded yet
        }

        var super = GameState.Vehicle(GameState.SuperBikeVehicleId);
        if (super != null)
        {
            list.Add(super);
        }

        return list;
    }

    public static void SetTimeOfDay(bool night)
    {
        try
        {
            MountainManager.Method_Public_Static_Boolean_EnumPublicSealedva5v2_0((Il2Cpp.EnumPublicSealedva5v2)(night ? 2 : 1));
        }
        catch (Exception e)
        {
            Mod.Log.Warning("could not set the time of day: " + e.Message);
        }
    }

    /// <summary>
    /// Selects any bike, unlocked or not (custom rides only: leaving the tab puts the Mountains tab's bike back, see
    /// CustomSession); the Super Bike gets a paint job to show. Through the game's own setter, as its bike screen does:
    /// it marks the rider's bike for rebuilding, else a ride on the trail already loaded keeps the bike it had.
    /// </summary>
    public static void SetBike(int vehicleId)
    {
        var properties = GameState.Properties;
        var bike = GameState.Vehicle(vehicleId);
        if (properties == null || bike == null)
        {
            return;
        }

        if (vehicleId == GameState.SuperBikeVehicleId)
        {
            SuperBike.Dress(bike);
        }

        properties.ActiveVehicle = vehicleId;
    }

    /// <summary>Applies a ride code to the setup; false if its trail is not playable here (the rest still applies).</summary>
    public static bool Apply(RideCode code)
    {
        Settings.Mirrored.Value = code.Has(Loadout.MirrorId);
        Settings.Obstacles.Value = code.Has(Loadout.ManyObstaclesId) ? Obstacles.Many
            : code.Has(Loadout.FewObstaclesId) ? Obstacles.Few
            : Obstacles.Off;
        Settings.ChangedCheckpoints.Value = code.Has(Loadout.ChangedCheckpointsId);
        Settings.UnlimitedSprinting.Value = code.Has(Loadout.UnlimitedSprintingId);
        Settings.BetterFlyingControl.Value = code.Has(Loadout.BetterFlyingControlId);
        Settings.Mode.Value = code.Mode;

        var layout = Loadout.FindLayout(code.Seed);
        Settings.ObstacleLayout.Value = layout;
        if (layout == 0)
        {
            Settings.Seed.Value = code.Seed;
        }

        var route = Loadout.FindRoute(code.Path);
        Settings.CheckpointRoute.Value = route;
        if (route == 0)
        {
            Settings.Path.Value = code.Path;
        }

        Settings.Save();
        CustomSession.ApplyMode();
        SetTimeOfDay(code.Night);
        SetBike(code.Bike * 1000);
        Mod.Log.Msg("ride code applied: " + code);
        Settings.Night.Value = code.Night;
        if (SelectRoute(code.Route))
        {
            Settings.Route.Value = code.Route;
            Settings.Save();
            return true;
        }

        Mod.Log.Warning("ride code trail not available: " + code.Route);
        return false;
    }

    /// <summary>A ride code's trail, time of day and bike in the game, leaving the New Ride setup as it is (playlists).</summary>
    public static bool ApplyGameState(RideCode code)
    {
        SetTimeOfDay(code.Night);
        SetBike(code.Bike * 1000);
        return SelectRoute(code.Route);
    }

    /// <summary>Starts the custom ride on the selected trail (from the Custom Rides tab).</summary>
    public static bool Start(RideCode code)
    {
        if (!CustomSession.Active)
        {
            return false;
        }

        Settings.Save();
        CustomSession.ApplyMode();
        Mod.Log.Msg($"custom ride start: {code?.Route ?? GameState.ActiveRoute}, {CustomSession.Loadout}");
        return CustomTab.StartRide();
    }
}
