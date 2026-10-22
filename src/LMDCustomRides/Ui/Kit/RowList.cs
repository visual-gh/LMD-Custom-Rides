using Il2CppInterop.Runtime;
using Il2CppMegagon.Downhill.UI.Screens;
using UnityEngine;
using UnityEngine.UI;

namespace LMDCustomRides.Ui.Kit;

/// <summary>
/// A vertical list of <see cref="Row"/>s, navigated like the game's Options screen: up and down move the selection
/// (skipping inactive rows, wrapping around the ends), left and right go to the selected row, the mouse selects the row
/// it moves onto. A list without input (information panels) just shows its rows.
/// </summary>
internal sealed class RowList
{
    private readonly List<Row> _rows = new();
    private bool _active;
    private int _selected = -1;
    private Vector3 _lastMouse;

    public GameObject Root { get; }
    public float Width { get; }

    /// <summary>Raised after the player changes a value through a row.</summary>
    public event Action Changed;

    public RowList(Transform parent, Vector2 position, float width)
    {
        Width = width;
        Root = new GameObject("LMDCustomRides_RowList", Il2CppType.Of<RectTransform>());
        var rect = Root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        UiTools.Place(rect, new Vector2(0, 1), new Vector2(0, 1), position, new Vector2(width, 900));
        var layout = Root.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 0;
    }

    // ---------------------------------------------------------------- building

    private T Add<T>(T row) where T : Row
    {
        _rows.Add(row);
        return row;
    }

    public ChoiceRow Choice(string labelKey, Func<string[]> keys, Func<int> get, Action<int> set) => Add(new ChoiceRow(this, labelKey, keys, get, set));

    public Row Checkbox(string labelKey, Func<bool> get, Action<bool> set) => Add(new CheckboxRow(this, labelKey, get, set));

    public Row Heading(string labelKey) => Add(new HeadingRow(this, labelKey));

    public Row Text(Func<string> line, Func<Color> color = null, float height = Tokens.LineHeight, float scale = Tokens.LineScale) =>
        Add(new TextRow(this, line, color, height, scale));


    public Row Item(Func<string> label, Func<string> value, Action pick) => Add(new ItemRow(this, label, value, pick));


    /// <summary>Removes all rows (lists filled anew each time they show, as in pop-ups).</summary>
    public void Clear()
    {
        foreach (var row in _rows)
        {
            UnityEngine.Object.Destroy(row.Root);
        }

        _rows.Clear();
        _selected = -1;
        _active = false;
    }

    public int Count => _rows.Count;

    // ---------------------------------------------------------------- a window over a longer list

    private ScrollWindow _window;
    private GameObject _arrowUp;
    private GameObject _arrowDown;

    /// <summary>
    /// Shows at most this many rows (for pick lists, whose rows do not fold): a longer list scrolls with the
    /// selection, a chevron row standing in at the top or bottom for rows out of view, as in <see cref="Table"/>.
    /// A click on a chevron, or the mouse wheel, moves the selection that way.
    /// </summary>
    public RowList Scrolling(int slots)
    {
        _window = new ScrollWindow(slots);
        _arrowUp = Chevron(true);
        _arrowDown = Chevron(false);
        return this;
    }

    /// <summary>The rows on screen (chevron rows included): what the list's height is made of.</summary>
    public int Slots => _window == null ? _rows.Count : Math.Min(_rows.Count, _window.Slots);

    /// <summary>A row holding the leaderboard filters' chevron (turned 270 degrees for down), centred.</summary>
    private GameObject Chevron(bool up)
    {
        var row = new GameObject(up ? "ChevronUp" : "ChevronDown", Il2CppType.Of<RectTransform>());
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.SetParent(Root.transform, false);
        // The list keeps each row's own height.
        rowRect.sizeDelta = new Vector2(Width, Tokens.RowHeight);

        var icon = new GameObject("Arrow", Il2CppType.Of<RectTransform>());
        var rect = icon.GetComponent<RectTransform>();
        rect.SetParent(row.transform, false);
        UiTools.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Tokens.TableArrowSize, Tokens.TableArrowSize));
        rect.localScale = Vector3.one * Tokens.TableArrowScale;
        rect.localEulerAngles = new Vector3(0, 0, up ? 90f : 270f);
        var image = icon.AddComponent<Image>();
        image.sprite = GameAssets.Sprite(Tokens.ArrowSprite);
        image.preserveAspect = true;
        image.raycastTarget = false;
        row.SetActive(false);
        return row;
    }

    /// <summary>Shows the rows in view and the chevrons for the rest.</summary>
    private void UpdateWindow()
    {
        if (_window == null)
        {
            return;
        }

        _window.Follow(Math.Max(0, _selected), _rows.Count);
        for (var i = 0; i < _rows.Count; i++)
        {
            var shown = _window.Shows(i);
            if (_rows[i].Root.activeSelf != shown)
            {
                _rows[i].Root.SetActive(shown);
            }
        }

        _arrowUp.transform.SetAsFirstSibling();
        _arrowDown.transform.SetAsLastSibling();
        if (_arrowUp.activeSelf != _window.ArrowAbove)
        {
            _arrowUp.SetActive(_window.ArrowAbove);
        }

        if (_arrowDown.activeSelf != _window.ArrowBelow)
        {
            _arrowDown.SetActive(_window.ArrowBelow);
        }
    }

    /// <summary>A click on a chevron row or the mouse wheel over the list: the way to move the selection (0: none).</summary>
    private int ScrollInput()
    {
        if (_window == null || !_window.Scrolls)
        {
            return 0;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (UiTools.MouseOver(_arrowUp.GetComponent<RectTransform>()))
            {
                return -1;
            }

            if (UiTools.MouseOver(_arrowDown.GetComponent<RectTransform>()))
            {
                return 1;
            }
        }

        var wheel = Input.mouseScrollDelta.y;
        return wheel != 0f && UiTools.MouseOver(Root.GetComponent<RectTransform>()) ? -Math.Sign(wheel) : 0;
    }

    /// <summary>The rows' screen elements, so the page shows and hides them with its own animations.</summary>
    public IEnumerable<ScreenElement> Elements => _rows.Select(r => r.Element).Where(e => e != null);

    internal void NotifyChanged() => Changed?.Invoke();

    // ---------------------------------------------------------------- input

    public Row Selected => _active && _selected >= 0 ? _rows[_selected] : null;

    public bool IsSelected(Row row) => Selected == row;

    /// <summary>Takes or gives back the input (the selected row shows as selected only while the list has it).</summary>
    public void SetActive(bool active)
    {
        if (active == _active)
        {
            return;
        }

        _active = active;

        // All rows unselected (white; a row never selected looks disabled), then the current one selected.
        foreach (var row in _rows)
        {
            row.Deselect();
        }

        if (active)
        {
            if (_selected < 0 || !_rows[_selected].Selectable)
            {
                _selected = Next(-1, 1);
            }

            if (_selected >= 0)
            {
                _rows[_selected].Select();
            }
        }
    }

    /// <summary>The next selectable row from a row in a direction, wrapping around the ends; the row itself if none.</summary>
    private int Next(int from, int step)
    {
        var count = _rows.Count;
        for (var n = 1; n <= count; n++)
        {
            var i = ((from + step * n) % count + count) % count;
            if (_rows[i].Selectable)
            {
                return i;
            }
        }

        return from;
    }

    public void Update()
    {
        foreach (var row in _rows)
        {
            row.Animate();
            row.Tick();
        }

        // A list without rows leaves up and down to the page (their key repeat is counted once per frame).
        if (!_active || _rows.Count == 0)
        {
            return;
        }

        var target = _selected;
        if (GameInput.Step(GameAction.Up))
        {
            target = Next(_selected, -1);
        }
        else if (GameInput.Step(GameAction.Down))
        {
            target = Next(_selected, 1);
        }
        else if (ScrollInput() is var scroll and not 0)
        {
            target = Next(_selected, scroll);
        }

        var mouse = Input.mousePosition;
        if ((mouse - _lastMouse).sqrMagnitude > 1f)
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Selectable && UiTools.MouseOver(_rows[i].Rect))
                {
                    target = i;
                }
            }
        }

        _lastMouse = mouse;

        // The selected row may have just become inactive: the nearest active one above, else below.
        if (target >= 0 && !_rows[target].Selectable)
        {
            var above = Next(target, -1);
            target = above != target ? above : Next(target, 1);
        }

        Select(target);
        UpdateWindow();

        if (Input.GetMouseButtonDown(0) && !GameInput.Blocked)
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                var click = _rows[i].ClickTarget;
                if (click != null && _rows[i].Selectable && UiTools.MouseOver(click))
                {
                    Select(i);
                    _rows[i].Activate();
                }
            }
        }
    }

    private void Select(int index)
    {
        if (index == _selected || index < 0)
        {
            return;
        }

        if (_selected >= 0)
        {
            _rows[_selected].Deselect();
        }

        _selected = index;
        _rows[_selected].Select();
    }

    public void Select(Row row)
    {
        var index = _rows.IndexOf(row);
        if (index >= 0 && row.Selectable)
        {
            Select(index);
        }
    }

    /// <summary>Enter on the selected row: true if the row did something with it (a checkbox flipped).</summary>
    public bool Activate() => Selected is { Selectable: true } row && row.Activate();

    /// <summary>Per frame, after the game's own input handling.</summary>
    public void LateUpdate()
    {
        UpdateWindow();
        foreach (var row in _rows)
        {
            row.Focused = _active || (row.IsLit?.Invoke() ?? false);
            row.Paint(IsSelected(row) && row.Selectable);
        }
    }

    /// <summary>Shows the current values again (after something else changed them).</summary>
    public void Refresh()
    {
        foreach (var row in _rows)
        {
            row.Setup();
        }
    }
}
