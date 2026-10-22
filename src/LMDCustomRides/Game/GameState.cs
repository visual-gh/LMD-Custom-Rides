using Il2Cpp;
using Il2CppMegagon.Downhill;
using Il2CppMegagon.Downhill.Config;
using Il2CppMegagon.Downhill.Managers;
using Il2CppMegagon.Downhill.Modifier;
using Il2CppMegagon.Downhill.SaveGame.Parameter;
using Il2CppMegagon.Downhill.Vehicle.Configuration;

namespace LMDCustomRides.Game;

/// <summary>
/// The game state this mod reads and writes. The game's names are obfuscated, so the interop names are
/// mapped here once (identified with a dev-only recon mod, not part of this repo).
/// </summary>
internal static class GameState
{
    /// <summary>VehicleConfiguration "Bike_G", the daily rides' Super Bike.</summary>
    public const int SuperBikeVehicleId = 7000;

    /// <summary>GameMode "GM_Normal" (daily ride token N).</summary>
    public const int NormalModeId = 1;

    /// <summary>GameMode "GM_OneLife", shown as Free Rider (daily ride token O).</summary>
    public const int FreeRiderModeId = 4;

    /// <summary>True while the game's own Daily Ride is set up; this mod leaves those rides alone.</summary>
    public static bool DailyRideActive
    {
        get
        {
            try
            {
                return RandomChallengeManager.prop_Boolean_0;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Today's Daily Ride as a ride code ("IS_01_A1/D/N/7/..."), or null before it is known.</summary>
    public static string DailyRideCode
    {
        get
        {
            try
            {
                var code = Manager<RandomChallengeManager>.prop_T_0?.field_Private_String_0;
                return RideCode.TryParse(code, out _) ? code : null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// The profile's game properties (selected bike, trail, mode). Null before the save manager has loaded the profile
    /// (read through its fields: the property getter throws until then).
    /// </summary>
    public static PlayerProfileParameter.GamePropertiesSettings Properties =>
        ObjectPublicOb1SePl1BoInInSePlUnique.field_Private_Static_ObjectPublicOb1SePl1BoInInSePlUnique_0?
            .field_Private_Object1PublicStObBo1UIObBoObBoObUnique_1_PlayerProfileParameter_0?.field_Private_T_0?.prop;

    public static int ActiveVehicle => Properties?.vhl ?? 0;

    public static bool SuperBikeSelected => ActiveVehicle == SuperBikeVehicleId;

    /// <summary>The selected trail, e.g. "AL_01_A1".</summary>
    public static string ActiveRoute => Properties?.trID;

    /// <summary>The selected time of day: 1 day, 2 night.</summary>
    public static int TimeOfDay
    {
        get
        {
            try
            {
                return (int)MountainManager.prop_EnumPublicSealedva5v2_0;
            }
            catch
            {
                return 1;
            }
        }
    }

    public static int GameModeId
    {
        get
        {
            try
            {
                return MountainManager.prop_GameMode_0?.ID ?? 0;
            }
            catch
            {
                return 0;
            }
        }
        set
        {
            try
            {
                MountainManager.Method_Public_Static_Boolean_Int32_1(value);
            }
            catch (Exception e)
            {
                Mod.Log.Warning("could not set the game mode: " + e.Message);
            }
        }
    }

    public static VehicleConfiguration Vehicle(int id)
    {
        try
        {
            var all = GameConfig.ActiveConfig?.vehicleDB?.m_allVehicleConfigurations;
            if (all != null)
            {
                foreach (var bike in all)
                {
                    if (bike != null && bike.ID == id)
                    {
                        return bike;
                    }
                }
            }
        }
        catch
        {
            // the game config is not loaded yet
        }

        return null;
    }

    /// <summary>The game's modifier definitions (name, icon) by ID.</summary>
    public static ModifierConfiguration Modifier(string id)
    {
        try
        {
            var all = GameConfig.ActiveConfig?.modifierDB?.m_fullConfigurations;
            if (all != null)
            {
                foreach (var config in all)
                {
                    if (config != null && config.ID == id)
                    {
                        return config;
                    }
                }
            }
        }
        catch
        {
            // the game config is not loaded yet
        }

        return null;
    }

    /// <summary>"Graterhorn | Trail 2" for a trail ID, as the game's menu headers show it.</summary>
    public static string TrailHeadline(string route)
    {
        try
        {
            return string.IsNullOrEmpty(route) ? "" : MountainManager.Method_Public_Static_String_String_0(route);
        }
        catch
        {
            return route ?? "";
        }
    }

    public static string MountainName(string route)
    {
        try
        {
            var mountain = string.IsNullOrEmpty(route) ? null : MountainManager.Method_Public_Static_Mountain_String_0(route.Substring(0, 2));
            return mountain == null ? "" : Texts.Get(mountain.m_mountainInfo.m_mountainNameLocaKey);
        }
        catch
        {
            return "";
        }
    }

    private static ModifierManager Modifiers => Manager<ModifierManager>.prop_T_0;

    /// <summary>Modifier configurations the game currently has active (its own or ours).</summary>
    public static int ActiveModifierCount => Modifiers?.field_Private_List_1_ModifierConfiguration_0?.Count ?? 0;

    /// <summary>The game's active modifier configurations, in its order.</summary>
    public static IEnumerable<ModifierConfiguration> ActiveModifiers
    {
        get
        {
            var list = Modifiers?.field_Private_List_1_ModifierConfiguration_0;
            for (var i = 0; list != null && i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    yield return list[i];
                }
            }
        }
    }

    /// <summary>
    /// Puts a loadout into the game's ModifierManager the way the daily ride does: obstacle seed, checkpoint path and
    /// modifier IDs. Only the configuration and effect lists are emptied first; the trail's obstacle groups stay
    /// registered, so this also works after the trail scene has loaded.
    /// </summary>
    public static bool Apply(Loadout loadout)
    {
        var manager = Modifiers;
        if (manager == null)
        {
            return false;
        }

        manager.field_Private_List_1_ModifierConfiguration_0?.Clear();
        manager.field_Private_List_1_ModifierEffectBase_0?.Clear();
        ModifierManager.Method_Public_Static_Void_Int32_0(loadout.Seed);
        ModifierManager.Method_Public_Static_Void_String_1(loadout.Path);
        if (loadout.IsModified)
        {
            ModifierManager.Method_Public_Static_Void_String_0(loadout.ModifierIds);
        }

        return true;
    }

    /// <summary>Removes all modifiers this mod put in (a normal ride after a custom one).</summary>
    public static void ClearModifiers()
    {
        var manager = Modifiers;
        manager?.field_Private_List_1_ModifierConfiguration_0?.Clear();
        manager?.field_Private_List_1_ModifierEffectBase_0?.Clear();
    }
}
