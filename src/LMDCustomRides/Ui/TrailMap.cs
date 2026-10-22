using Il2CppMegagon.Downhill.UI.Screens;
using LMDCustomRides.Game;
using LMDCustomRides.Ui.Kit;
using UnityEngine;

namespace LMDCustomRides.Ui;

/// <summary>
/// The trail map behind the New Ride page: a copy of the game's trail screen map, driven the way the trail screen
/// drives it (the selected mountain's map for the time of day, zoomed onto the selected trail, moving to the next trail
/// when it changes).
/// The trail's checkpoint markers show as on the game's challenge screen; with changed checkpoints they are hidden and
/// the chosen route's are drawn instead, as copies of the map's markers placed from the trail's checkpoint data (see
/// <see cref="CheckpointMaps"/>).
/// </summary>
internal static class TrailMap
{
    // The trail screen's timings: zoom in when it opens, move on when the trail changes, fade out when it closes.
    private const float OpenZoom = 1f;
    private const float OpenFade = 0.1f;
    private const float ChangeZoom = 0.25f;
    private const float CloseFade = 0.15f;

    private static TrailMenuMapDisplay _display;
    private static string _shownRoute;
    private static string _shownMountain;
    private static bool _shownNight;
    private static readonly HashSet<string> Placed = new();
    private static readonly List<GameObject> Drawn = new();
    private static string _drawnFor;
    private static float _nextCheck;
    private static MapTrail _standardShownOn;

    /// <summary>The page's map (put in by <see cref="CustomTab"/> from the trail menu's prefab, set up by the page).</summary>
    public static void Build(Transform page)
    {
        var map = page.Find(CustomTab.TrailMapName);
        _display = map == null ? null : map.GetComponent<TrailMenuMapDisplay>();
        if (_display == null)
        {
            throw new InvalidOperationException("the New Ride page has no trail map");
        }

        _shownRoute = null;
    }

    /// <summary>The page opens: the mountain's map, zooming onto the trail.</summary>
    public static void Show()
    {
        var route = GameState.ActiveRoute;
        if (_display == null || string.IsNullOrEmpty(route))
        {
            return;
        }

        try
        {
            // The zoom starts from wherever this map was last; the first time that is its default spot, a different
            // one on every mountain. So a map seen for the first time is put straight onto the trail instead.
            if (PutUpMap(route))
            {
                PlaceOn(route);
            }
            else
            {
                _display.Method_Public_Void_String_Single_Single_Boolean_Action_Action_0(route, 0f, OpenZoom, false, null, null);
            }

            _display.Method_Public_Void_Single_Single_Action_Action_1(OpenZoom, OpenFade, null, null);
        }
        catch (Exception e)
        {
            Mod.Log.Warning("trail map: " + e.Message);
        }

        _shownRoute = route;
    }

    /// <summary>
    /// The selected mountain's map for the time of day, set up as the game's trail screen does it: the map, its selected
    /// trail, and every trail's base line (shown in the map's colour for the trails the game shows). True the first time
    /// this map is up.
    /// </summary>
    private static bool PutUpMap(string route)
    {
        ClearDrawn();
        _standardShownOn = null;
        _shownMountain = CustomRides.ActiveMountain?.m_mountainID;
        _shownNight = GameState.TimeOfDay == 2;
        _display.Method_Public_Void_Int32_0(CustomRides.ActiveRouteIndex);
        _display.Method_Public_Void_String_0(route);
        _display.Method_Public_Void_0();
        return Placed.Add($"{_shownMountain}/{_shownNight}");
    }

    /// <summary>
    /// Puts the map straight onto the trail. A move of no length still waits for the next update (and another move in
    /// the same frame would replace it): completed now, the map never shows a frame elsewhere.
    /// </summary>
    private static void PlaceOn(string route)
    {
        _display.Method_Public_Void_String_Single_Single_Boolean_Action_Action_0(route, 0f, 0f, true, null, null);
        if (_display.field_Private_Tween_0 != null)
        {
            Il2CppDG.Tweening.TweenExtensions.Complete(_display.field_Private_Tween_0);
        }
    }

    public static void Hide()
    {
        if (_display == null || _shownRoute == null)
        {
            return;
        }

        try
        {
            _display.Method_Public_Void_Single_Single_Action_Action_0(0f, CloseFade, null, null);
            _display.Method_Public_Void_1();
        }
        catch (Exception e)
        {
            Mod.Log.Warning("trail map: " + e.Message);
        }

        _shownRoute = null;
        _standardShownOn = null;
        ClearDrawn();
    }

    /// <summary>Per frame while the page shows: follows the selected trail, and draws changed checkpoints.</summary>
    public static void Update()
    {
        if (_display == null || _shownRoute == null)
        {
            return;
        }

        var route = GameState.ActiveRoute;
        var night = GameState.TimeOfDay == 2;
        if (!string.IsNullOrEmpty(route) && (route != _shownRoute || night != _shownNight))
        {
            try
            {
                // Another mountain's map, or the other time of day's (the game picks the map by both), is put straight
                // onto the trail: gliding there would start from wherever that map was last. Trails of one map glide.
                if (CustomRides.ActiveMountain?.m_mountainID != _shownMountain || night != _shownNight)
                {
                    PutUpMap(route);
                    PlaceOn(route);
                }
                else
                {
                    _display.Method_Public_Void_String_Single_Single_Boolean_Action_Action_0(route, 0f, ChangeZoom, true, null, null);
                }

                _shownRoute = route;
            }
            catch (Exception e)
            {
                Mod.Log.Warning("trail map: " + e.Message);
            }
        }

        UpdateMarkers();
    }

    private static void UpdateMarkers()
    {
        if (Time.unscaledTime < _nextCheck)
        {
            return;
        }

        _nextCheck = Time.unscaledTime + 0.15f;
        var route = GameState.ActiveRoute;
        MapTrail trail = null;
        foreach (var t in _display.GetComponentsInChildren<MapTrail>(false))
        {
            if (t.m_routeID == route)
            {
                trail = t;
            }
        }

        ShowStandardCheckpoints(trail, !Settings.ChangedCheckpoints.Value && Settings.Mode.Value != RideMode.FreeRider);
        if (!Settings.ChangedCheckpoints.Value || Settings.Mode.Value == RideMode.FreeRider)
        {
            ClearDrawn();
            return;
        }

        if (trail == null)
        {
            return;
        }

        var key = route + "/" + Loadout.CheckpointPath();
        if (key != _drawnFor)
        {
            ClearDrawn();
            CheckpointMaps.Draw(trail, route, Loadout.CheckpointPath(), Drawn);

            // The map fills in its markers while it opens: until they are there, try again.
            if (Drawn.Count > 0)
            {
                _drawnFor = key;
            }
        }
    }

    /// <summary>
    /// The trail's own checkpoint markers, shown and hidden the way the game's challenge screen does it (the trail
    /// screen shows start and finish only). Not in Free Rider, whose rides have no checkpoints, and not with changed
    /// checkpoints, whose markers are drawn instead.
    /// </summary>
    private static void ShowStandardCheckpoints(MapTrail trail, bool show)
    {
        var shown = _standardShownOn != null && trail != null && _standardShownOn.Pointer == trail.Pointer;
        if (trail == null || show == shown)
        {
            return;
        }

        try
        {
            if (show)
            {
                trail.Method_Public_Void_Single_Single_Action_Action_0(0f, 0f, null, null);
                _standardShownOn = trail;
            }
            else
            {
                trail.Method_Public_Void_Single_Single_Action_Action_1(0f, 0f, null, null);
                _standardShownOn = null;
            }
        }
        catch (Exception e)
        {
            Mod.Log.Warning("trail map checkpoints: " + e.Message);
            _standardShownOn = show ? trail : null;
        }
    }

    private static void ClearDrawn()
    {
        foreach (var marker in Drawn)
        {
            if (marker != null)
            {
                UnityEngine.Object.Destroy(marker);
            }
        }

        Drawn.Clear();
        _drawnFor = null;
    }
}
