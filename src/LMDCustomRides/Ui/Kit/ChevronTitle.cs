using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LMDCustomRides.Ui.Kit;

/// <summary>
/// A title to step through with chevrons, made of Progression's page selector (Achievements, Statistics): the dark band,
/// a small white line ("Playlist 2 / 7") over the orange headline, a chevron on each side. Left and right (or a click on
/// a chevron) step; the page does the stepping, the title shows it. A chevron is lit only while the title has the input
/// and there is somewhere to go that way, like the arrows of the game's Options rows.
/// </summary>
internal sealed class ChevronTitle
{
    private readonly TextMeshProUGUI _subLine;
    private readonly TextMeshProUGUI _headline;
    private readonly Image _previous;
    private readonly Image _next;

    public ChevronTitle(Transform parent, Vector2 position)
    {
        var root = UnityEngine.Object.Instantiate(GameAssets.ChevronTitle, parent, false);
        root.name = "LMDCustomRides_ChevronTitle";
        UiTools.StripBehaviours(root);
        root.SetActive(true);
        var rect = root.GetComponent<RectTransform>();
        rect.anchoredPosition = position;

        _subLine = UiTools.Get<TextMeshProUGUI>(root.transform, "Elements/TextMeshPro Text_SubLine");
        _headline = UiTools.Get<TextMeshProUGUI>(root.transform, "Elements/TextMeshPro Text_Headline");
        _previous = UiTools.Get<Image>(root.transform, "Elements/Image_PreviousButton");
        _next = UiTools.Get<Image>(root.transform, "Elements/Image_NextButton");

        // The game sizes the headline to its text; a long name would run under the right chevron, so it gets the room
        // between the chevrons and shrinks to fit it.
        UnityEngine.Object.DestroyImmediate(_headline.GetComponent<ContentSizeFitter>());
        var nextLeft = rect.sizeDelta.x + _next.rectTransform.anchoredPosition.x;
        HeadlineWidth = nextLeft - Tokens.ChevronTextX - Tokens.ChevronHeadlineGap;
        _headline.rectTransform.sizeDelta = new Vector2(HeadlineWidth, _headline.rectTransform.sizeDelta.y);
        _headline.enableWordWrapping = false;
        _headline.fontSizeMax = _headline.fontSize;
        _headline.fontSizeMin = _headline.fontSize * 0.6f;
        _headline.enableAutoSizing = true;
        _headline.overflowMode = TextOverflowModes.Ellipsis;
        _subLine.enableWordWrapping = false;
    }

    /// <summary>The headline's room: from the texts' left edge to before the right chevron.</summary>
    public float HeadlineWidth { get; }

    /// <summary>The headline's type size (a text field editing it in place matches it).</summary>
    public float HeadlineFontSize => _headline.fontSizeMax;

    /// <summary>The headline's top left on the parent (where a text field editing it goes).</summary>
    public static Vector2 HeadlinePosition => Tokens.ChevronPosition + new Vector2(Tokens.ChevronTextX, -Tokens.ChevronHeadlineTop);

    public void Set(string subLine, string headline)
    {
        UiTools.SetText(_subLine, subLine);
        UiTools.SetText(_headline, headline);
    }

    /// <summary>Hides the headline (while a text field edits it in place).</summary>
    public void HideHeadline(bool hide) => _headline.enabled = !hide;

    /// <summary>Lights each chevron that can step (dimmed otherwise).</summary>
    public void SetArrows(bool previous, bool next)
    {
        UiTools.SetAlpha(_previous, previous ? 1f : Tokens.ArrowOff);
        UiTools.SetAlpha(_next, next ? 1f : Tokens.ArrowOff);
    }

    /// <summary>A click on a chevron this frame: -1 previous, 1 next, else 0.</summary>
    public int Clicked()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return 0;
        }

        return UiTools.MouseOver(_previous.rectTransform) ? -1 : UiTools.MouseOver(_next.rectTransform) ? 1 : 0;
    }

    /// <summary>True when the mouse is over the band (a click there gives it the input).</summary>
    public bool MouseOver => UiTools.MouseOver(_headline.transform.parent.parent.GetComponent<RectTransform>());
}
