using Il2CppTMPro;
using UnityEngine;

namespace LMDCustomRides.Ui.Kit;

/// <summary>
/// A figure as the Statistics pages show one (a copy of their "Total Time", without its strips): a white caption over a
/// large orange value. A difference can go with the value the way the result screen puts one on a time
/// (Text_ChangeTime): small, green or red, at the value's top right. Lines under it go in the page's Stats.
/// </summary>
internal sealed class StatBlock
{
    private readonly GameObject _root;
    private readonly TextMeshProUGUI _caption;
    private readonly TextMeshProUGUI _value;
    private readonly TextMeshProUGUI _change;

    public StatBlock(Transform parent, Vector2 position, float width, float height)
    {
        _root = UnityEngine.Object.Instantiate(GameAssets.Metric, parent, false);
        _root.name = "LMDCustomRides_StatBlock";
        UiTools.StripBehaviours(_root);
        _root.SetActive(true);
        UiTools.Place(_root.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), position, new Vector2(width, height));
        // Without the Statistics page's dark strips: on this page they read as hard-edged boxes around a lone figure.
        foreach (var strip in _root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            strip.enabled = false;
        }

        _caption = UiTools.Get<TextMeshProUGUI>(_root.transform, "Image/TextMeshPro Text_Header");
        _value = UiTools.Get<TextMeshProUGUI>(_root.transform, "Image (1)/TextMeshPro Text_Stats");
        foreach (var text in new[] { _caption, _value })
        {
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        // The difference: the value's type, small, placed from the value's pivot (top middle) once its text is laid out.
        var change = UnityEngine.Object.Instantiate(_value.gameObject, _value.transform, false);
        change.name = "Change";
        _change = change.GetComponent<TextMeshProUGUI>();
        _change.enableAutoSizing = false;
        _change.fontSize = Tokens.StatChangeFont;
        _change.alignment = TextAlignmentOptions.TopLeft;
        var rect = _change.rectTransform;
        rect.anchorMin = _value.rectTransform.pivot;
        rect.anchorMax = _value.rectTransform.pivot;
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(Tokens.StatChangeWidth, Tokens.StatChangeFont * 1.25f);
    }

    /// <summary>The caption, the value and a difference with its colour (null: none); a null caption hides the figure.</summary>
    public void Set(string caption, string value, string change = null, Color? changeColor = null)
    {
        var shown = caption != null;
        if (_root.activeSelf != shown)
        {
            _root.SetActive(shown);
        }

        if (!shown)
        {
            return;
        }

        UiTools.SetText(_caption, caption);
        if (_value.text != (value ?? ""))
        {
            _value.text = value ?? "";
            _value.ForceMeshUpdate();
            // At the value's top right, its top level with the digits' tops (the box adds the font's ascender room).
            var bounds = _value.textBounds;
            _change.rectTransform.anchoredPosition = new Vector2(bounds.max.x + Tokens.StatChangeGap, bounds.max.y + Tokens.StatChangeFont * 0.2f);
        }

        UiTools.SetText(_change, change ?? "");
        if (changeColor != null)
        {
            _change.color = changeColor.Value;
        }
    }
}
