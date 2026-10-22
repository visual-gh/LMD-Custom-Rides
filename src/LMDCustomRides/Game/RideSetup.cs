using HarmonyLib;
using Il2CppMegagon.Downhill;
using Il2CppMegagon.Downhill.TrackElements;

namespace LMDCustomRides.Game;

/// <summary>
/// Puts the custom ride's modifiers into the game right before a ride from the Custom Rides tab loads, the way the
/// daily ride does. Other rides get none; the game's own Daily Ride sets up its modifiers itself and is left alone.
/// </summary>
internal static class RideSetup
{
    /// <summary>When the last ride's route was set up (start or restart), in unscaled time.</summary>
    public static float RouteStartedAt { get; private set; } = -100f;

    public static void ApplyForNextRide(string when)
    {
        // The game's Daily Ride sets up its own; a ride from the Custom Rides tab is never one.
        if (GameState.DailyRideActive && !CustomSession.Active)
        {
            return;
        }

        if (!CustomSession.Active)
        {
            if (GameState.ActiveModifierCount > 0)
            {
                GameState.ClearModifiers();
                Mod.Log.Msg($"{when}: normal ride, modifiers removed");
            }

            return;
        }

        CustomSession.ApplyMode();
        var loadout = CustomSession.Loadout;
        if (!GameState.Apply(loadout))
        {
            Mod.Log.Warning($"{when}: the game's ModifierManager is not ready, modifiers not applied");
            return;
        }

        Mod.Log.Msg($"{when}: custom ride {loadout}");
    }

    /// <summary>Runs when a trail starts loading (before its scene), for normal rides and daily rides alike.</summary>
    [HarmonyPatch(typeof(PlayManager), nameof(PlayManager.Method_Private_Void_PDM_3))]
    private static class TrailLoading
    {
        private static void Prefix() => ApplyForNextRide("trail loading");
    }

    /// <summary>Runs when the trail's route is set up (also on restarts); route setup reads the modifiers right after.</summary>
    [HarmonyPatch(typeof(PlayManager), nameof(PlayManager.Method_Private_Void_TrackRoute_PDM_0))]
    private static class RouteStarting
    {
        private static void Prefix(TrackRoute __0)
        {
            RouteStartedAt = UnityEngine.Time.unscaledTime;
            ApplyForNextRide("route " + __0?.m_trackRouteID);
        }
    }
}
