using UnityEngine;

namespace LMDCustomRides.Ui.Kit;

/// <summary>
/// The design tokens of the mod's UI: colours, type sizes and layout measures, taken from the game's own screens
/// (Options rows, Daily Rides panel, leaderboards) on its 1920 x 1080 menu canvas. Components read these; pages
/// never hard-code a measure of their own.
/// </summary>
internal static class Tokens
{
    // ---- colour (the game's)
    public static readonly Color Orange = new(0.914f, 0.675f, 0.314f);
    public static readonly Color Green = new(0.561f, 0.737f, 0.051f);
    public static readonly Color Grey = new(0.75f, 0.75f, 0.75f);

    /// <summary>The HUD's delta text when faster (Text_DeltaTime).</summary>
    public static readonly Color Faster = new(0.61f, 0.91f, 0.31f);

    /// <summary>Slower than the best: a red in the HUD's warm palette (the game shows no slower delta in this mod's rides).</summary>
    public static readonly Color Slower = new(0.93f, 0.36f, 0.27f);
    public static readonly Color White = Color.white;
    public const string OrangeHex = "#e9ac50";

    /// <summary>Opacity of an inactive (dimmed) row.</summary>
    public const float Dimmed = 0.35f;

    /// <summary>Opacity of a chevron with nothing to go to, or while its title has no input.</summary>
    public const float ArrowOff = 0.25f;

    // ---- type (pt on the menu canvas)
    public const float RowFont = 36f;        // Daily Rides panel text
    public const float HeadingScale = 0.8f;  // group headings, of the row font
    public const float TitleScale = 1.2f;    // information panel titles
    public const float LineScale = 0.75f;    // information lines
    public const float TableFont = 30f;    // the leaderboard's 40, for the shorter rows

    // ---- layout: the row list on the left (where the Daily Rides panel is)
    public static readonly Vector2 ListPosition = new(64f, -250f);
    public const float ListWidth = 720f;
    public const float RowHeight = 50f;
    public const float LabelX = 64f;

    // Row anatomy: a value column on the right with the arrows, the value centred between them.
    public const float ValueColumn = 326f;
    public const float ValueCenter = -172f;
    public const float ValueWidth = 240f;
    public const float ArrowLeftX = -310f;
    public const float ArrowRightX = -34f;
    public const float ControlScale = 0.85f;
    public const float MixedDash = 0.3f;        // a mixed checkbox's mark, squashed into a dash
    public const float ValuePadding = 6f;       // between the value and each arrow

    // Pick list entries (no arrows): the value (a ride count) right-aligned in a narrow column, as far in from the right
    // as the name is from the left; the name gets the rest.
    public const float ItemValueWidth = 80f;
    public const float ItemValueRight = LabelX;

    // Leading icons as in the Daily Rides panel: 28 px, their right edge 8 px before the label.
    public const float IconSize = 28f;
    public const float IconRight = 56f;

    // ---- layout: the information area on the right
    public const float TitleHeight = 44f;
    public const float LineHeight = 32f;
    // ---- layout: the table on the right, a copy of the Daily Rides leaderboard (whose top is at y 260): 16 rows fit
    // above the bottom bar (the game's rows are 51.6 px for 10; ours are 37 for 16)
    public static readonly Vector2 TablePosition = new(820f, -260f);
    public const float TableWidth = 1036f;
    public const float TableHeaderHeight = 48f;
    public const float TableRowHeight = 37f;
    public const float TableHeaderFont = 36f;

    // Table anatomy, in three parts: the leaderboard's 64 px margins (its strips fade out toward the sides); the fixed
    // columns from the left (# / trail, "Redmoor Peaks · 4/4" its widest, the column ending with it); the mode, floating,
    // centred between the trail and the time's figures: bike and time of day icons, the text ("Free Rider" 99 px, the
    // widest), then the five modifier slots, spaced as icons are; the time and the difference at the right.
    public const float TableMargin = 64f;
    public const float TableTimeWidth = 120f;
    public const float TableTimeInk = 72f;        // "0:00.00" at the table's type: the figures' width in the time column
    public const float TableDeltaWidth = 100f;
    public const float TableIconGap = 6f;
    public const string TableHeldMarker = ">";    // in place of a held (moving) entry's number
    public const string LockSprite = "LMD_Lock";  // the game's lock (bike selector headline, locked list buttons)
    public const string TabSprite = "LMD_MenuIcon_Goal";  // the tab's icon: the flag of the Daily Rides' Mode row
    public const float NumberColumn = 52f;
    public const float TrailColumn = 207f + TableIconGap + 3f;
    public const float ModeColumn = 2 * (IconSize + TableIconGap) + 99f + TableIconGap + 5 * IconSize + 4 * TableIconGap;

    // The leaderboard filters' chevron (ArrowButton, 20 px at 1.5x, turned 270 degrees for "down").
    public const string ArrowSprite = "ArrowButton";
    public const float TableArrowSize = 20f;
    public const float TableArrowScale = 1.5f;

    // ---- layout: the chevron title (a copy of Progression's SubScreenNavigation, where the game has it); its texts
    // start 196 in, and the figures under it line up with them
    public static readonly Vector2 ChevronPosition = new(0f, -260f);
    public const float ChevronTextX = 196f;
    public const float ChevronHeadlineTop = 48f;   // the headline's top below the band's top
    public const float ChevronHeadlineHeight = 68f;
    public const float ChevronHeadlineGap = 20f;   // between the headline's room and the right chevron

    // ---- layout: the figures under the title (whose band ends at y 388): a Statistics metric (caption over a large
    // value) with its caption about 48 px under the band, then lines
    public static readonly Vector2 StatBlockPosition = new(ChevronTextX, -428f);
    public const float StatBlockWidth = 560f;
    public const float StatBlockHeight = 150f;
    public const float StatChangeFont = 36f;     // the result screen's Text_ChangeTime
    public const float StatChangeWidth = 200f;
    public const float StatChangeGap = 12f;
    // The lines' text (64 into its row) under the figure's caption (16 into its strip).
    public static readonly Vector2 StatsPosition = new(ChevronTextX + 16f - LabelX, -586f);
    public const float StatsWidth = 600f;

    // An option row under the figures (the speedrun clock): centred between the fifth line (ends at y 746) and the
    // bottom bar's band (from y 935).
    public static readonly Vector2 OptionPosition = new(StatsPosition.x, -815f);

    // ---- layout: the hints in the middle of the bottom bar (the room between the 512 px back and action buttons)
    public const float HintBarWidth = 880f;
    public const float HintSpacing = 40f;

    // ---- pop-ups (the game's 720 x 360 box): a pick list between the headline (32 + 72 from the top) and the key
    // labels (32 + 50 from the bottom)
    public const float PopupMargin = 64f;
    public const float PopupListTop = 128f;
    public const float PopupListBottom = 112f;
    public const int PopupMaxRows = 8;          // a longer list scrolls, with chevron rows like the table
    public const string PopupBackKey = "LMDMOD_ACTION_BACK";

    // ---- the ride HUD (PlayScreen_Group, 1920 x 1080): the run panel is the live time panel at half size, mirrored in
    // the free bottom right corner (the game's "Show Results" sits bottom left at y 76, its prompts bottom middle)
    public const float HudScale = 0.5f;
    public const float HudDeltaScale = 0.7f;    // the delta text stays readable
    public const float HudPanelBottom = 76f;    // the shadow's bottom, level with the game's bottom left prompt
    public const float HudCaptionAbove = 48f;   // "Section 1/7" top above the shadow top (live panel), scaled
    public const float HudClockAbove = 32f;     // the big time's top above the shadow top (live panel), scaled
    public const float HudDeltaLine = 30f;      // the delta's line under the shadow
    public const float HudDeltaBelow = 26f;     // the game's delta strip top below its live panel's bottom (scaled)

    // ---- the game's sprites used by components
    public const string LightStrip = "BackgroundGradient70";
    public const string DarkStrip = "BackgroundGradient40";

    // ---- timing
    public const float MessageSeconds = 4f;
    public const float CollapseSeconds = 0.15f;
    public const float CollapseSlide = 12f;    // how far a row moves while it fades in or out
}
