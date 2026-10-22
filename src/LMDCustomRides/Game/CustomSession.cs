using Il2CppMegagon.Downhill.SaveGame.Parameter;
using Il2CppMegagon.Downhill.UI.Screens;

namespace LMDCustomRides.Game;

/// <summary>
/// Whether the player is in the Custom Rides tab, or riding a ride started from it. Rides started anywhere else
/// (Mountains, Daily Rides) get no modifiers from this mod. Leaving the tab puts back the game mode, bike, trail and
/// time of day the normal menus had, so the Super Bike, a bike not unlocked yet or Free Rider never carry over into a
/// normal ride.
/// </summary>
internal static class CustomSession
{
    private static MainMenuScreen _mainMenu;
    private static int _modeBefore = -1;

    public static bool Active { get; private set; }

    /// <summary>A playlist ride's code: while set, mode and modifiers come from it instead of the New Ride setup.</summary>
    public static RideCode Override { get; set; }

    public static RideMode Mode => Override?.Mode ?? Settings.Mode.Value;

    /// <summary>The ride being ridden (or about to be): the playlist ride's code, or New Ride's.</summary>
    public static RideCode Ride => Override ?? Ui.Pages.NewRidePage.CurrentCode();

    /// <summary>What the next ride gets from this mod: the playlist ride's code, or the New Ride setup.</summary>
    public static Loadout Loadout => Override != null ? Loadout.FromCode(Override) : Loadout.FromSettings(GameState.SuperBikeSelected);

    public static void Update()
    {
        if (CustomTab.InAnyMenu)
        {
            var inCustom = CustomTab.InCustomMenu;
            if (inCustom && !Active)
            {
                Enter();
            }
            else if (!inCustom && Active)
            {
                Leave();
            }

            return;
        }

        if (Active && MainMenuShown)
        {
            Leave();
        }
    }

    private static bool MainMenuShown
    {
        get
        {
            if (_mainMenu == null && CustomTab.GameMenu != null)
            {
                _mainMenu = CustomTab.GameMenu.transform.parent.GetComponentInChildren<MainMenuScreen>(true);
            }

            return _mainMenu != null && _mainMenu.gameObject.activeInHierarchy;
        }
    }

    /// <summary>The game menu switches tabs: the selections change over before the next tab shows.</summary>
    [HarmonyLib.HarmonyPatch(typeof(GameMenu), nameof(GameMenu.Method_Private_Void_GameMenuSubMenu_Action_Action_0))]
    private static class TabSwitch
    {
        private static void Prefix(GameMenuSubMenu param_1)
        {
            var toCustom = param_1 != null && CustomTab.Menu != null && param_1.Pointer == CustomTab.Menu.Pointer;
            if (toCustom && !Active)
            {
                Enter();
            }
            else if (!toCustom && Active)
            {
                Leave();
            }
        }
    }

    // The profile's trail, time of day, mode and bike as the normal menus had them when the tab opened: the bike is put
    // back when the tab is left (New Ride may ride one not unlocked yet), and a profile saved meanwhile gets them all.
    private static (string Trail, int TimeOfDay, int Mode, int Bike)? _menuSaved;

    private static void Enter()
    {
        Active = true;
        var p = GameState.Properties;
        _menuSaved = p == null || string.IsNullOrEmpty(p.trID) ? null : (p.trID, p.tod, p.gm, p.vhl);
        _modeBefore = GameState.GameModeId;
        ApplyMode();
        if (Settings.Bike.Value != 0)
        {
            CustomRides.SetBike(Settings.Bike.Value);
        }

        UseNewRideTrail();
        Mod.Log.Msg("custom rides: entered");
    }

    private static void Leave()
    {
        Active = false;
        if (_modeBefore > 0 && GameState.GameModeId != _modeBefore)
        {
            GameState.GameModeId = _modeBefore;
        }

        if (_menuSaved is { } menu && GameState.Properties is { } p)
        {
            p.ActiveVehicle = menu.Bike;
        }

        SuperBike.PutBackNormalBike();
        PutBackMenuTrail();
        PutBackBackground();
        Mod.Log.Msg("custom rides: left");
    }

    /// <summary>The mountain of the last ride from the tab ("AU"): back in the menu, the game shows it behind the menus.</summary>
    public static string RodeMountain { get; set; }

    /// <summary>
    /// The menu's background goes back to the Mountains tab's mountain after a ride on another one, through the game's
    /// own switch (what its mountain screen does, without the slide).
    /// </summary>
    private static void PutBackBackground()
    {
        var rode = RodeMountain;
        RodeMountain = null;
        var menu = Settings.MenuRoute.Value;
        if (rode == null || string.IsNullOrEmpty(menu) || menu.StartsWith(rode, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var mountain = Il2CppMegagon.Downhill.MountainManager.Method_Public_Static_Mountain_String_0(menu.Substring(0, 2));
            if (mountain != null)
            {
                Il2CppMegagon.Downhill.MenuBackgroundManager.Method_Public_Static_Void_Mountain_EnumNPublicSealedva4vUnique_Action_Action_EnumPublicSealedva18Unique_0(
                    mountain, default, Nothing, Nothing, default);
            }
        }
        catch (Exception e)
        {
            Mod.Log.Warning("menu background: " + e.Message);
        }
    }

    private static readonly Il2CppSystem.Action Nothing = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() => { }));

    /// <summary>
    /// New Ride has its own trail and time of day; the game has one selection, the Mountains tab's. In the tab the game
    /// gets New Ride's, after it the Mountains tab's comes back, so New Ride never moves the Mountains tab. A trail or
    /// time of day picked on the Mountains tab since the last visit is taken over by New Ride.
    /// </summary>
    private static void UseNewRideTrail()
    {
        var route = GameState.ActiveRoute;
        var night = GameState.TimeOfDay == 2;
        if (string.IsNullOrEmpty(route))
        {
            return;
        }

        if (route != Settings.MenuRoute.Value || string.IsNullOrEmpty(Settings.Route.Value))
        {
            Settings.Route.Value = route;
        }

        if (night != Settings.MenuNight.Value)
        {
            Settings.Night.Value = night;
        }

        Settings.MenuRoute.Value = route;
        Settings.MenuNight.Value = night;
        Settings.Save();
        CustomRides.SetTimeOfDay(Settings.Night.Value);
        if (!CustomRides.SelectRoute(Settings.Route.Value))
        {
            Settings.Route.Value = route;
            CustomRides.SelectRoute(route);
        }
    }

    private static void PutBackMenuTrail()
    {
        if (string.IsNullOrEmpty(Settings.MenuRoute.Value))
        {
            return;
        }

        CustomRides.SetTimeOfDay(Settings.MenuNight.Value);
        CustomRides.SelectRoute(Settings.MenuRoute.Value);
    }

    /// <summary>
    /// The profile is turned into JSON here before it is saved (locally and to Steam Cloud). It gets the normal menus'
    /// selection, never the tab's: no Super Bike, and while the tab is open (the game closed from it) the Mountains
    /// tab's trail, time of day, mode and bike, so the next start does not open on New Ride's. The game keeps its own in memory.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(Il2Cpp.ObjectPublicAbstractUIUnique), nameof(Il2Cpp.ObjectPublicAbstractUIUnique.Method_Protected_String_BaseParameter_0))]
    private static class ProfileSave
    {
        private static void Prefix(BaseParameter __0, out (PlayerProfileParameter.GamePropertiesSettings Properties, int Bike, string Trail, int TimeOfDay, int Mode)? __state)
        {
            __state = null;
            var p = __0?.TryCast<PlayerProfileParameter>()?.prop;
            if (p == null || (p.vhl != GameState.SuperBikeVehicleId && !Active))
            {
                return;
            }

            __state = (p, p.vhl, p.trID, p.tod, p.gm);
            if (p.vhl == GameState.SuperBikeVehicleId)
            {
                p.vhl = SuperBike.NormalBike;
            }

            if (Active && _menuSaved is { } menu)
            {
                (p.trID, p.tod, p.gm, p.vhl) = menu;
                if (p.vhl == GameState.SuperBikeVehicleId)
                {
                    p.vhl = SuperBike.NormalBike;
                }
            }
        }

        private static void Postfix((PlayerProfileParameter.GamePropertiesSettings Properties, int Bike, string Trail, int TimeOfDay, int Mode)? __state)
        {
            if (__state is { } s)
            {
                (s.Properties.vhl, s.Properties.trID, s.Properties.tod, s.Properties.gm) = (s.Bike, s.Trail, s.TimeOfDay, s.Mode);
            }
        }
    }

    /// <summary>Sets the game mode (Normal or Free Rider) the custom ride uses.</summary>
    public static void ApplyMode()
    {
        if (!Active)
        {
            return;
        }

        var id = Mode == RideMode.FreeRider ? GameState.FreeRiderModeId : GameState.NormalModeId;
        if (GameState.GameModeId != id)
        {
            GameState.GameModeId = id;
        }
    }
}
