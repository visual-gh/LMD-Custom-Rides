using Il2CppMegagon.Downhill.UI.Screens;
using LMDCustomRides.Game;
using LMDCustomRides.Ui.Kit;
using UnityEngine;

namespace LMDCustomRides.Ui.Pages;

/// <summary>
/// On the game's Daily Rides screen, Ctrl+C copies today's ride code, to ride it again later as a custom ride; the
/// bottom bar says so when it happens.
/// </summary>
internal static class DailyHint
{
    private static DailyChallengesScreen _screen;
    private static GameMenuSubMenu _menu;
    private static HintBar _hints;

    public static void Update()
    {
        if (CustomTab.GameMenu == null || !GameAssets.Ready)
        {
            return;
        }

        if (_hints == null)
        {
            _screen = CustomTab.GameMenu.GetComponentInChildren<DailyChallengesScreen>(true);
            if (_screen == null)
            {
                return;
            }

            // No hint (clipboard keys are a power-user trick): the bar only says when the code was copied.
            _hints = new HintBar(_screen.transform);
        }

        // The screen first: the code is read (and parsed) only while the Daily Rides screen shows, not every frame of a ride.
        _menu ??= _screen.GetComponentInParent<GameMenuSubMenu>();
        var code = _screen.gameObject.activeInHierarchy && CustomTab.GameMenu.m_currentSubMenu?.Pointer == _menu?.Pointer &&
                   _menu?.m_currentScreen?.Pointer == _screen.Pointer
            ? GameState.DailyRideCode
            : null;
        var shown = code != null;
        _hints.Update(shown);
        if (shown && GameInput.Copy)
        {
            GUIUtility.systemCopyBuffer = code;
            _hints.Say(Texts.Get("LMDMOD_DAILYCOPIED"));
        }
    }
}
