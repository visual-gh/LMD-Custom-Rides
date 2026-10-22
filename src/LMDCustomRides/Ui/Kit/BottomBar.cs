using Il2CppMegagon.Downhill.UI;
using Il2CppMegagon.Downhill.UI.Screens;
using LMDCustomRides.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LMDCustomRides.Ui.Kit;

/// <summary>
/// The game menu's bottom bar: the back button on the left (whose fade the mod's hints follow), the action button on
/// the right (its label set through the button's own setter, so the key shows as the game writes it for the player's
/// device: loca texts "{0} Start Ride").
/// </summary>
internal static class BottomBar
{
    private static CanvasGroup _back;
    private static string _actionKey;

    private static GameMenu Menu => CustomTab.GameMenu;

    /// <summary>The back button's current fade (0 to 1), which the game animates with every screen change.</summary>
    public static float Alpha
    {
        get
        {
            if (_back == null && Menu != null)
            {
                _back = Menu.transform.Find("GameMenuScreen_Group/GameMenuElements/BackButton")?.GetComponent<CanvasGroup>();
            }

            return _back == null ? 1f : _back.alpha;
        }
    }

    /// <summary>The back button's label: the measure for every text on the bottom bar.</summary>
    public static RectTransform BackLabel =>
        Menu == null ? null : Menu.transform.Find("GameMenuScreen_Group/GameMenuElements/BackButton/TextMeshPro Text_Back")?.GetComponent<RectTransform>();

    public static void SetAction(string locaKey)
    {
        var button = Menu == null ? null : Menu.m_actionButton;
        if (button == null || locaKey == null || _actionKey == locaKey)
        {
            return;
        }

        _actionKey = locaKey;
        button.Method_Public_Void_String_0(locaKey);
    }

    /// <summary>After a screen change the game set its own labels: the next page sets its own again.</summary>
    public static void ForgetAction()
    {
        _actionKey = null;
        _backKey = null;
    }

    private static string _backKey;

    /// <summary>The back button's label, through its own setter like the action's (loca text with "{0}" for the key).</summary>
    public static void SetBack(string locaKey)
    {
        var button = Menu == null ? null : Menu.m_backButton;
        if (button == null || string.IsNullOrEmpty(locaKey) || _backKey == locaKey)
        {
            return;
        }

        _backKey = locaKey;
        button.Method_Public_Void_String_0(locaKey);
    }
}

/// <summary>Helpers for the mod's elements living in the game's screens.</summary>
internal static class Screens
{
    /// <summary>Lets a screen show and hide the element with its own animations.</summary>
    public static void Register(GameMenuScreenBase screen, ScreenElement element)
    {
        var elements = screen?.m_screenElements;
        if (elements == null || element == null)
        {
            return;
        }

        for (var i = 0; i < elements.Count; i++)
        {
            if (elements[i] != null && elements[i].Pointer == element.Pointer)
            {
                return;
            }
        }

        elements.Add(element);
    }

    /// <summary>Takes away what a copied button would do in its original place (it calls its original screen).</summary>
    public static void Disarm(GameObject copy)
    {
        foreach (var button in copy.GetComponentsInChildren<Button>(true))
        {
            button.onClick = new Button.ButtonClickedEvent();

            // Only clicked or by their key: never picked up by the menu's up/down selection (Enter would press it).
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
        }

        foreach (var input in copy.GetComponentsInChildren<InteractiveUIElement>(true))
        {
            var entry = input.m_delegatesOnActivate;
            if (entry != null)
            {
                entry.callback = new UnityEngine.EventSystems.EventTrigger.TriggerEvent();
            }
        }
    }
}
