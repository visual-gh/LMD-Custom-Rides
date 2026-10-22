using HarmonyLib;
using Il2CppMegagon.Downhill.UI.Screens;
using LMDCustomRides.Game;
using LMDCustomRides.Ui.Kit;
using UnityEngine;

namespace LMDCustomRides.Ui.Pages;

/// <summary>
/// A page of the Custom Rides tab, laid out the same way: a <see cref="RowList"/> on the left (the page's input) or a
/// title with lines of figures under it (<see cref="Stats"/>), anything the page adds on the right (a map, a
/// <see cref="Table"/>), the <see cref="HintBar"/> in the middle of the bottom bar, the back label and the action
/// button ("[Enter] ...") on the right. Pages
/// build their content once the game menu has the page, before it first shows, so the page's own show animation
/// includes it.
/// </summary>
internal abstract class Page
{
    private float _retryAt;
    private bool _wasShown;
    private bool _refresh;
    private float _activatedAt;

    protected GameMenuScreenBase Screen { get; private set; }
    protected RowList List { get; private set; }

    /// <summary>Lines of figures on the left, under a title and its <see cref="StatBlock"/> (a playlist's runs).</summary>
    protected RowList Stats { get; private set; }
    protected HintBar Hints { get; private set; }

    /// <summary>The page's pop-up (pick lists, confirmations); the page's own input is off while it is open.</summary>
    protected Popup Popup { get; private set; }

    /// <summary>The page's index in the tab (CustomTab.NewRidePage...).</summary>
    protected abstract int Index { get; }

    /// <summary>The action button's label now: a loca key with "{0}" for the key.</summary>
    protected abstract string ActionKey { get; }

    protected abstract void Build();

    /// <summary>Enter, by key, gamepad or a click on the action button.</summary>
    protected abstract void OnEnter();

    /// <summary>The back button's label now (null: the page's own, "Back to Mountains").</summary>
    protected virtual string BackKey => null;

    /// <summary>Back (Escape): true if the page used it (leaving the table for the title), so the menu stays.</summary>
    protected virtual bool OnBack() => false;

    protected virtual void OnShow()
    {
    }

    protected virtual void OnHide()
    {
    }

    /// <summary>Per frame while the page has the input: its own keys.</summary>
    protected virtual void HandleInput()
    {
    }

    /// <summary>Per frame while the page shows: fills its tables and the like.</summary>
    protected virtual void Draw()
    {
    }

    protected bool Interactive => Screen != null && CustomTab.Showing(Index) && !Fade.Visible && !GameInput.Blocked && !Popup.IsOpen && !Popup.HoldsInput;

    /// <summary>The rows are set up again next frame (their values or value lists changed).</summary>
    protected void RefreshRows() => _refresh = true;

    internal void Say(string message) => Hints.Say(message);

    /// <summary>
    /// Changes a playlist's rides (not their order): when it has a personal best, after a confirmation, since the best
    /// is for the rides as they are (it comes back if they do).
    /// </summary>
    protected void ChangeRides(Playlist playlist, Action change)
    {
        var best = Records.Of(playlist)?.Pb;
        if (best == null)
        {
            change();
            return;
        }

        Popup.List.Clear();
        Popup.Open(Texts.Get("LMDMOD_CHANGETITLE"), string.Format(Texts.Get("LMDMOD_CHANGEMESSAGE"), playlist.Name, Times.Format(best.GameMs)),
            "LMDMOD_ACTION_CHANGE", change);
    }

    private readonly List<RowList> _lists = new();

    /// <summary>Another row list on the page (built in <see cref="Build"/>): shown and painted like the page's own, without input.</summary>
    protected RowList AddRows(Vector2 position, float width)
    {
        var list = new RowList(Screen.transform, position, width);
        _lists.Add(list);
        return list;
    }

    public void Update()
    {
        var screen = CustomTab.Page(Index);
        if (screen == null)
        {
            return;
        }

        if (Screen == null || Screen.Pointer != screen.Pointer)
        {
            if (Time.unscaledTime < _retryAt || !GameAssets.Ready)
            {
                return;
            }

            try
            {
                Screen = screen;
                List = new RowList(screen.transform, Tokens.ListPosition, Tokens.ListWidth);
                Stats = new RowList(screen.transform, Tokens.StatsPosition, Tokens.StatsWidth);
                Hints = new HintBar(screen.transform);
                Popup = new Popup(screen.transform.parent);
                _lists.Clear();
                _lists.Add(List);
                _lists.Add(Stats);
                Build();
                foreach (var element in _lists.SelectMany(l => l.Elements))
                {
                    Screens.Register(screen, element);
                }

                Mod.Log.Msg($"{GetType().Name} built");
            }
            catch (Exception e)
            {
                Mod.Log.Error($"{GetType().Name} could not be built: {e}");
                _retryAt = Time.unscaledTime + 5f;
                Screen = null;
                return;
            }
        }

        Popup.Tick();
        if (!screen.gameObject.activeInHierarchy)
        {
            List.SetActive(false);
            if (_wasShown)
            {
                _wasShown = false;
                Popup.Reset();
                OnHide();
            }

            Hints.Update(false);
            return;
        }

        if (!_wasShown || _refresh)
        {
            if (!_wasShown)
            {
                // A fallback: no pop-up is left over from before.
                Popup.Reset();
                OnShow();
            }

            _wasShown = true;
            _refresh = false;
            List.Refresh();
        }

        // A pop-up has the input while it is open; the page goes on drawing behind it.
        if (Popup.IsOpen)
        {
            if (!CustomTab.Showing(Index))
            {
                Popup.Close();
            }

            List.SetActive(false);
            List.Update();
            Popup.Update();
            Draw();
            Hints.Update(false);
            return;
        }

        var interactive = Interactive;
        List.SetActive(interactive);
        List.Update();
        if (interactive)
        {
            HandleInput();
            if (GameInput.Pressed(GameAction.Submit))
            {
                Activate();
            }

            BottomBar.SetAction(ActionKey);
            BottomBar.SetBack(BackKey ?? (screen.TryCast<GameMenuSubScreen>()?.m_backLocaID));
        }

        Draw();
        Hints.Update(CustomTab.Showing(Index));
    }

    public void LateUpdate()
    {
        foreach (var list in _lists)
        {
            list.LateUpdate();
        }

        Popup?.LateUpdate();
    }

    public void Activate()
    {
        if (!Interactive || Time.unscaledTime - _activatedAt < 0.2f)
        {
            return;
        }

        _activatedAt = Time.unscaledTime;
        OnEnter();
    }

    // ---------------------------------------------------------------- the tab's pages

    public static readonly NewRidePage NewRideTab = new();
    public static readonly PlaylistsPage PlaylistsTab = new();

    private static readonly Page[] All = { NewRideTab, PlaylistsTab };
    private static int _lastPage = -1;

    public static void UpdateAll()
    {
        GameInput.Back ??= () =>
        {
            var current = CustomTab.CurrentPage;
            return current >= 0 && current < All.Length && All[current].Interactive && All[current].OnBack();
        };

        // A page change (or leaving the tab): the game set its own action label, the page sets its own again.
        var page = CustomTab.CurrentPage;
        if (page != _lastPage)
        {
            _lastPage = page;
            BottomBar.ForgetAction();
        }

        foreach (var p in All)
        {
            p.Update();
        }
    }

    public static void LateUpdateAll()
    {
        foreach (var p in All)
        {
            p.LateUpdate();
        }
    }

    /// <summary>The game menu's action button in the Custom Rides tab is the current page's Enter.</summary>
    [HarmonyPatch(typeof(GameMenu), nameof(GameMenu.OnActionButton))]
    private static class ActionButton
    {
        private static bool Prefix()
        {
            var page = CustomTab.CurrentPage;
            if (page < 0 || page >= All.Length)
            {
                return true;
            }

            All[page].Activate();
            return false;
        }
    }
}
