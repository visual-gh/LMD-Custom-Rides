using Il2CppInterop.Runtime;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LMDCustomRides.Ui.Kit;

/// <summary>How a table row stands out: not, as the chosen entry (green), or as one being moved (orange).</summary>
internal enum TableStyle
{
    Normal,
    Selected,
    Held,
}

/// <summary>
/// A column on the left side of a table: its caption (loca key; null: none), its width (for its widest content), how
/// many icon slots go before its text (from the left edge) and how many follow the text (spaced as the icons are, from
/// where the text ends). The last columns may float: together they then sit centred in the room between the fixed
/// columns and the time's figures, so the table reads in three parts with even gaps whether or not the difference
/// shows.
/// </summary>
internal sealed record TableColumn(string Header, float Width, int Icons = 0, bool Floats = false, int After = 0);

/// <summary>An icon in a cell.</summary>
internal readonly record struct TableIcon(Sprite Sprite);

/// <summary>One cell of a row: its text and its icons, slot by slot (the ones before the text, then the ones after; null: none).</summary>
internal readonly record struct TableCell(string Text, IReadOnlyList<TableIcon> Icons = null);

/// <summary>
/// A table made of the game's own leaderboard (a copy of the Daily Rides leaderboard): its header strip with orange
/// captions, its entries stacked edge to edge with their alternating gradient backgrounds and shadowed type, its 64 px
/// margins. Columns run from the left; the time, and after a run its difference, sit at the right. The table shows a
/// window of a longer list that follows the selected entry (the mouse wheel scrolls it); when entries are out of view
/// the first row (above) or the last row (below) shows the leaderboard filters' chevron instead, pointing the way.
/// The selected entry shows in the game's green while the table has the input, a held one in orange with a marker in
/// place of its number.
/// </summary>
internal sealed class Table
{
    private sealed class Row
    {
        public RectTransform Rect;
        public TextMeshProUGUI[] Texts;
        public Image[][] Icons;
        public string[] FlowedFor;
        public TextMeshProUGUI Time;
        public TextMeshProUGUI Delta;
        public Image Arrow;
    }

    private readonly List<Row> _rows = new();
    private readonly TableColumn[] _columns;
    private readonly RectTransform _root;
    private readonly RectTransform _entries;
    private readonly CanvasGroup _group;
    private readonly TextMeshProUGUI _timeCaption;
    private readonly TextMeshProUGUI _deltaCaption;
    private readonly TextMeshProUGUI[] _captions;
    private readonly float _width;
    private readonly float _fixedRight;
    private bool? _deltaShown;

    /// <summary>Rows on screen.</summary>
    public int Count => _rows.Count;

    private readonly ScrollWindow _window;

    public Table(Transform parent, Vector2 position, float width, int rows, TableColumn[] columns, string timeHeader, string deltaHeader)
    {
        _columns = columns;
        _width = width;
        _window = new ScrollWindow(rows);
        var root = UnityEngine.Object.Instantiate(GameAssets.Leaderboard, parent, false);
        root.name = "LMDCustomRides_Table";
        UiTools.StripBehaviours(root);
        root.SetActive(true);
        // Only the header and the entries: the leaderboard's paging, player count and timer go.
        for (var i = root.transform.childCount - 1; i >= 0; i--)
        {
            var child = root.transform.GetChild(i);
            if (child.name != "Categories" && child.name != "LeaderboardEntries")
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        _root = root.GetComponent<RectTransform>();
        UiTools.Place(_root, new Vector2(0, 1), new Vector2(0, 1), position,
            new Vector2(width, Tokens.TableHeaderHeight + rows * Tokens.TableRowHeight));
        _group = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();

        // The header strip: the game's captions, one per column (copies of the player caption), the time and the score.
        var header = root.transform.Find("Categories").GetComponent<RectTransform>();
        header.sizeDelta = new Vector2(header.sizeDelta.x, Tokens.TableHeaderHeight);
        var playerCaption = Caption(header, "TextMeshPro Text_Player");
        _timeCaption = Caption(header, "TextMeshPro Text_Time");
        _deltaCaption = Caption(header, "TextMeshPro Text_Score");
        UnityEngine.Object.DestroyImmediate(header.Find("TextMeshPro Text_Rank")?.gameObject);
        _captions = new TextMeshProUGUI[columns.Length];
        for (var c = 0; c < columns.Length; c++)
        {
            _captions[c] = c == 0 ? playerCaption : UnityEngine.Object.Instantiate(playerCaption.gameObject, header, false).GetComponent<TextMeshProUGUI>();
            UiTools.SetText(_captions[c], columns[c].Header == null ? "" : Game.Texts.Get(columns[c].Header));
        }

        UiTools.SetText(_timeCaption, Game.Texts.Get(timeHeader));
        UiTools.SetText(_deltaCaption, Game.Texts.Get(deltaHeader));

        // The entries: the first is the template for all of ours, the game's others go.
        _entries = root.transform.Find("LeaderboardEntries").GetComponent<RectTransform>();
        _entries.anchorMin = new Vector2(0, 1);
        _entries.anchorMax = new Vector2(1, 1);
        _entries.pivot = new Vector2(0.5f, 1);
        _entries.anchoredPosition = new Vector2(0, -Tokens.TableHeaderHeight);
        // The game places its entries itself: ours are placed here, edge to edge.
        var layout = _entries.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            UnityEngine.Object.DestroyImmediate(layout);
        }

        var template = _entries.GetChild(0).gameObject;
        for (var i = _entries.childCount - 1; i > 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(_entries.GetChild(i).gameObject);
        }

        UnityEngine.Object.DestroyImmediate(template.transform.Find("Image_LeaderboardIcon")?.gameObject);
        UnityEngine.Object.DestroyImmediate(template.transform.Find("TextMeshPro Text_Row_1")?.gameObject);
        var light = GameAssets.Sprite(Tokens.LightStrip);
        var dark = GameAssets.Sprite(Tokens.DarkStrip);
        // All rows are copies of the untouched template, made before any is filled in.
        var rowObjects = new List<GameObject> { template };
        for (var i = 1; i < rows; i++)
        {
            rowObjects.Add(UnityEngine.Object.Instantiate(template, _entries, false));
        }

        for (var i = 0; i < rows; i++)
        {
            var go = rowObjects[i];
            go.name = "Row" + i;
            var row = new Row { Rect = go.GetComponent<RectTransform>() };
            var element = go.GetComponent<LayoutElement>();
            if (element != null)
            {
                element.minHeight = Tokens.TableRowHeight;
                element.preferredHeight = Tokens.TableRowHeight;
            }

            row.Rect.anchorMin = new Vector2(0, 1);
            row.Rect.anchorMax = new Vector2(1, 1);
            row.Rect.pivot = new Vector2(0.5f, 1);
            row.Rect.sizeDelta = new Vector2(0, Tokens.TableRowHeight);
            row.Rect.anchoredPosition = new Vector2(0, -i * Tokens.TableRowHeight);
            var background = go.transform.Find("Image_Background")?.GetComponent<Image>();
            if (background != null)
            {
                // As the game's: darker first, lighter second.
                background.sprite = i % 2 == 0 ? dark : light;
            }

            // Each column: its icons, then its text (placed with the column, see PlaceColumn).
            row.Texts = new TextMeshProUGUI[columns.Length];
            row.Icons = new Image[columns.Length][];
            row.FlowedFor = new string[columns.Length];
            var textCell = go.transform.Find("TextMeshPro Text_Row_2").gameObject;
            for (var c = 0; c < columns.Length; c++)
            {
                row.Icons[c] = Enumerable.Range(0, columns[c].Icons + columns[c].After).Select(_ => Icon(go.transform, 0)).ToArray();
                var cell = c == 0 ? textCell : UnityEngine.Object.Instantiate(textCell, go.transform, false);
                row.Texts[c] = Cell(cell);
            }

            row.Time = Cell(go.transform.Find("TextMeshPro Text_Row_3").gameObject);
            row.Delta = Cell(go.transform.Find("TextMeshPro Text_Row_4").gameObject);
            row.Delta.fontSizeMax = Tokens.TableFont * Tokens.LineScale;

            // The chevron a row shows in place of an entry when entries are out of view that way.
            row.Arrow = Icon(go.transform, 0);
            row.Arrow.rectTransform.anchorMin = row.Arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            row.Arrow.rectTransform.sizeDelta = new Vector2(Tokens.TableArrowSize, Tokens.TableArrowSize);
            row.Arrow.rectTransform.localScale = Vector3.one * Tokens.TableArrowScale;
            row.Arrow.sprite = GameAssets.Sprite(Tokens.ArrowSprite);
            row.Arrow.gameObject.SetActive(false);
            _rows.Add(row);
        }

        // The fixed columns from the left margin; a floating one is placed with the time (ShowDelta).
        var x = Tokens.TableMargin;
        for (var c = 0; c < columns.Length && !columns[c].Floats; c++)
        {
            PlaceColumn(c, x);
            x += columns[c].Width;
        }

        _fixedRight = x;
        ShowDelta(false);
    }

    /// <summary>Places a column's caption, icons and texts from its left edge.</summary>
    private void PlaceColumn(int c, float x)
    {
        var column = _columns[c];
        var step = Tokens.IconSize + Tokens.TableIconGap;
        Place(_captions[c], false, x, x + column.Width, TextAlignmentOptions.MidlineLeft);
        foreach (var row in _rows)
        {
            for (var k = 0; k < column.Icons; k++)
            {
                row.Icons[c][k].rectTransform.anchoredPosition = new Vector2(x + k * step + Tokens.IconSize / 2f, 0);
            }

            Place(row.Texts[c], false, x + column.Icons * step, x + column.Width - Tokens.TableIconGap, TextAlignmentOptions.MidlineLeft);
            row.FlowedFor[c] = null;
            Flow(row, c);
        }
    }

    /// <summary>The icons after a cell's text, spaced as the icons are from where the text ends (again when it changes).</summary>
    private void Flow(Row row, int c)
    {
        var column = _columns[c];
        var text = row.Texts[c];
        if (column.After == 0 || row.FlowedFor[c] == text.text)
        {
            return;
        }

        row.FlowedFor[c] = text.text;
        var ink = 0f;
        if (!string.IsNullOrEmpty(text.text))
        {
            text.ForceMeshUpdate();
            ink = text.textBounds.size.x;
        }

        var step = Tokens.IconSize + Tokens.TableIconGap;
        var x = text.rectTransform.offsetMin.x + ink + Tokens.TableIconGap;
        for (var k = 0; k < column.After; k++)
        {
            row.Icons[c][column.Icons + k].rectTransform.anchoredPosition = new Vector2(x + k * step + Tokens.IconSize / 2f, 0);
        }
    }

    private static TextMeshProUGUI Caption(Transform header, string name)
    {
        var caption = header.Find(name).GetComponent<TextMeshProUGUI>();
        var localized = caption.GetComponent<Il2CppMegagon.Downhill.UI.TextMeshProLocalized>();
        if (localized != null)
        {
            localized.enabled = false;
        }

        caption.fontSizeMax = Tokens.TableHeaderFont;
        caption.enableWordWrapping = false;
        return caption;
    }

    /// <summary>A text cell of an entry: stretched over the row's height, the game's type with a smaller maximum.</summary>
    private static TextMeshProUGUI Cell(GameObject cell)
    {
        var text = cell.GetComponent<TextMeshProUGUI>();
        text.fontSizeMax = Tokens.TableFont;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Image Icon(Transform row, float x)
    {
        var icon = new GameObject("Icon", Il2CppType.Of<RectTransform>());
        var rect = icon.GetComponent<RectTransform>();
        rect.SetParent(row, false);
        UiTools.Place(rect, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(Tokens.IconSize, Tokens.IconSize));
        var image = icon.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    /// <summary>Places a text between two edges, counted from the left, or from the right (negative) when <paramref name="fromRight"/>.</summary>
    private static void Place(TextMeshProUGUI cell, bool fromRight, float left, float right, TextAlignmentOptions align)
    {
        var r = cell.rectTransform;
        r.anchorMin = new Vector2(fromRight ? 1 : 0, 0);
        r.anchorMax = new Vector2(fromRight ? 1 : 0, 1);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(left, 0);
        r.offsetMax = new Vector2(right, 0);
        cell.alignment = align;
    }

    /// <summary>Shows or leaves out the difference column (the time then sits at the right edge).</summary>
    public void ShowDelta(bool shown)
    {
        if (_deltaShown == shown)
        {
            return;
        }

        _deltaShown = shown;
        var deltaRight = -Tokens.TableMargin;
        var timeRight = shown ? deltaRight - Tokens.TableDeltaWidth : deltaRight;
        foreach (var (time, delta) in _rows.Select(r => (r.Time, r.Delta)).Append((_timeCaption, _deltaCaption)))
        {
            Place(time, true, timeRight - Tokens.TableTimeWidth, timeRight, TextAlignmentOptions.MidlineRight);
            Place(delta, true, deltaRight - Tokens.TableDeltaWidth, deltaRight, TextAlignmentOptions.MidlineRight);
            delta.gameObject.SetActive(shown);
        }

        // The floating columns, as one block: centred between the fixed columns' last text (which ends a gap before their
        // right edge) and the time's figures (right-aligned, TableTimeInk wide), so the gaps on both sides are even with
        // or without the difference.
        var first = Array.FindIndex(_columns, c => c.Floats);
        if (first >= 0)
        {
            var figures = _width + timeRight - Tokens.TableTimeInk;
            var left = _fixedRight - Tokens.TableIconGap;
            var x = left + (figures - left - _columns.Skip(first).Sum(c => c.Width)) / 2f;
            for (var c = first; c < _columns.Length; c++)
            {
                PlaceColumn(c, x);
                x += _columns[c].Width;
            }
        }
    }

    /// <summary>Fills a row with its cells (one per column), time and difference, or hides it (null cells).</summary>
    public void Set(int index, TableCell[] cells, string time = null, TableStyle style = TableStyle.Normal, string delta = null, Color? deltaColor = null)
    {
        var row = _rows[index];
        var shown = cells != null;
        if (row.Rect.gameObject.activeSelf != shown)
        {
            row.Rect.gameObject.SetActive(shown);
        }

        if (!shown)
        {
            return;
        }

        if (row.Arrow.gameObject.activeSelf)
        {
            row.Arrow.gameObject.SetActive(false);
        }

        var color = style switch { TableStyle.Selected => Tokens.Green, TableStyle.Held => Tokens.Orange, _ => Tokens.White };
        for (var c = 0; c < _columns.Length; c++)
        {
            var cell = c < cells.Length ? cells[c] : default;
            row.Texts[c].color = color;
            // A held entry shows a marker in place of its first cell (its number): it is on the move.
            UiTools.SetText(row.Texts[c], c == 0 && style == TableStyle.Held ? Tokens.TableHeldMarker : cell.Text ?? "");
            Flow(row, c);
            for (var k = 0; k < row.Icons[c].Length; k++)
            {
                var icon = cell.Icons != null && k < cell.Icons.Count ? cell.Icons[k] : default;
                var image = row.Icons[c][k];
                image.enabled = icon.Sprite != null;
                if (icon.Sprite != null && image.sprite != icon.Sprite)
                {
                    image.sprite = icon.Sprite;
                }

                // Icons take the row's colour (the game's icons are white).
                image.color = color;
            }
        }

        row.Time.color = color;
        UiTools.SetText(row.Time, time ?? "");
        UiTools.SetText(row.Delta, delta ?? "");
        row.Delta.color = deltaColor ?? color;
    }

    /// <summary>Sets how many rows show (the strips end with the last; an empty table is its header alone).</summary>
    public void Finish(int shown) => _entries.sizeDelta = new Vector2(_entries.sizeDelta.x, shown * Tokens.TableRowHeight);

    /// <summary>Shows the table, or hides it whole (header too) where there is nothing it could hold.</summary>
    public void SetShown(bool shown)
    {
        _group.alpha = shown ? 1f : 0f;
        _group.blocksRaycasts = shown;
    }

    /// <summary>The time column's caption ("Best Time", after a run "Run").</summary>
    public void SetTimeHeader(string key) => UiTools.SetText(_timeCaption, Game.Texts.Get(key));

    // ---------------------------------------------------------------- the window over a longer list

    /// <summary>A row's content: an entry, or one of these.</summary>
    public const int ArrowUp = ScrollWindow.ArrowUp;
    public const int ArrowDown = ScrollWindow.ArrowDown;
    public const int Empty = ScrollWindow.Empty;

    /// <summary>Scrolls just enough to have the entry in view (as the game's leaderboards do).</summary>
    public void Follow(int entry, int entries) => _window.Follow(entry, entries);

    /// <summary>What a row shows: an entry index, <see cref="ArrowUp"/>, <see cref="ArrowDown"/> or <see cref="Empty"/>.</summary>
    public int EntryAt(int row) => _window.EntryAt(row);

    /// <summary>The rows showing (entries and chevrons): where the strips end.</summary>
    public int RowsShown => _window.SlotsShown;

    /// <summary>Shows the chevron on a row in place of an entry, on the row's strip.</summary>
    public void SetArrow(int index, bool up)
    {
        var row = _rows[index];
        if (!row.Rect.gameObject.activeSelf)
        {
            row.Rect.gameObject.SetActive(true);
        }

        foreach (var text in row.Texts.Append(row.Time).Append(row.Delta))
        {
            UiTools.SetText(text, "");
        }

        foreach (var icon in row.Icons.SelectMany(i => i))
        {
            icon.enabled = false;
        }

        row.Arrow.enabled = true;
        row.Arrow.gameObject.SetActive(true);
        // The filters' chevron points down at 270 degrees.
        row.Arrow.rectTransform.localEulerAngles = new Vector3(0, 0, up ? 90f : 270f);
    }

    /// <summary>The mouse wheel over the table scrolls it: true if it moved.</summary>
    public bool Scroll()
    {
        var wheel = Input.mouseScrollDelta.y;
        return wheel != 0f && _window.Scrolls && UiTools.MouseOver(_root) && _window.Scroll(-Math.Sign(wheel));
    }

    /// <summary>The first and last entries in view.</summary>
    public (int First, int Last) InView => _window.InView;

    /// <summary>The entry (not row) clicked this frame, or -1; a click on a chevron scrolls that way.</summary>
    public int Clicked()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return -1;
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            if (!_rows[i].Rect.gameObject.activeSelf || !UiTools.MouseOver(_rows[i].Rect))
            {
                continue;
            }

            var entry = EntryAt(i);
            if (entry == ArrowUp || entry == ArrowDown)
            {
                _window.Scroll(entry == ArrowUp ? -1 : 1);
                return -1;
            }

            return entry;
        }

        return -1;
    }
}
