using HarmonyLib;
using Il2CppMegagon.Downhill.UI.Screens;
using LMDCustomRides.Ui.Kit;
using UnityEngine;

namespace LMDCustomRides.Game;

/// <summary>
/// The "Custom Rides" entry in the game menu's top bar, inserted after Daily Rides: a copy of the Progression menu,
/// so it has the game's own sub-tabs ([F] / [G]): New Ride and Playlists. Its pages are copies of the
/// Progression pages with their content switched off; this mod fills them.
/// </summary>
internal static class CustomTab
{
    public const string TitleKey = "LMDMOD_CUSTOMRIDES";
    public const string NewRideKey = "LMDMOD_NEWRIDE";
    public const string PlaylistsKey = "LMDMOD_PLAYLISTS";

    // Bottom bar action labels: the game puts the key in for {0}.
    public const string ActionStartRideKey = "LMDMOD_ACTION_STARTRIDE";
    public const string ActionStartPlaylistKey = "LMDMOD_ACTION_STARTPLAYLIST";
    public const string ActionRideAgainKey = "LMDMOD_ACTION_RIDEAGAIN";
    public const string ActionCreatePlaylistKey = "LMDMOD_ACTION_CREATEPLAYLIST";
    private const int Index = 2;

    public const int NewRidePage = 0;
    public const int PlaylistsPage = 1;
    public const int PageCount = 2;

    private static GameObject _template;

    public static GameMenu GameMenu { get; private set; }

    /// <summary>The tab's menu (the copy of the Progression menu).</summary>
    public static GameMenuSubMenu Menu { get; private set; }

    /// <summary>A page of the tab once the menu has made it (index: NewRidePage, PlaylistsPage).</summary>
    public static GameMenuScreenBase Page(int index)
    {
        var pages = Menu == null ? null : Menu.field_Private_List_1_GameMenuScreenBase_0;
        return pages != null && index < pages.Count ? pages[index] : null;
    }

    /// <summary>True when the menu currently shown is this tab.</summary>
    public static bool InCustomMenu => GameMenu != null && Menu != null && GameMenu.m_currentSubMenu != null && GameMenu.m_currentSubMenu.Pointer == Menu.Pointer;

    /// <summary>True when the game menu shows some menu (not riding, not on the main menu).</summary>
    public static bool InAnyMenu => GameMenu != null && GameMenu.m_currentSubMenu != null && GameMenu.gameObject.activeInHierarchy;

    /// <summary>The page the tab shows (NewRidePage...), or -1 when the tab is not shown.</summary>
    public static int CurrentPage
    {
        get
        {
            for (var i = 0; i < PageCount; i++)
            {
                if (Showing(i))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    /// <summary>True when the tab is open on the given page.</summary>
    public static bool Showing(int page)
    {
        var screen = Page(page);
        return InCustomMenu && screen != null && Menu.m_currentScreen != null && Menu.m_currentScreen.Pointer == screen.Pointer;
    }

    /// <summary>The game menu sets up its menus and the top bar here, once.</summary>
    [HarmonyPatch(typeof(GameMenu), nameof(GameMenu.Method_Protected_Virtual_Void_0))]
    private static class MenuInit
    {
        private static void Prefix(GameMenu __instance)
        {
            var prefabs = __instance.m_subMenuPrefabs;
            if (prefabs == null || prefabs.Count == 0)
            {
                return;
            }

            for (var i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i] != null && _template != null && prefabs[i].gameObject.Pointer == _template.Pointer)
                {
                    return;
                }
            }

            // The top bar has one prebuilt button per menu; the new menu gets a copy of the Daily Rides button.
            var buttons = __instance.m_navigation?.m_buttons;
            if (buttons == null || buttons.Count != prefabs.Count || buttons.Count < Index)
            {
                Mod.Log.Warning("custom rides: the top bar is not as expected, not added");
                return;
            }

            GameMenuSubMenu progression = null, daily = null;
            foreach (var prefab in prefabs)
            {
                if (prefab != null && prefab.TryCast<ProgressionMenu>() != null)
                {
                    progression = prefab;
                }
                else if (prefab != null && prefab.TryCast<DailyChallengesMenu>() != null)
                {
                    daily = prefab;
                }
            }

            if (progression == null)
            {
                Mod.Log.Warning("custom rides: the Progression menu was not found, not added");
                return;
            }

            var source = buttons[Index - 1];
            var button = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent);
            button.name = "NavigationButton_CustomRides";
            button.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            buttons.Insert(Index, button.GetComponent<GameMenuNavigationButton>());

            prefabs.Insert(Index, Template(progression, prefabs[0], daily));
        }

        private static void Postfix(GameMenu __instance)
        {
            var menus = __instance.field_Private_List_1_GameMenuSubMenu_0;
            if (menus == null)
            {
                return;
            }

            foreach (var menu in menus)
            {
                if (menu != null && menu.name.StartsWith("CustomRidesMenu", StringComparison.Ordinal))
                {
                    GameMenu = __instance;
                    Menu = menu;
                    Mod.Log.Msg("custom rides tab ready");
                    return;
                }
            }
        }
    }

    /// <summary>
    /// The tab's menu: a copy of the Progression menu (kept as an inactive, prefab-like object the game clones) with
    /// the Daily Rides' mode flag as its icon, and blank copies of the Progression pages under this mod's titles.
    /// </summary>
    private static GameMenuSubMenu Template(GameMenuSubMenu progression, GameMenuSubMenu mountains, GameMenuSubMenu daily)
    {
        if (_template != null)
        {
            return _template.GetComponent<GameMenuSubMenu>();
        }

        var holder = new GameObject("LMDCustomRides_Templates");
        holder.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(holder);
        _template = UnityEngine.Object.Instantiate(progression.gameObject, holder.transform);
        _template.name = "CustomRidesMenu";
        var menu = _template.GetComponent<GameMenuSubMenu>();
        menu.m_locaID = TitleKey;
        menu.m_navigationIcon = GameAssets.Sprite(Tokens.TabSprite) ?? mountains.m_navigationIcon;
        menu.m_headerColor = mountains.m_headerColor;

        var source = menu.m_subScreenPrefabs;
        var pages = new Il2CppSystem.Collections.Generic.List<GameMenuScreenBase>();
        string[] titles = { NewRideKey, PlaylistsKey };
        Sprite[] icons = { mountains.m_navigationIcon, daily?.m_navigationIcon };
        string[] actions = { ActionStartRideKey, ActionStartPlaylistKey };
        int[] from = { 0, 1 };
        for (var i = 0; i < titles.Length && from[i] < source.Count; i++)
        {
            var copy = UnityEngine.Object.Instantiate(source[from[i]].gameObject, holder.transform);
            copy.name = "CustomRidesPage_" + titles[i];
            var page = copy.GetComponent<GameMenuScreenBase>();
            Blank(page);
            page.m_locaID = titles[i];
            if (icons[i] != null)
            {
                page.m_navigationIcon = icons[i];
            }

            // Each page's bottom bar action is its own (see NewRidePage and Page).
            var sub = copy.GetComponent<GameMenuSubScreen>();
            if (sub != null)
            {
                sub.m_showActionButton = true;
                sub.m_actionLocaID = actions[i];
            }

            if (i == NewRidePage)
            {
                AddTrailMap(page, mountains);
            }

            pages.Add(page);
        }

        menu.m_subScreenPrefabs = pages;
        return menu;
    }

    public const string TrailMapName = "LMDCustomRides_TrailMap";

    /// <summary>
    /// The trail screen's map, copied from the trail menu's prefab into the page, as one of its elements: the page sets
    /// it up (its mountain maps and trail lines) and shows and hides it like the trail menu does its own.
    /// </summary>
    private static void AddTrailMap(GameMenuScreenBase page, GameMenuSubMenu mountains)
    {
        var trailMenu = mountains.m_nextScreenPrefab;
        var map = trailMenu == null ? null : trailMenu.transform.Find("TrailMenu_MapDisplay");
        if (map == null)
        {
            Mod.Log.Warning("custom rides: the trail map was not found");
            return;
        }

        var copy = UnityEngine.Object.Instantiate(map.gameObject, page.transform, false);
        copy.name = TrailMapName;
        copy.transform.SetAsFirstSibling();
        copy.SetActive(true);
        page.m_screenElements?.Add(copy.GetComponent<ScreenElement>());
    }

    /// <summary>
    /// Empties a copied page: its content off, and out of the page's element list (the page switches its elements
    /// back on when it shows). Destroying the content instead breaks the game menu.
    /// </summary>
    private static void Blank(GameMenuScreenBase page)
    {
        page.m_screenElements?.Clear();
        var t = page.transform;
        for (var i = 0; i < t.childCount; i++)
        {
            t.GetChild(i).gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// The game menu's start of a ride, as Daily Rides uses it: hides the menu and loads the selected trail. Only from
    /// the menu (it needs a current menu).
    /// </summary>
    public static bool StartRide()
    {
        if (GameMenu == null || GameMenu.m_currentSubMenu == null)
        {
            return false;
        }

        _returnPage = CurrentPage;
        CustomSession.RodeMountain = GameState.ActiveRoute is { Length: >= 2 } route ? route.Substring(0, 2) : null;
        GameMenu.Method_Public_Void_1();
        return true;
    }

    // The page the last ride started from: after the ride the tab opens on it again (the game would open its first page).
    private static int _returnPage = -1;

    /// <summary>A menu of the game menu shows, on its page at field_Private_Int32_0: the tab, after a ride, on the ride's page.</summary>
    [HarmonyPatch(typeof(GameMenuSubMenu), nameof(GameMenuSubMenu.Method_Public_Virtual_Void_Action_Action_0))]
    private static class ShowOnRidePage
    {
        private static void Prefix(GameMenuSubMenu __instance)
        {
            if (_returnPage >= 0 && __instance.name.StartsWith("CustomRidesMenu", StringComparison.Ordinal))
            {
                __instance.field_Private_Int32_0 = _returnPage;
                _returnPage = -1;
            }
        }
    }

}
