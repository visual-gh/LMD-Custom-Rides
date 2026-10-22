using HarmonyLib;
using Il2Cpp;
using Il2CppMegagon.Downhill.UI.Screens;
using UnityEngine;

namespace LMDCustomRides.Game;

/// <summary>
/// The ride HUD in custom rides. The game's panels stay on screen the whole ride (section and section time on the left;
/// modifiers, bike, crashes and total time on the right): the game slides them in only now and then, and a custom ride
/// is about the clock.
/// The times next to the clock are the game's own personal best display, fed from this mod's data instead of the
/// leaderboards (which hold the normal trail's times; a custom ride starts with none), whatever the game's Options say:
/// the game asks for each section's best time, and gets the player's best for that section on this exact ride (see
/// <see cref="Records.SectionPassed"/>); past each section its difference to that best pops up. The best time shows only
/// when there is one, always under the personal best icon. Past the finish line the clock keeps the last section's time
/// (the game stops it in the run tracker's finish, which custom rides skip).
/// </summary>
internal static class RideHud
{
    // At or over the game's "no time" mark: the HUD shows no time.
    private const float NoTime = float.PositiveInfinity;

    private static PlayScreen _screen;
    private static CanvasGroup _live;
    private static CanvasGroup _overall;

    // The game's answer to its last best time request in a custom ride.
    private static bool _hasBest;

    /// <summary>The differences of the sections passed last (null: the section had no best yet), for the result screen.</summary>
    public static List<float?> Deltas { get; } = new();

    /// <summary>The last finished ride's difference to the ride's best time before it (null: none yet).</summary>
    public static float? TotalDelta { get; private set; }

    private static PlayScreen Screen
    {
        get
        {
            if (_screen == null && CustomTab.GameMenu != null)
            {
                var group = CustomTab.GameMenu.transform.parent.Find("PlayScreen(Clone)/PlayScreen_Group");
                _screen = group?.parent.GetComponent<PlayScreen>();
                _live = group?.Find("LiveTimePanel_Canvas")?.GetComponent<CanvasGroup>();
                _overall = group?.Find("OverallStatsPanel_Canvas")?.GetComponent<CanvasGroup>();
            }

            return _screen;
        }
    }

    /// <summary>Keeps the time panels in (after the first seconds: the game's camera fly-over and modifiers intro).</summary>
    public static void LateUpdate()
    {
        if (!CustomSession.Active || Screen == null || !_live.gameObject.activeInHierarchy || Time.unscaledTime - RideSetup.RouteStartedAt < 5f)
        {
            return;
        }

        Show(_live);
        Show(_overall);
    }

    private static void Show(CanvasGroup panel)
    {
        if (panel == null)
        {
            return;
        }

        panel.alpha = 1f;
        var rect = panel.GetComponent<RectTransform>();
        if (rect.anchoredPosition.x != 0f)
        {
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
        }
    }

    /// <summary>A section of a custom ride was passed (sections from 0): its time is kept, its difference pops up.</summary>
    public static void SectionPassed(int section, long ms)
    {
        if (!CustomSession.Active || section < 0)
        {
            return;
        }

        var best = Records.SectionPassed(CustomSession.Ride, section, ms);
        while (Deltas.Count <= section)
        {
            Deltas.Add(null);
        }

        Deltas[section] = best > 0 ? (ms - best) / 1000f : null;
        if (Deltas[section] != null && Screen != null)
        {
            Screen.Method_Private_Void_PDM_21();
        }
    }

    /// <summary>A custom ride crossed its finish line in this time: its difference to the ride's best before it.</summary>
    public static void Finished(long totalMs)
    {
        var best = CustomSession.Active ? Records.RideBest(CustomSession.Ride) : null;
        TotalDelta = best is > 0 && totalMs > 0 ? (totalMs - best.Value) / 1000f : null;
    }

    private static void PersonalIcon(PlayScreen screen, UnityEngine.UI.Image image)
    {
        if (image != null && screen.m_personalLeaderboardIcon != null && image.sprite != screen.m_personalLeaderboardIcon)
        {
            image.sprite = screen.m_personalLeaderboardIcon;
        }
    }

    /// <summary>The game asks for a section's best time (at each section's start and when the HUD shows again): the player's own.</summary>
    [HarmonyPatch(typeof(ObjectPublicBoGaObInSiStObDi2LiUnique), nameof(ObjectPublicBoGaObInSiStObDi2LiUnique.Method_Public_Void_Int32_Action_1_Single_0))]
    private static class BestTimeRequest
    {
        private static bool Prefix(int __0, Il2CppSystem.Action<float> __1)
        {
            if (!RideGuard.ModifiedRide)
            {
                return true;
            }

            var best = CustomSession.Active ? Records.SectionBest(CustomSession.Ride, __0) : 0;
            _hasBest = best > 0;
            __1?.Invoke(_hasBest ? best / 1000f : NoTime);
            return false;
        }
    }

    /// <summary>
    /// The best time's icon and display: set after each best time, and again when the HUD shows (after the pause menu or
    /// a resting spot), both by the game's Options. In custom rides: the personal best icon, and no display without a time.
    /// </summary>
    [HarmonyPatch]
    private static class BestTimeDisplay
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PlayScreen), nameof(PlayScreen.Method_Private_Void_5));
            yield return AccessTools.Method(typeof(PlayScreen), nameof(PlayScreen.Method_Protected_Virtual_Void_1));
        }

        private static void Postfix(PlayScreen __instance)
        {
            if (!RideGuard.ModifiedRide)
            {
                return;
            }

            PersonalIcon(__instance, __instance.m_bestTimeIcon);
            var display = __instance.m_bestTimeDisplay;
            if (display != null && display.activeSelf != _hasBest)
            {
                display.SetActive(_hasBest);
            }
        }
    }

    /// <summary>The difference pop-up's icon, set by the game's Options: the personal best icon in custom rides.</summary>
    [HarmonyPatch(typeof(PlayScreen), nameof(PlayScreen.Method_Private_Void_PDM_21))]
    private static class DeltaIcon
    {
        private static void Postfix(PlayScreen __instance)
        {
            if (RideGuard.ModifiedRide)
            {
                PersonalIcon(__instance, __instance.m_deltaTimeDisplayImage);
            }
        }
    }

    /// <summary>The game's own trigger of the difference pop-up (an event after its leaderboard checks): not in custom rides.</summary>
    [HarmonyPatch(typeof(PlayScreen), nameof(PlayScreen.Method_Private_Void_PDM_20))]
    private static class GameDeltaTrigger
    {
        private static bool Prefix() => !RideGuard.ModifiedRide;
    }

    /// <summary>The difference the pop-up shows: in custom rides the passed section's to its own best (none: no pop-up).</summary>
    [HarmonyPatch(typeof(ObjectPublicBoGaObInSiStObDi2LiUnique), nameof(ObjectPublicBoGaObInSiStObDi2LiUnique.Method_Public_Single_EnumPublicSealedva3v2_PDM_0))]
    private static class Difference
    {
        private static bool Prefix(ObjectPublicBoGaObInSiStObDi2LiUnique __instance, ref float __result)
        {
            if (!RideGuard.ModifiedRide)
            {
                return true;
            }

            // The tracker counts the sections passed: the last one is that count minus one.
            var section = __instance.field_Private_Int32_0 - 1;
            __result = section >= 0 && section < Deltas.Count && Deltas[section] is { } d ? d : NoTime;
            return false;
        }
    }

    /// <summary>
    /// The total clock (top right) is the sections passed, read from the run tracker when the HUD shows or a section
    /// starts, plus the current section's time. The game starts the tracker afresh at a ride's first input, after the HUD
    /// already read the last attempt's (a restart, the next ride of a run); its panel would only show again at the next
    /// checkpoint, but custom rides keep it on screen: read it again.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppMegagon.Downhill.HighscoreManager), nameof(Il2CppMegagon.Downhill.HighscoreManager.Method_Public_Static_Void_GameMode_EnumPublicSealedva5v2_PDM_0))]
    private static class TrackerStart
    {
        private static void Postfix()
        {
            if (RideGuard.ModifiedRide && Screen != null)
            {
                Screen.Method_Private_Void_PDM_24();
            }
        }
    }

    /// <summary>
    /// The modifier icons next to the total clock: the game fills them from its active modifiers whenever the HUD shows
    /// (also leaving a resting spot or the pause menu). In custom rides without the Super Bike's own "Modified Bike":
    /// the HUD names the bike already, and the mod shows the Super Bike as a bike everywhere.
    /// </summary>
    [HarmonyPatch]
    private static class ModifierIcons
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ObjectPublicAbstractSealedVoMoLi1ImGaVoLi1ImUnique), nameof(ObjectPublicAbstractSealedVoMoLi1ImGaVoLi1ImUnique.Method_Public_Static_Void_GameObject_List_1_Image_0));
            yield return AccessTools.Method(typeof(ObjectPublicAbstractSealedVoMoLi1ImGaVoLi1ImUnique), nameof(ObjectPublicAbstractSealedVoMoLi1ImGaVoLi1ImUnique.Method_Public_Static_Void_MonoBehaviour_List_1_Image_0));
            yield return AccessTools.Method(typeof(ObjectPublicAbstractSealedVoMoLi1ImGaVoLi1ImUnique), nameof(ObjectPublicAbstractSealedVoMoLi1ImGaVoLi1ImUnique.Method_Public_Static_Void_List_1_Image_0));
        }

        // The separator (a GameObject or a component's) comes first when there is one, the icons last.
        private static void Postfix(object[] __args)
        {
            if (!RideGuard.ModifiedRide || __args[^1] is not Il2CppSystem.Collections.Generic.List<UnityEngine.UI.Image> images)
            {
                return;
            }

            var shown = GameState.ActiveModifiers.Where(m => m.ID != Loadout.SuperBikeId).ToList();
            for (var i = 0; i < images.Count; i++)
            {
                var image = images[i];
                if (image == null)
                {
                    continue;
                }

                image.gameObject.SetActive(i < shown.Count);
                if (i < shown.Count)
                {
                    image.sprite = shown[i].m_modifierIcon;
                }
            }

            var separator = __args.Length > 1 ? __args[0] switch
            {
                GameObject o => o,
                MonoBehaviour b => b.gameObject,
                _ => null,
            } : null;
            separator?.SetActive(shown.Count > 0);
        }
    }

    /// <summary>The clock, every frame: past the finish line of a custom ride, the last section's time.</summary>
    [HarmonyPatch(typeof(PlayScreen), nameof(PlayScreen.Method_Private_Void_4))]
    private static class Clock
    {
        private static bool Prefix(PlayScreen __instance)
        {
            if (!RideGuard.ModifiedRide)
            {
                return true;
            }

            var tracker = Il2CppMegagon.Downhill.HighscoreManager.prop_ObjectPublicBoGaObInSiStObDi2LiUnique_0;
            if (tracker == null || !tracker.prop_Boolean_0)
            {
                return true;
            }

            __instance.Method_Private_Void_Single_2(tracker.field_Private_Single_0);
            return false;
        }
    }
}
