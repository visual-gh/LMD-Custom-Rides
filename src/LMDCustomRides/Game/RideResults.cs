using HarmonyLib;
using Il2CppMegagon.Downhill.UI.Screens;
using Il2CppTMPro;
using UnityEngine;

namespace LMDCustomRides.Game;

/// <summary>
/// The game's result screen after a custom ride. Its Rank column (leaderboard ranks, which a custom ride has none of)
/// becomes "+/-": each section's difference to its best before this ride, as the HUD showed it, and the total's to the
/// ride's best. The game's own differences next to the times (against the normal trail) are cleared, and there is no
/// Leaderboards button. During a playlist pass Continue reads "Next Ride (2/16)", or "Run Results" after the last ride.
/// </summary>
internal static class RideResults
{
    private static readonly Color Faster = new(0.612f, 0.914f, 0.314f, 1f);  // the game's difference green
    private static readonly Color Slower = new(0.830f, 0.192f, 0.192f, 1f);  // the HUD difference's red
    private static readonly Color Plain = Color.white;

    private sealed class Row
    {
        public TextMeshProUGUI Change;
        public TextMeshProUGUI Rank;
        public GameObject Improvement;
    }

    private static ResultScreen _screen;
    private static TextMeshProUGUI _header;
    private static readonly List<Row> Rows = new();
    private static Row _total;
    private static TextMeshProUGUI _continue;
    private static string _continueText;  // the game's own Continue label while it reads "Next Ride"
    private static readonly List<GameObject> Hidden = new();
    private static bool _changed;
    private static float _nextLook;
    private static string _headerText;
    private static Color _rankColor = Color.white;

    public static void LateUpdate()
    {
        if (!CustomSession.Active)
        {
            // The normal game's result screen as it was (it writes its rows itself).
            if (_changed)
            {
                _changed = false;
                foreach (var o in Hidden.Where(o => o != null))
                {
                    o.SetActive(true);
                }

                Hidden.Clear();
                RelabelContinue(false);
                Set(_header, _headerText, null);
                foreach (var row in Rows.Append(_total).Where(r => r?.Rank != null))
                {
                    row.Rank.color = _rankColor;
                }
            }

            return;
        }

        if (_screen == null)
        {
            if (Time.unscaledTime < _nextLook)
            {
                return;
            }

            _nextLook = Time.unscaledTime + 1f;
            _screen = Resources.FindObjectsOfTypeAll<ResultScreen>().FirstOrDefault(s => s.gameObject.scene.IsValid());
            if (_screen == null)
            {
                return;
            }

            Collect();
        }

        if (!_screen.gameObject.activeInHierarchy)
        {
            return;
        }

        _changed = true;
        Set(_header, Texts.Get("LMDMOD_PLUSMINUS"), null);
        for (var i = 0; i < Rows.Count; i++)
        {
            Fill(Rows[i], i < RideHud.Deltas.Count ? RideHud.Deltas[i] : null);
        }

        Fill(_total, RideHud.TotalDelta);
        HideControl(_screen.m_leaderboardButton);
        RelabelContinue(PlaylistRunner.Running);
    }

    /// <summary>
    /// During a pass Continue goes on to the next ride: "Next Ride (2/16)" (the game's key markup stays, the words change).
    /// Anywhere else it is the game's own "Continue To Menu" again.
    /// </summary>
    private static void RelabelContinue(bool pass)
    {
        if (_continue == null)
        {
            return;
        }

        if (!pass)
        {
            if (_continueText != null)
            {
                Set(_continue, _continueText, null);
                _continueText = null;
            }

            return;
        }

        var text = _continue.text ?? "";
        _continueText ??= text;
        var key = text.LastIndexOf('>');
        Set(_continue, (key >= 0 ? text.Substring(0, key + 1) + " " : "") + PlaylistRunner.ContinueText, null);
    }

    /// <summary>The screen's texts, found once by the names in the game's prefab.</summary>
    private static void Collect()
    {
        Rows.Clear();
        _total = null;
        foreach (var text in _screen.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            var name = text.name;
            var parent = text.transform.parent;
            if (name == "TextMeshPro Text_Rank" && parent.name == "Image_Categories")
            {
                _header = text;
            }
            else if (name == "TextMeshPro Text_Rank" && parent.name.StartsWith("Resultscreen_Overview_Entry", StringComparison.Ordinal))
            {
                Rows.Add(RowOf(parent));
            }
            else if (name == "TextMeshPro Text_Rank" && parent.name == "Resultscreen_Overview_Total")
            {
                _total = RowOf(parent);
            }
            else if (name == "TextMeshPro Text_Continue")
            {
                _continue = text;
            }
        }

        // The rows in the screen's order (section 1 first).
        Rows.Sort((a, b) => a.Rank.transform.parent.GetSiblingIndex().CompareTo(b.Rank.transform.parent.GetSiblingIndex()));
        _headerText = _header?.text;
        _rankColor = Rows.FirstOrDefault()?.Rank?.color ?? Color.white;
    }

    private static Row RowOf(Transform row)
    {
        var rank = row.Find("TextMeshPro Text_Rank");
        return new Row
        {
            Rank = rank?.GetComponent<TextMeshProUGUI>(),
            Change = (row.Find("TextMeshPro Text_Time/TextMeshPro Text_ChangeTime") ?? row.Find("TextMeshPro Text_ChangeTime"))?.GetComponent<TextMeshProUGUI>(),
            Improvement = rank?.Find("Image_Improvement")?.gameObject ?? row.Find("Image_Improvement")?.gameObject,
        };
    }

    private static void Fill(Row row, float? delta)
    {
        if (row == null)
        {
            return;
        }

        Set(row.Change, "", null);
        Hide(row.Improvement);
        if (delta is not { } d)
        {
            Set(row.Rank, "-", Plain);
            return;
        }

        // In hundredths, as shown: a difference that shows as 0.00 is even.
        var shown = Times.Hundredths((long)Math.Round(d * 1000f));
        Set(row.Rank, (shown < 0 ? "-" : shown > 0 ? "+" : "±") + Times.Format(Math.Abs(shown)), shown < 0 ? Faster : shown > 0 ? Slower : Plain);
    }

    private static void Set(TextMeshProUGUI text, string value, Color? color)
    {
        if (text == null)
        {
            return;
        }

        if (text.text != value)
        {
            text.text = value;
        }

        if (color is { } c && text.color != c)
        {
            text.color = c;
        }
    }

    private static void Hide(GameObject o)
    {
        if (o != null && o.activeSelf)
        {
            o.SetActive(false);
        }
    }

    /// <summary>Hides one of the game's controls, to put back for the normal game.</summary>
    private static void HideControl(GameObject o)
    {
        if (o != null && o.activeSelf)
        {
            o.SetActive(false);
            Hidden.Add(o);
        }
    }

    /// <summary>No leaderboards from a custom ride's result screen (they would be the normal trail's).</summary>
    [HarmonyPatch(typeof(ResultScreen), nameof(ResultScreen.OnGoToHighscoreScreen))]
    private static class Leaderboards
    {
        private static bool Prefix() => !CustomSession.Active;
    }

    /// <summary>Restart Run during a playlist pass: the ride again, the pass goes on after it.</summary>
    [HarmonyPatch(typeof(ResultScreen), nameof(ResultScreen.OnRestart))]
    private static class Restart
    {
        private static void Postfix() => PlaylistRunner.RideAgain();
    }

    /// <summary>Continue during a playlist pass: the run takes it on to the next ride (behind its fade).</summary>
    [HarmonyPatch(typeof(ResultScreen), nameof(ResultScreen.OnGoToGameMenu))]
    private static class Continue
    {
        private static bool Prefix() => !PlaylistRunner.Continue();
    }
}
