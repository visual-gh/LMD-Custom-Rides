using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppMegagon.Downhill.UI;
using Il2CppMegagon.Downhill.UI.Screens;
using Il2CppMegagon.Downhill.UI.Screens.Settings;
using Il2CppTMPro;
using LMDCustomRides.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LMDCustomRides.Ui.Kit;

/// <summary>
/// A row of a <see cref="RowList"/>, made of one of the game's Options rows (its look, sounds and left/right handling
/// are the game's), resized to the Daily Rides panel. A row can be inactive (dimmed and skipped) and have a leading
/// icon. Rows are made through the list (<see cref="RowList.Choice"/> and so on).
/// </summary>
internal abstract class Row
{
    private readonly List<(Graphic Graphic, float Alpha)> _graphics = new();
    private readonly float _height;
    private Func<bool> _enabled;
    private Func<bool> _shown;
    private bool _opening = true;
    private Image _background;
    private Image _icon;
    private Func<Sprite> _iconSprite;
    private Func<bool> _iconLit;

    protected readonly RowList List;
    protected readonly BaseSettingsEntry Entry;
    protected readonly TextMeshProUGUI Label;

    public GameObject Root { get; }
    public RectTransform Rect { get; }

    /// <summary>Headings and text lines: never selected, never dimmed.</summary>
    public virtual bool Passive => false;

    public bool Selectable => !Passive && !Collapsed && (_enabled == null || _enabled());

    /// <summary>Folded away (a sub-setting whose parent is off): no height, not shown, never selected.</summary>
    public bool Collapsed => _shown != null && !_shown();

    /// <summary>How far the row is open: 1 shown, 0 folded away (animated between).</summary>
    protected float Open { get; private set; } = 1f;

    public ScreenElement Element => Root.GetComponent<ScreenElement>();

    protected Row(RowList list, GameObject prefab, string labelKey, float height)
    {
        List = list;
        Root = UnityEngine.Object.Instantiate(prefab, list.Root.transform, false);
        Root.name = "Row_" + labelKey;
        Root.SetActive(true);
        Rect = Root.GetComponent<RectTransform>();
        Entry = Root.GetComponent<BaseSettingsEntry>();
        var label = Root.transform.Find("TextMeshPro Text_SettingsLabel");
        Label = label == null ? null : label.GetComponent<TextMeshProUGUI>();
        var localized = label == null ? null : label.GetComponent<TextMeshProLocalized>();
        if (localized != null)
        {
            localized.m_locaKeyString = labelKey;
            localized.Method_Public_Void_0();
        }

        if (Label != null)
        {
            Label.text = Texts.Get(labelKey);
        }

        _labelKey = labelKey;
        _height = height;
        _background = Root.GetComponent<Image>();
        SetHeight(height);
        Resize(list.Width);
    }

    private void SetHeight(float height)
    {
        Rect.sizeDelta = new Vector2(Rect.sizeDelta.x, height);
        var element = Root.GetComponent<LayoutElement>();
        if (element != null)
        {
            element.minHeight = height;
            element.preferredHeight = height;
        }
    }

    /// <summary>
    /// Per frame: opens or folds the row when its condition changes. The row keeps its size: a spacer in the list makes
    /// room for it (the rows below slide down or up) while the row fades in moving down into place, or fades out moving
    /// up, as the game's own screen elements move.
    /// </summary>
    internal void Animate()
    {
        if (_shown == null)
        {
            return;
        }

        var target = _shown() ? 1f : 0f;
        if (!Mathf.Approximately(Open, target))
        {
            _opening = target > Open;
            Open = Mathf.MoveTowards(Open, target, Time.unscaledDeltaTime / Tokens.CollapseSeconds);
            ApplyOpen();
        }

        // The game's own selection fades set the background's alpha back (when the page shows): a folded row's strip is
        // kept hidden every frame, as its texts and icons are (Paint).
        if (_background != null && Open < 1f)
        {
            _background.canvasRenderer.SetAlpha(Open);
        }

        FollowSpacer();
    }

    // Ease-out both ways: fast at first, slowing down at the end.
    private float Eased => _opening ? 1f - (1f - Open) * (1f - Open) : Open * Open;

    private void ApplyOpen()
    {
        if (_spacer != null)
        {
            _spacer.sizeDelta = new Vector2(_spacer.sizeDelta.x, _height * Eased);
            LayoutRebuilder.ForceRebuildLayoutImmediate(List.Root.GetComponent<RectTransform>());
        }

        if (_background != null)
        {
            _background.canvasRenderer.SetAlpha(Open);
        }
    }

    /// <summary>The row sits where its spacer is (its top at the spacer's top), a little higher while not fully open.</summary>
    private void FollowSpacer()
    {
        if (_spacer == null)
        {
            return;
        }

        var lift = (1f - Eased) * Tokens.CollapseSlide;
        var y = _spacer.anchoredPosition.y - _height * (1f - Rect.pivot.y) + lift;
        if (!Mathf.Approximately(Rect.anchoredPosition.y, y))
        {
            Rect.anchoredPosition = new Vector2(Rect.anchoredPosition.x, y);
        }
    }

    private RectTransform _spacer;

    /// <summary>The row leaves the list's layout; a spacer before it takes its place and its animated height.</summary>
    private void MakeSpacer()
    {
        var go = new GameObject("Spacer_" + Root.name, Il2CppType.Of<RectTransform>());
        _spacer = go.GetComponent<RectTransform>();
        _spacer.SetParent(List.Root.transform, false);
        _spacer.SetSiblingIndex(Root.transform.GetSiblingIndex());
        _spacer.anchorMin = new Vector2(0, 1);
        _spacer.anchorMax = new Vector2(0, 1);
        _spacer.pivot = new Vector2(Rect.pivot.x, 1f);
        _spacer.sizeDelta = new Vector2(List.Width, _height);

        var element = Root.GetComponent<LayoutElement>() ?? Root.AddComponent<LayoutElement>();
        element.ignoreLayout = true;
        Rect.anchorMin = new Vector2(0, 1);
        Rect.anchorMax = new Vector2(0, 1);
        Rect.sizeDelta = new Vector2(List.Width, _height);
        Rect.anchoredPosition = new Vector2(List.Width * Rect.pivot.x, Rect.anchoredPosition.y);
    }

    /// <summary>The Options row resized to the Daily Rides panel: its type size, a narrower value column.</summary>
    private void Resize(float width)
    {
        if (Label != null)
        {
            LimitFont(Label, Tokens.RowFont);
            var rect = Label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width - Tokens.LabelX - Tokens.ValueColumn, rect.sizeDelta.y);
        }

        var value = UiTools.Get<TextMeshProUGUI>(Root.transform, "TextMeshPro Text_Enum");
        if (value != null)
        {
            LimitFont(value, Tokens.RowFont);
            value.rectTransform.anchoredPosition = new Vector2(Tokens.ValueCenter, 0f);
            value.rectTransform.sizeDelta = new Vector2(Tokens.ValueWidth, value.rectTransform.sizeDelta.y);
        }

        foreach (var (name, x) in new[] { ("Image_ArrowLeft", Tokens.ArrowLeftX), ("Image_ArrowRight", Tokens.ArrowRightX) })
        {
            var arrow = UiTools.Get<RectTransform>(Root.transform, name);
            if (arrow != null)
            {
                arrow.anchoredPosition = new Vector2(x, 0f);
                arrow.localScale = Vector3.one * Tokens.ControlScale;
            }
        }

        CenterValue(value);
    }

    /// <summary>
    /// The value fills the room between the arrows as they are drawn (the right one is mirrored about its pivot), with
    /// the same padding on both sides, so a value shrinking to fit stays centred.
    /// </summary>
    private void CenterValue(TextMeshProUGUI value)
    {
        var left = UiTools.Get<RectTransform>(Root.transform, "Image_ArrowLeft");
        var right = UiTools.Get<RectTransform>(Root.transform, "Image_ArrowRight");
        if (value == null || left == null || right == null)
        {
            return;
        }

        var inner = EdgeX(left, true);
        var outer = EdgeX(right, false);
        var width = outer - inner - 2f * Tokens.ValuePadding;
        if (width <= 0f)
        {
            return;
        }

        // The value is anchored to the row's right edge: its x is measured from there.
        var center = (inner + outer) / 2f - Rect.rect.xMax;
        value.rectTransform.anchoredPosition = new Vector2(center, 0f);
        value.rectTransform.sizeDelta = new Vector2(width, value.rectTransform.sizeDelta.y);
    }

    /// <summary>An arrow's drawn edge in the row's space: the right edge of the left arrow, the left edge of the right one.</summary>
    private float EdgeX(RectTransform arrow, bool rightEdge)
    {
        var corners = new Vector3[4];
        arrow.GetWorldCorners(corners);
        var xs = corners.Select(c => Rect.InverseTransformPoint(c).x).ToList();
        return rightEdge ? xs.Max() : xs.Min();
    }

    private static void LimitFont(TextMeshProUGUI text, float size)
    {
        text.fontSizeMax = Math.Min(text.fontSizeMax, size);
        text.fontSize = Math.Min(text.fontSize, size);
    }

    /// <summary>A selector row showing only its label: one value, its value column and arrows hidden.</summary>
    protected void LabelOnly(string labelKey)
    {
        foreach (var name in new[] { "Image_ArrowLeft", "Image_ArrowRight", "TextMeshPro Text_Enum" })
        {
            UiTools.Find(Root.transform, name)?.gameObject.SetActive(false);
        }

        Entry.Cast<SettingsEnum>().Method_Public_Void_ObjectPublicDi2StInLi1InDi2StUnique_Boolean_Func_1_Int32_Action_1_Int32_0(
            Options(new[] { labelKey }), false,
            DelegateSupport.ConvertDelegate<Il2CppSystem.Func<int>>(new Func<int>(() => 0)),
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action<int>>(new Action<int>(_ => { })));
    }

    /// <summary>Remembers the row's texts and images for dimming (not its own background, the selection highlight).</summary>
    protected void CollectGraphics(params Graphic[] except)
    {
        foreach (var graphic in Root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.gameObject.Pointer != Root.Pointer && except.All(e => e == null || e.Pointer != graphic.Pointer))
            {
                _graphics.Add((graphic, graphic.color.a));
            }
        }
    }

    // ---------------------------------------------------------------- fluent options

    /// <summary>The row is active only while this is true (dimmed and skipped otherwise).</summary>
    public Row When(Func<bool> enabled)
    {
        _enabled = enabled;
        return this;
    }

    /// <summary>
    /// A row of a list without input shows lit (as selected: a checkbox white, not grey) while this is true: a control
    /// the page's own key works while it has the input (Playlists' speedrun clock).
    /// </summary>
    public Row Lit(Func<bool> lit)
    {
        IsLit = lit;
        return this;
    }

    internal Func<bool> IsLit { get; private set; }

    private string _labelKey;
    private string _key;
    private string _hintKey;
    private KeyLabel _keyed;
    private TextMeshProUGUI _keyedText;
    private Func<bool> _mixed;

    /// <summary>
    /// The key that works the row from elsewhere on the page, written with the label as the game writes its hints (one
    /// text, a copy of a bottom bar hint, in the label's place, size and colour): "[C] Speedrun Clock" on the keyboard,
    /// the game's button glyph for <paramref name="padAction"/> with a gamepad. <paramref name="hintKey"/> is the label
    /// as a hint text ("{0} Speedrun Clock").
    /// </summary>
    public Row WithKey(string key, GameAction padAction, string hintKey)
    {
        _key = key;
        _hintKey = hintKey;
        if (Label == null)
        {
            return this;
        }

        var item = UnityEngine.Object.Instantiate(GameAssets.MiddleControls.transform.Find("GameObject").GetChild(0).gameObject, Root.transform, false);
        item.name = "KeyedLabel";
        Screens.Disarm(item);
        UnityEngine.Object.DestroyImmediate(item.GetComponent<ContentSizeFitter>());
        var rect = item.GetComponent<RectTransform>();
        var label = Label.rectTransform;
        rect.anchorMin = label.anchorMin;
        rect.anchorMax = label.anchorMax;
        rect.pivot = label.pivot;
        rect.anchoredPosition = label.anchoredPosition;
        rect.sizeDelta = label.sizeDelta;
        _keyedText = item.GetComponent<TextMeshProUGUI>();
        _keyedText.alignment = TextAlignmentOptions.MidlineLeft;
        _keyedText.enableAutoSizing = false;
        _keyedText.enableWordWrapping = false;
        _keyed = new KeyLabel(_keyedText, padAction);
        _keyed.SetText(hintKey);
        Label.enabled = false;
        return this;
    }

    /// <summary>The label with its key, for the device in use, in the label's own size and colour.</summary>
    private void PaintKey()
    {
        _keyedText.fontSize = Label.fontSize;
        _keyedText.color = Label.color;
        if (GameInput.UsingGamepad)
        {
            _keyed.Apply();
        }
        else
        {
            UiTools.SetText(_keyedText, string.Format(Texts.Get(_hintKey), $"<color={Tokens.OrangeHex}>[{_key}]</color>"));
        }
    }
    /// <summary>While true the row stands for several rides that differ: a value shows "Mixed", a checkbox a dash.</summary>
    public Row MixedWhen(Func<bool> mixed)
    {
        _mixed = mixed;
        return this;
    }

    protected bool Mixed => _mixed != null && _mixed();

    /// <summary>Set by the list each frame: its rows show white (it has the input, or the row is lit), else grey.</summary>
    internal bool Focused { get; set; }

    /// <summary>
    /// The row is there only while this is true: otherwise it fades out moving up and the rows below slide up into its
    /// place (its size never changes). For sub-settings of a switch; whole settings the mode does not allow are dimmed instead
    /// (<see cref="When"/>).
    /// </summary>
    public Row ShowWhen(Func<bool> shown)
    {
        _shown = shown;
        MakeSpacer();
        Open = shown() ? 1f : 0f;
        _opening = Open > 0f;
        ApplyOpen();
        return this;
    }

    /// <summary>A leading icon, as in the Daily Rides panel; unlit, it shows faded (a selector at its "none" value).</summary>
    public Row WithIcon(Func<Sprite> sprite, Func<bool> lit = null)
    {
        var go = new GameObject("Icon", Il2CppType.Of<RectTransform>());
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(Root.transform, false);
        UiTools.Place(rect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(Tokens.IconRight, 0f), new Vector2(Tokens.IconSize, Tokens.IconSize));
        _icon = go.AddComponent<Image>();
        _icon.preserveAspect = true;
        _icon.raycastTarget = false;
        _iconSprite = sprite;
        _iconLit = lit;
        return this;
    }

    // ---------------------------------------------------------------- behaviour

    /// <summary>Reads the current value(s) into the row again.</summary>
    public virtual void Setup()
    {
    }

    /// <summary>Per frame, before input.</summary>
    public virtual void Tick()
    {
    }

    public void Select() => Entry?.OnSelect();

    public void Deselect() => Entry?.Method_Public_Abstract_Virtual_New_Void_0();

    /// <summary>Enter on the row; true if the row did something with it.</summary>
    public virtual bool Activate() => false;

    /// <summary>The part of the row a click acts on (or null: clicks only select it).</summary>
    public virtual RectTransform ClickTarget => null;

    /// <summary>Per frame, after the game's own input handling: the row's look.</summary>
    public virtual void Paint(bool selected)
    {
        if (_keyed != null)
        {
            PaintKey();
        }

        var factor = (Passive || _enabled == null || _enabled() ? 1f : Tokens.Dimmed) * Open;
        foreach (var (graphic, alpha) in _graphics)
        {
            if (graphic != null)
            {
                UiTools.SetAlpha(graphic, alpha * factor);
            }
        }

        if (_icon != null)
        {
            var sprite = _iconSprite();
            if (_icon.sprite != sprite)
            {
                _icon.sprite = sprite;
            }

            _icon.enabled = sprite != null;
            UiTools.SetAlpha(_icon, factor * (_iconLit == null || _iconLit() ? 1f : Tokens.Dimmed));
        }
    }

    // ---- shared by the selector rows: the game's options list format
    protected static ObjectPublicDi2StInLi1InDi2StUnique Options(IReadOnlyList<string> keys)
    {
        var options = new ObjectPublicDi2StInLi1InDi2StUnique
        {
            field_Public_Dictionary_2_String_Int32_0 = new Il2CppSystem.Collections.Generic.Dictionary<string, int>(),
            field_Public_List_1_Int32_0 = new Il2CppSystem.Collections.Generic.List<int>(),
        };
        for (var i = 0; i < keys.Count; i++)
        {
            options.field_Public_Dictionary_2_String_Int32_0[keys[i]] = i;
            options.field_Public_List_1_Int32_0.Add(i);
        }

        return options;
    }
}

/// <summary>
/// A value selector (the Options screen's enum row): left and right step through the values and stop at the ends, as
/// the game's do; an arrow lights up on the selected row when there is a value that way. A long list of numbers can
/// wrap around instead (<see cref="Wrapping"/>): one more press at an end goes to the other end, both arrows lit; held,
/// Left / Right step on after a pause, by nine after a while (the last digit keeps moving), and stop at the ends.
/// </summary>
internal sealed class ChoiceRow : Row
{
    private bool _wraps;
    private int _before;

    private readonly SettingsEnum _choice;
    private readonly Func<string[]> _keys;
    private readonly Func<int> _get;
    private readonly Action<int> _set;
    private readonly Image _left;
    private readonly Image _right;
    private readonly Il2CppSystem.Func<int> _getter;
    private readonly Il2CppSystem.Action<int> _onChange;
    private readonly TextMeshProUGUI _value;
    private int _count;
    private bool _resetup;
    private bool _wasMixed;

    public ChoiceRow(RowList list, string labelKey, Func<string[]> keys, Func<int> get, Action<int> set)
        : base(list, GameAssets.ChoiceRow, labelKey, Tokens.RowHeight)
    {
        _choice = Entry.Cast<SettingsEnum>();
        _keys = keys;
        _get = get;
        _set = set;
        _left = UiTools.Get<Image>(Root.transform, "Image_ArrowLeft");
        _right = UiTools.Get<Image>(Root.transform, "Image_ArrowRight");
        _value = UiTools.Get<TextMeshProUGUI>(Root.transform, "TextMeshPro Text_Enum");
        _getter = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<int>>(new Func<int>(() => Math.Clamp(_get(), 0, Math.Max(0, _count - 1))));
        _onChange = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<int>>(new Action<int>(OnChange));
        Setup();
        CollectGraphics(_left, _right);
    }

    /// <summary>Values (loca keys) asked again, so the list can change (the trails of another mountain).</summary>
    public override void Setup()
    {
        var keys = _keys();
        _count = keys.Length;
        _choice.Method_Public_Void_ObjectPublicDi2StInLi1InDi2StUnique_Boolean_Func_1_Int32_Action_1_Int32_0(Options(keys), false, _getter, _onChange);
    }

    /// <summary>A new press past an end goes round to the other end (a held key stops there).</summary>
    public ChoiceRow Wrapping()
    {
        _wraps = true;
        return this;
    }

    private int Index => Math.Clamp(_get(), 0, Math.Max(0, _count - 1));

    public override void Tick()
    {
        if (_resetup)
        {
            _resetup = false;
            Setup();
        }

        _before = Index;
    }

    /// <summary>
    /// After the game's row had the input (it stops at the ends): a press past an end wraps; on a mixed row it takes the
    /// value underneath when the row could not step.
    /// </summary>
    private void Wrap(bool selected, bool mixed)
    {
        if ((!_wraps && !mixed) || !selected || !Selectable || _count < 2 || Index != _before)
        {
            return;
        }

        var click = Input.GetMouseButtonDown(0);
        var left = GameInput.Pressed(GameAction.Left) || (click && UiTools.MouseOver(_left.rectTransform));
        var right = GameInput.Pressed(GameAction.Right) || (click && UiTools.MouseOver(_right.rectTransform));
        var to = !_wraps ? (left || right ? _before : -1)
            : left && _before == 0 ? _count - 1 : right && _before == _count - 1 ? 0 : -1;
        if (to >= 0)
        {
            _set(to);
            List.NotifyChanged();
            Setup();
        }
    }

    // A held arrow key on a wrapping row (the game's row takes single presses only): when it started, the next step.
    private const float FastAfter = 2f;
    private float _heldSince = -1f;
    private float _nextStep;

    /// <summary>Held Left / Right: a step after the key repeat's pause, then at its pace, by nine after a while; it stops at the ends.</summary>
    private void Repeat(bool selected)
    {
        var direction = GameInput.Held(GameAction.Left) ? -1 : GameInput.Held(GameAction.Right) ? 1 : 0;
        if (!_wraps || !selected || !Selectable || direction == 0)
        {
            _heldSince = -1f;
            return;
        }

        var now = Time.unscaledTime;
        if (_heldSince < 0f)
        {
            _heldSince = now;
            _nextStep = now + GameInput.RepeatDelay;
            return;
        }

        if (now < _nextStep)
        {
            return;
        }

        _nextStep = now + GameInput.RepeatInterval;
        var to = Math.Clamp(Index + direction * (now - _heldSince > FastAfter ? 9 : 1), 0, _count - 1);
        if (to != Index)
        {
            _set(to);
            List.NotifyChanged();
            Setup();
        }
    }

    /// <summary>The row steps on its own (and wraps); inactive rows do not change.</summary>
    private void OnChange(int value)
    {
        if (!Selectable || value == _get())
        {
            _resetup = true;
            return;
        }

        _set(value);
        List.NotifyChanged();
    }

    public override void Paint(bool selected)
    {
        base.Paint(selected);
        var mixed = Mixed;
        Wrap(selected, mixed);
        Repeat(selected);
        mixed = Mixed;

        // "Mixed" in place of the value; the row's own text comes back once it is not.
        if (mixed)
        {
            UiTools.SetText(_value, Texts.Get("LMDMOD_MIXED"));
        }
        else if (_wasMixed)
        {
            Setup();
        }

        _wasMixed = mixed;
        var index = Index;
        Lit(_left, selected && _count > 1 && (_wraps || mixed || index > 0));
        Lit(_right, selected && _count > 1 && (_wraps || mixed || index < _count - 1));

        // The arrows (not dimmed with the row: their button shades them) go with a folding row.
        foreach (var arrow in new[] { _left, _right })
        {
            if (arrow != null && arrow.gameObject.activeSelf != Open >= 1f)
            {
                arrow.gameObject.SetActive(Open >= 1f);
            }
        }
    }

    private static void Lit(Image arrow, bool lit)
    {
        var button = arrow == null ? null : arrow.GetComponent<Button>();
        if (button != null && button.interactable != lit)
        {
            button.interactable = lit;
        }
    }
}

/// <summary>
/// A switch as one checkbox (the Options row's "On" box alone in the value column): Enter or a click flips it, and
/// like the Options toggles it is white while its list has the input (green on the selected row when on), grey otherwise.
/// </summary>
internal sealed class CheckboxRow : Row
{
    private readonly SettingsToggle _toggle;
    private readonly Func<bool> _get;
    private readonly Action<bool> _set;
    private readonly RectTransform _box;
    private readonly Image _boxImage;
    private readonly Image _mark;
    private readonly Il2CppSystem.Action<bool> _onChange;
    private readonly Il2CppSystem.Func<bool> _getter;

    public CheckboxRow(RowList list, string labelKey, Func<bool> get, Action<bool> set)
        : base(list, GameAssets.ToggleRow, labelKey, Tokens.RowHeight)
    {
        _toggle = Entry.Cast<SettingsToggle>();
        _get = get;
        _set = set;

        // The row's own left = on / right = off is not used.
        _onChange = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<bool>>(new Action<bool>(_ => { }));
        _getter = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<bool>>(get);
        Setup();

        UiTools.Find(Root.transform, "Toggle_Off")?.gameObject.SetActive(false);
        UiTools.Find(Root.transform, "Toggle_On/TextMeshPro Text_On")?.gameObject.SetActive(false);
        _box = UiTools.Get<RectTransform>(Root.transform, "Toggle_On/Background");
        if (_box != null)
        {
            _box.anchoredPosition = new Vector2(Tokens.ValueCenter, 0f);
            _box.localScale = Vector3.one * Tokens.ControlScale;
            _boxImage = _box.GetComponent<Image>();
        }

        _mark = UiTools.Get<Image>(Root.transform, "Toggle_On/Background/Checkmark");
        CollectGraphics();
    }

    public override void Setup() => _toggle.Method_Public_Void_Action_1_Boolean_Func_1_Boolean_0(_onChange, _getter);

    public override RectTransform ClickTarget => _box;

    /// <summary>Flips the switch; a mixed one (on for some rides) turns on.</summary>
    public override bool Activate()
    {
        _set(Mixed || !_get());
        List.NotifyChanged();
        return true;
    }

    public override void Paint(bool selected)
    {
        base.Paint(selected);
        if (_mark == null)
        {
            return;
        }

        // Every frame: the row fades the mark its own way when it handles left and right. Mixed: the mark as a dash.
        var mixed = Mixed;
        var on = _get() || mixed;
        _mark.enabled = on;
        if (on)
        {
            _mark.canvasRenderer.SetAlpha(1f);
        }

        _mark.rectTransform.localScale = new Vector3(1f, mixed ? Tokens.MixedDash : 1f, 1f);

        // As the Options toggles: white in a list with the input, grey in one without; the selected row's box green when on.
        var shade = selected && on ? Tokens.Green : selected || Focused ? Tokens.White : Tokens.Grey;
        foreach (var graphic in new Graphic[] { _mark, _boxImage })
        {
            if (graphic != null)
            {
                graphic.color = new Color(shade.r, shade.g, shade.b, graphic.color.a);
            }
        }
    }
}

/// <summary>A group heading: the label alone, in orange, a little smaller; never selected.</summary>
internal sealed class HeadingRow : Row
{
    public HeadingRow(RowList list, string labelKey)
        : base(list, GameAssets.ChoiceRow, labelKey, Tokens.RowHeight)
    {
        LabelOnly(labelKey);
        if (Label != null)
        {
            Label.enableAutoSizing = false;
            Label.fontSize = Tokens.RowFont * Tokens.HeadingScale;
        }

        var background = Root.GetComponent<Image>();
        if (background != null)
        {
            background.enabled = false;
        }

        CollectGraphics();
    }

    public override bool Passive => true;

    public override void Paint(bool selected)
    {
        base.Paint(selected);
        if (Label != null)
        {
            Label.color = new Color(Tokens.Orange.r, Tokens.Orange.g, Tokens.Orange.b, Label.color.a);
        }
    }
}

/// <summary>
/// An entry of a pick list (pop-ups): a name on the left, a value on the right ("3/24"), the Options row's selection
/// look; Enter or a click picks it.
/// </summary>
internal sealed class ItemRow : Row
{
    private readonly Func<string> _label;
    private readonly Func<string> _value;
    private readonly Action _pick;
    private readonly TextMeshProUGUI _valueText;

    public ItemRow(RowList list, Func<string> label, Func<string> value, Action pick)
        : base(list, GameAssets.ChoiceRow, "LMDMOD_EMPTY", Tokens.RowHeight)
    {
        _label = label;
        _value = value;
        _pick = pick;
        LabelOnly("LMDMOD_EMPTY");
        var localized = Label == null ? null : Label.GetComponent<TextMeshProLocalized>();
        if (localized != null)
        {
            localized.enabled = false;
        }

        // No arrows: the name gets the room of the value column but a narrow one at the right for the value.
        var valueRight = Tokens.ItemValueRight + Tokens.ItemValueWidth;
        if (Label != null)
        {
            Label.enableWordWrapping = false;
            Label.overflowMode = TextOverflowModes.Ellipsis;
            Label.rectTransform.sizeDelta = new Vector2(list.Width - Tokens.LabelX - valueRight - Tokens.ValuePadding, Label.rectTransform.sizeDelta.y);
        }

        // The value column's text, shown again (without arrows) for the value.
        _valueText = UiTools.Get<TextMeshProUGUI>(Root.transform, "TextMeshPro Text_Enum");
        if (_valueText != null)
        {
            _valueText.gameObject.SetActive(true);
            _valueText.rectTransform.anchoredPosition = new Vector2(-(Tokens.ItemValueRight + Tokens.ItemValueWidth / 2f), 0f);
            _valueText.rectTransform.sizeDelta = new Vector2(Tokens.ItemValueWidth, _valueText.rectTransform.sizeDelta.y);
            // Right-aligned, as far in from the right as the name is from the left (counts line up).
            _valueText.alignment = TextAlignmentOptions.MidlineRight;
            var valueLocalized = _valueText.GetComponent<TextMeshProLocalized>();
            if (valueLocalized != null)
            {
                valueLocalized.enabled = false;
            }
        }

        CollectGraphics();
    }

    public override RectTransform ClickTarget => Rect;

    public override bool Activate()
    {
        _pick();
        return true;
    }

    public override void Paint(bool selected)
    {
        base.Paint(selected);
        UiTools.SetText(Label, _label() ?? "");
        UiTools.SetText(_valueText, _value?.Invoke() ?? "");
    }
}

/// <summary>A line of text across the whole row (information panels): its text and colour are asked every frame.</summary>
internal sealed class TextRow : Row
{
    private readonly Func<string> _line;
    private readonly Func<Color> _color;

    public TextRow(RowList list, Func<string> line, Func<Color> color, float height, float scale)
        : base(list, GameAssets.ChoiceRow, "LMDMOD_EMPTY", height)
    {
        _line = line;
        _color = color;
        LabelOnly("LMDMOD_EMPTY");
        var background = Root.GetComponent<Image>();
        if (background != null)
        {
            background.enabled = false;
        }

        var localized = Label == null ? null : Label.GetComponent<TextMeshProLocalized>();
        if (localized != null)
        {
            localized.enabled = false;
        }

        if (Label != null)
        {
            var rect = Label.rectTransform;
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMax = new Vector2(-24f, rect.offsetMax.y);
            UnityEngine.Object.DestroyImmediate(Label.GetComponent<ContentSizeFitter>());
            Label.enableAutoSizing = false;
            Label.fontSize = Tokens.RowFont * scale;

            // One line, never hidden (the ellipsis mode hides a line taller than the row).
            Label.enableWordWrapping = false;
            Label.overflowMode = TextOverflowModes.Overflow;
        }

        CollectGraphics();
    }

    public override bool Passive => true;

    public override void Paint(bool selected)
    {
        base.Paint(selected);
        if (Label == null)
        {
            return;
        }

        var color = _color?.Invoke() ?? Tokens.White;
        Label.color = new Color(color.r, color.g, color.b, Label.color.a);
        UiTools.SetText(Label, _line() ?? "");
    }
}
