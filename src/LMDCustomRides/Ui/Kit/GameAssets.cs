using Il2CppMegagon.Downhill.UI.Screens;
using Il2CppMegagon.Downhill.UI.Screens.Settings;
using Il2CppTMPro;
using UnityEngine;

namespace LMDCustomRides.Ui.Kit;

/// <summary>
/// The game's own UI pieces the components are made from: the Options rows, the result screen's middle controls,
/// the debug seed field's text input, the game's font and sprites. All are loaded with the menu; until then
/// <see cref="Ready"/> is false.
/// </summary>
internal static class GameAssets
{
    private static SettingsScreen _options;
    private static GameObject _middleControls;
    private static TMP_InputField _inputField;
    private static readonly Dictionary<string, Sprite> Sprites = new();

    public static bool Ready => Options != null && MiddleControls != null;

    /// <summary>
    /// The Options screen with the row prefabs. It is made when first opened; its prefab, loaded with the menu, has
    /// the same rows.
    /// </summary>
    public static SettingsScreen Options
    {
        get
        {
            if (_options == null)
            {
                _options = Resources.FindObjectsOfTypeAll<SettingsScreen>().FirstOrDefault(s => s.m_settingsEntryToggle != null && s.m_settingsEntryEnum != null);
            }

            return _options;
        }
    }

    public static GameObject ChoiceRow => Options.m_settingsEntryEnum;

    public static GameObject ToggleRow => Options.m_settingsEntryToggle;

    /// <summary>The game's menu font, as the Options rows use it.</summary>
    public static TextMeshProUGUI FontSource => ChoiceRow.GetComponentInChildren<TextMeshProUGUI>(true);

    /// <summary>
    /// The result screen's middle controls: a row of key hints between the back and action buttons, with the soft
    /// background fading to both sides.
    /// </summary>
    public static GameObject MiddleControls
    {
        get
        {
            if (_middleControls == null)
            {
                var screen = Resources.FindObjectsOfTypeAll<ResultScreen>().FirstOrDefault(s => s.gameObject.scene.IsValid());
                _middleControls = screen == null ? null : screen.transform.Find("ResultsScreen_Group/GameControls/GameControls_Middle_Context")?.gameObject;
            }

            return _middleControls;
        }
    }

    private static GameObject _leaderboard;

    /// <summary>The Daily Rides leaderboard: a header strip with orange captions over stacked entries.</summary>
    public static GameObject Leaderboard
    {
        get
        {
            if (_leaderboard == null)
            {
                var board = Resources.FindObjectsOfTypeAll<DailyChallengesLeaderboard>()
                    .FirstOrDefault(b => b.gameObject.scene.IsValid() && b.name == "Leaderboards_RandomChallenge" && b.transform.Find("LeaderboardEntries")?.childCount > 0);
                _leaderboard = board == null ? null : board.gameObject;
            }

            return _leaderboard;
        }
    }

    private static GameObject _chevronTitle;

    /// <summary>
    /// Progression's page selector (Achievements, Statistics): a dark band with a small line over an orange headline,
    /// between two chevrons.
    /// </summary>
    public static GameObject ChevronTitle
    {
        get
        {
            if (_chevronTitle == null)
            {
                _chevronTitle = Resources.FindObjectsOfTypeAll<SubscreenNavigation>()
                    .FirstOrDefault(n => n.name == "SubScreenNavigation" && n.transform.Find("Elements/Image_NextButton") != null)?.gameObject;
            }

            return _chevronTitle;
        }
    }

    private static GameObject _metric;

    /// <summary>A Statistics page figure: a caption on a dark strip over a large orange value ("Total Time", "21d 13h").</summary>
    public static GameObject Metric
    {
        get
        {
            if (_metric == null)
            {
                _metric = Resources.FindObjectsOfTypeAll<StatisticsDisplay_Small>()
                    .FirstOrDefault(m => m.name == "StatisticsScreen_SingleSmallMetric_TotalPlaytime")?.gameObject;
            }

            return _metric;
        }
    }

    private static GameObject _popUp;

    /// <summary>
    /// The game's pop-up: Daily Rides' "time is running out" warning (dark background, framed box, headline, text,
    /// accept and cancel labels with their keys).
    /// </summary>
    public static GameObject PopUp
    {
        get
        {
            if (_popUp == null)
            {
                var menu = Resources.FindObjectsOfTypeAll<DailyChallengesMenu>().FirstOrDefault(m => m.gameObject.scene.IsValid());
                _popUp = menu == null ? null : menu.transform.Find("TimeWarning")?.gameObject;
            }

            return _popUp;
        }
    }

    /// <summary>A text input field in the game's style (the Daily Rides menu's hidden seed field).</summary>
    public static TMP_InputField InputField
    {
        get
        {
            if (_inputField == null)
            {
                _inputField = Resources.FindObjectsOfTypeAll<TMP_InputField>().FirstOrDefault(f => f.gameObject.scene.IsValid() && f.name == "InputField (TMP)");
            }

            return _inputField;
        }
    }

    /// <summary>One of the game's loaded sprites by name (cached), or null.</summary>
    public static Sprite Sprite(string name)
    {
        if (Sprites.TryGetValue(name, out var cached) && cached != null)
        {
            return cached;
        }

        var sprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == name);
        Sprites[name] = sprite;
        return sprite;
    }
}
