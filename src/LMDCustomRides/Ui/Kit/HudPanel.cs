using Il2CppInterop.Runtime;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LMDCustomRides.Ui.Kit;

/// <summary>
/// A panel in the ride HUD's free bottom right corner, made like the game's live time panel (top left) mirrored and at
/// half size: its TimeBackground shadow and live time scaled as the game lays them out (the time 32 in from the shadow's
/// edge, its top 32 above the shadow's top), a caption in the "Section 1/7" type at the game's size, as far above the
/// time's digits as the game's section line is above its live time, and under the shadow a difference in the game's
/// delta type (green when faster, red when slower), as the game's delta sits under its live time panel. The game uses
/// the other bottom corners and the middle (show results, restart, hidden spots). It slides and fades with the live time
/// panel and hides when given no caption.
/// </summary>
internal sealed class HudPanel
{
    private const string Group = "PlayScreen(Clone)/PlayScreen_Group";

    private readonly RectTransform _root;
    private readonly CanvasGroup _fade;
    private readonly CanvasGroup _live;
    private readonly RectTransform _liveRect;
    private readonly TextMeshProUGUI _liveSection;
    private readonly TextMeshProUGUI _liveClock;
    private readonly TextMeshProUGUI _caption;
    private readonly TextMeshProUGUI _clock;
    private readonly TextMeshProUGUI _delta;
    private bool _captionPlaced;

    private HudPanel(Transform group)
    {
        var live = group.Find("LiveTimePanel_Canvas");
        _live = live.GetComponent<CanvasGroup>();
        _liveRect = live.GetComponent<RectTransform>();
        var s = Tokens.HudScale;

        var go = new GameObject("LMDCustomRides_HudPanel", Il2CppType.Of<RectTransform>());
        _root = go.GetComponent<RectTransform>();
        _root.SetParent(group, false);
        UiTools.Place(_root, new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, Tokens.HudPanelBottom), new Vector2(_liveRect.sizeDelta.x * s, _liveRect.sizeDelta.y * s));
        _fade = go.AddComponent<CanvasGroup>();
        _fade.blocksRaycasts = false;
        _fade.interactable = false;

        // The live panel's shadow, mirrored for the right side.
        var shadowGo = new GameObject("Shadow", Il2CppType.Of<RectTransform>());
        var shadowRect = shadowGo.GetComponent<RectTransform>();
        shadowRect.SetParent(_root, false);
        shadowRect.anchorMin = Vector2.zero;
        shadowRect.anchorMax = Vector2.one;
        shadowRect.offsetMin = Vector2.zero;
        shadowRect.offsetMax = Vector2.zero;
        shadowRect.localScale = new Vector3(-1f, 1f, 1f);
        var shadow = shadowGo.AddComponent<Image>();
        var liveShadow = live.GetComponent<Image>();
        shadow.sprite = liveShadow.sprite;
        shadow.color = liveShadow.color;
        shadow.raycastTarget = false;

        // The live time ("BestTime TextMeshPro" in the game's naming) without its best time children, placed as the game
        // places it, scaled.
        _liveClock = live.Find("BestTime TextMeshPro").GetComponent<TextMeshProUGUI>();
        _clock = Copy(_liveClock, "Clock");
        _clock.fontSize = _liveClock.fontSize * s;
        var liveClockRect = _liveClock.rectTransform;
        _clock.rectTransform.anchoredPosition = new Vector2(-liveClockRect.anchoredPosition.x * s, liveClockRect.anchoredPosition.y * s);
        _clock.rectTransform.sizeDelta = new Vector2(_root.sizeDelta.x, liveClockRect.sizeDelta.y * s);

        // The caption: the section text at the game's own size, lined up with the time; its height is set once laid out.
        _liveSection = live.Find("TextMeshPro_Section").GetComponent<TextMeshProUGUI>();
        _caption = Copy(_liveSection, "Caption");
        _caption.enableAutoSizing = false;
        _caption.fontSize = _liveSection.fontSize;
        _caption.rectTransform.sizeDelta = new Vector2(_root.sizeDelta.x, _liveSection.rectTransform.sizeDelta.y);
        _caption.rectTransform.anchoredPosition = new Vector2(_clock.rectTransform.anchoredPosition.x,
            _clock.rectTransform.anchoredPosition.y + Tokens.HudCaptionAbove - Tokens.HudClockAbove);

        // The delta: the game's delta text (from its strip, without the strip) under the shadow, right-aligned with the
        // time, as far below the shadow as the game's delta strip is below its live panel (scaled).
        var deltaSource = group.Find("Image_DeltaTimeDisplay/Text_DeltaTime").GetComponent<TextMeshProUGUI>();
        _delta = Copy(deltaSource, "Delta");
        _delta.enableAutoSizing = false;
        _delta.fontSize = deltaSource.fontSize * Tokens.HudDeltaScale;
        UnityEngine.Object.DestroyImmediate(_delta.GetComponent<ContentSizeFitter>());
        var deltaRect = _delta.rectTransform;
        deltaRect.anchorMin = new Vector2(1, 0);
        deltaRect.anchorMax = new Vector2(1, 0);
        deltaRect.pivot = new Vector2(1, 1);
        deltaRect.sizeDelta = new Vector2(_root.sizeDelta.x, Tokens.HudDeltaLine);
        deltaRect.anchoredPosition = new Vector2(_clock.rectTransform.anchoredPosition.x, -Tokens.HudDeltaBelow * s);

        go.SetActive(false);
    }

    private TextMeshProUGUI Copy(TextMeshProUGUI source, string name)
    {
        var copy = UnityEngine.Object.Instantiate(source.gameObject, _root, false);
        copy.name = name;
        for (var i = copy.transform.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(copy.transform.GetChild(i).gameObject);
        }

        UiTools.StripBehaviours(copy);
        var text = copy.GetComponent<TextMeshProUGUI>();
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.MidlineRight;
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        return text;
    }

    /// <summary>
    /// The caption's height: the game's gap between its section line and its live time's digits (measured on its own
    /// panel), between ours. The caption keeps the game's type size while the time is at half size, so the gap is
    /// measured, not scaled. Done once both panels have text laid out.
    /// </summary>
    private void PlaceCaption()
    {
        if (_captionPlaced || !_liveSection.gameObject.activeInHierarchy || string.IsNullOrEmpty(_liveSection.text) || string.IsNullOrEmpty(_clock.text))
        {
            return;
        }

        // What shows: the caption's baseline (capitals, no descenders) above the time's digit tops (cap height). All four
        // texts hang from their panel's top with the pivot at their top, so heights add up from there.
        var gap = Baseline(_liveSection) - CapTop(_liveClock);
        var y = CapTop(_clock) + gap - (Baseline(_caption) - _caption.rectTransform.anchoredPosition.y);
        _caption.rectTransform.anchoredPosition = new Vector2(_caption.rectTransform.anchoredPosition.x, y);
        _captionPlaced = true;
    }

    /// <summary>A one-line text's baseline, from its panel's top.</summary>
    private static float Baseline(TextMeshProUGUI text)
    {
        text.ForceMeshUpdate();
        var info = text.textInfo;
        var line = info != null && info.characterCount > 0 ? info.characterInfo[0].baseLine : 0f;
        return text.rectTransform.anchoredPosition.y + line;
    }

    /// <summary>The top of a one-line text's capitals and digits, from its panel's top.</summary>
    private static float CapTop(TextMeshProUGUI text)
    {
        var face = text.font.faceInfo;
        return Baseline(text) + face.capLine * text.fontSize / face.pointSize;
    }

    /// <summary>The panel once the ride HUD exists (null before).</summary>
    public static HudPanel Create(Transform ui)
    {
        var group = ui == null ? null : ui.Find(Group);
        return group == null || group.Find("LiveTimePanel_Canvas") == null || group.Find("Image_DeltaTimeDisplay") == null ? null : new HudPanel(group);
    }

    public bool Alive => _root != null;

    /// <summary>Shows a caption, a time and a difference (null: none); a null caption hides the panel.</summary>
    public void Show(string caption, string time, long? delta)
    {
        if (_root == null)
        {
            return;
        }

        var on = caption != null;
        if (_root.gameObject.activeSelf != on)
        {
            _root.gameObject.SetActive(on);
        }

        if (!on)
        {
            return;
        }

        // In and out with the live time panel: its fade, and its slide mirrored (in from the right).
        _fade.alpha = _live == null ? 1f : _live.alpha;
        var x = _liveRect == null ? 0f : -_liveRect.anchoredPosition.x;
        if (!Mathf.Approximately(_root.anchoredPosition.x, x))
        {
            _root.anchoredPosition = new Vector2(x, Tokens.HudPanelBottom);
        }

        UiTools.SetText(_caption, caption);
        UiTools.SetText(_clock, time ?? "");
        PlaceCaption();

        var showDelta = delta != null;
        if (_delta.gameObject.activeSelf != showDelta)
        {
            _delta.gameObject.SetActive(showDelta);
        }

        if (showDelta)
        {
            UiTools.SetText(_delta, Times.Delta(delta.Value));
            _delta.color = delta.Value <= 0 ? Tokens.Faster : Tokens.Slower;
        }
    }
}
