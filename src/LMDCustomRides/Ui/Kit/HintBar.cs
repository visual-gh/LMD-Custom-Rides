using Il2CppMegagon.Downhill.UI;
using Il2CppTMPro;
using LMDCustomRides.Game;
using UnityEngine;

namespace LMDCustomRides.Ui.Kit;

/// <summary>
/// The key hints in the middle of the bottom bar, between back and action: a copy of the result screen's middle
/// controls (its soft background fading to both sides, its hints laid out side by side). A hint bound to a game action
/// is the game's own hint element: it shows that action's key or gamepad button for the device in use. Keyboard-only
/// hints (clipboard) hide while a gamepad is used. A message replaces the hints for a few seconds.
/// </summary>
internal sealed class HintBar
{
    /// <summary>One hint: its visibility and text can follow the page's state.</summary>
    public sealed class Hint
    {
        private readonly KeyLabel _label;

        internal Hint(GameObject item, GameAction? action, bool keyboardOnly)
        {
            Item = item;
            _label = action == null ? null : new KeyLabel(item.GetComponent<TextMeshProUGUI>(), action.Value);
            KeyboardOnly = keyboardOnly;
        }

        internal GameObject Item { get; }
        internal bool KeyboardOnly { get; }
        internal Func<bool> Visible { get; private set; }

        /// <summary>Shown only while this is true.</summary>
        public Hint When(Func<bool> visible)
        {
            Visible = visible;
            return this;
        }

        /// <summary>Binds the hint to another game action (a page whose keys differ by device).</summary>
        public void SetAction(GameAction action) => _label?.SetAction(action);

        /// <summary>The hint's text, a loca key with "{0}" for the key ("{0} Remove Ride").</summary>
        public void SetText(string locaKey) => _label?.SetText(locaKey);

        internal void Apply() => _label?.Apply();
    }

    private readonly GameObject _root;
    private readonly CanvasGroup _group;
    private readonly Transform _items;
    private readonly GameObject _template;
    private readonly TextMeshProUGUI _message;
    private readonly List<Hint> _hints = new();
    private string _messageText;
    private float _messageUntil;
    private bool _aligned;

    private const float FadeOut = 0.15f;

    public HintBar(Transform parent)
    {
        _root = UnityEngine.Object.Instantiate(GameAssets.MiddleControls, parent, false);
        _root.name = "LMDCustomRides_HintBar";
        Screens.Disarm(_root);
        _root.SetActive(true);
        _group = _root.GetComponent<CanvasGroup>() ?? _root.AddComponent<CanvasGroup>();
        _group.alpha = 0f;

        // Centred in the room between the back and action buttons, however many hints show.
        _items = _root.transform.Find("GameObject");
        // The layout sizes each hint to its text (the copies' own size fitters would fight it).
        var layout = _items.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = Tokens.HintSpacing;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            // Each hint keeps its height, centred on the band's midline (set in Align) like the back and action labels.
            layout.childControlHeight = false;
        }

        var itemsRect = _items.GetComponent<RectTransform>();
        itemsRect.sizeDelta = new Vector2(Tokens.HintBarWidth, itemsRect.sizeDelta.y);
        itemsRect.anchoredPosition = new Vector2(0f, itemsRect.anchoredPosition.y);

        // The result screen's own hints go; the first is the template for ours.
        _template = _items.GetChild(0).gameObject;
        _template.SetActive(false);
        UnityEngine.Object.DestroyImmediate(_template.GetComponent<UnityEngine.UI.ContentSizeFitter>());
        var element = _template.GetComponent<UnityEngine.UI.LayoutElement>();
        if (element != null)
        {
            element.preferredWidth = -1f;
            element.minWidth = -1f;
        }

        var templateRect = _template.GetComponent<RectTransform>();
        templateRect.pivot = new Vector2(0.5f, 0.5f);
        for (var i = _items.childCount - 1; i > 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(_items.GetChild(i).gameObject);
        }

        var message = Item("Message");
        UnityEngine.Object.DestroyImmediate(message.GetComponent<InteractiveUIElement>());
        _message = message.GetComponent<TextMeshProUGUI>();
    }

    private GameObject Item(string name)
    {
        var item = UnityEngine.Object.Instantiate(_template, _items, false);
        item.name = "Hint_" + name;
        return item;
    }

    /// <summary>A hint for one of the game's actions: "{0} Add to Playlist" with the action's key or button.</summary>
    public Hint Add(GameAction action, string locaKey)
    {
        var item = Item(locaKey);
        var hint = new Hint(item, action, false);
        hint.SetText(locaKey);
        _hints.Add(hint);
        return hint;
    }

    /// <summary>A keyboard-only hint ("[Ctrl+C] Copy code"), written as the game writes its keys.</summary>
    public Hint AddKeys(string keys, string textKey)
    {
        var item = Item(textKey);
        UnityEngine.Object.DestroyImmediate(item.GetComponent<InteractiveUIElement>());
        item.GetComponent<TextMeshProUGUI>().text = $"<color={Tokens.OrangeHex}>[{keys}]</color> {Texts.Get(textKey)}";
        var hint = new Hint(item, null, true);
        _hints.Add(hint);
        return hint;
    }

    /// <summary>A message in place of the hints for a few seconds.</summary>
    public void Say(string message)
    {
        _messageText = message;
        _messageUntil = Time.unscaledTime + Tokens.MessageSeconds;
    }

    /// <summary>
    /// The result screen's hints sit in a lower band than the game menu's back and action labels: the hints take the
    /// back label's band (its height and its height above the bar's bottom), so all texts on the bar share a line.
    /// </summary>
    private void Align()
    {
        // The label's band inside its button (both stretch over the bar's height, which this bar shares), read once its
        // slide-in animation is over.
        var back = BottomBar.BackLabel;
        if (_aligned || back == null || BottomBar.Alpha < 1f)
        {
            return;
        }

        var items = _items.GetComponent<RectTransform>();
        items.offsetMin = new Vector2(items.offsetMin.x, back.offsetMin.y);
        items.offsetMax = new Vector2(items.offsetMax.x, back.offsetMax.y);
        _aligned = true;
    }

    /// <summary>Per frame: fades in and out with the bottom bar while its screen is the current one.</summary>
    public void Update(bool shown)
    {
        Align();
        _group.alpha = shown ? BottomBar.Alpha : Mathf.MoveTowards(_group.alpha, 0f, Time.unscaledDeltaTime / FadeOut);
        var messaging = Time.unscaledTime < _messageUntil;
        Show(_message.gameObject, messaging);
        if (messaging)
        {
            UiTools.SetText(_message, _messageText);
        }

        var gamepad = GameInput.UsingGamepad;
        foreach (var hint in _hints)
        {
            Show(hint.Item, !messaging && !(hint.KeyboardOnly && gamepad) && (hint.Visible == null || hint.Visible()));
            hint.Apply();
        }
    }

    private static void Show(GameObject go, bool on)
    {
        if (go.activeSelf != on)
        {
            go.SetActive(on);
        }
    }
}
