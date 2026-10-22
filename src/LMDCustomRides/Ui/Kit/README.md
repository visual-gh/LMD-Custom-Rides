# UI kit

The mod's screens are built from these components, and the components are built from the game's own UI pieces. The
goal is that nothing looks or behaves like it was bolted on.

## Rules

1. **Pages never hard-code a number.** Colours, type sizes and measures live in `Tokens`. A new measure goes into
   `Tokens` first, with a comment saying which game screen it comes from.
2. **Native pieces first.** A component copies a game object (an Options row, the result screen's hints, the HUD's
   live time panel, a pop-up) and adjusts it, rather than drawing its own.
3. **New elements go into the kit before a page uses them.** Pages compose components; they don't create GameObjects.
4. **Input goes through `GameInput`** (Rewired actions), so keyboard, gamepad and rebinds all work. Keyboard-only
   features (clipboard, R for rename) say so (`HintBar.AddKeys`: hidden while a gamepad is used).

## Foundations

| Component | What it is |
|---|---|
| `Tokens` | Colours (`Orange`, `Green`, `Faster`, `Slower`...), type sizes, layout measures (list, info area, hint bar, pop-up, HUD panel), sprite names, timings. |
| `GameAssets` | Where the game's pieces come from: Options rows, result screen middle controls, the Daily Rides pop-up (`PopUp`) and leaderboard (`Leaderboard`), Progression's page selector (`ChevronTitle`), a Statistics figure (`Metric`), the debug `TMP_InputField`, the font, sprites by name. `Ready` once the menu has loaded. |
| `GameInput` | `Pressed`, `Held`, `Step` (with key repeat) for `GameAction` Submit, Up, Down, Left, Right, Secondary (Space), Tertiary (T), Quaternary (Z), Cancel (Escape), PadExtra (X on a gamepad only: it is Left Ctrl on the keyboard; `PadPressed`); `PressedThrough` (a text field's own confirm / cancel); `Copy`/`Paste`; `UsingGamepad`; `MenuHeld` (a pop-up or text field holds the input: the game menu's Back and the pages wait). |
| `UiTools` | `Find`, `Get`, `StripBehaviours`, `SetText` (only when changed), `Place`, `MouseOver`, `SetAlpha`. |
| `Screens` | `Register` (a page's show/hide animation includes the element), `Disarm` (a copied button no longer calls its original screen). |
| `KeyLabel` | A copy of an input-bound text: `"{0} Start Ride"` with the device's key or button written in. Used by hints and pop-up buttons. |

## Rows and lists

`RowList` is a vertical list navigated like the Options screen (up/down skip inactive rows and wrap around the ends,
the mouse selects, left/right go to the selected row).

```csharp
var list = new RowList(screen.transform, Tokens.ListPosition, Tokens.ListWidth);
list.Choice("LMDMOD_OBSTACLES", () => new[] { "LMDMOD_NONE", "LMDMOD_FEW" }, () => (int)value, v => value = v)
    .WithIcon(() => icon);
list.Choice("LMDMOD_LAYOUT", Layouts, () => layout, v => layout = v)
    .ShowWhen(() => value != Obstacles.Off);     // folds away when off
list.Checkbox("LMDMOD_MIRRORED", () => mirrored, v => mirrored = v)
    .When(() => mode != RideMode.FreeRider);     // dimmed and skipped
list.Changed += Save;
```

| Row | Made with | Behaviour |
|---|---|---|
| `ChoiceRow` | `Choice(label, keys, get, set)` | The Options enum row: values stop at the ends as in Options, arrows lit only toward a value; `.Wrapping()` lets a new press go round, and a held arrow step on (by nine after 2 s, stopping at the ends) (Layout, Checkpoint Route). |
| `CheckboxRow` | `Checkbox(label, get, set)` | One box; `RowList.Activate()` (New Ride: Space, X on a gamepad) or a click flips it; white when selected, grey otherwise. |
| `ItemRow` | `Item(label, value, pick)` | A pick-list entry (name left, value right); Enter or a click picks it. |
| `HeadingRow` | `Heading(label)` | Orange group heading, never selected. |
| `TextRow` | `Text(line, color)` | A line of text (figures under a `StatBlock`). |

Options on every row: `.When(enabled)` dims and skips; `.ShowWhen(shown)` folds the row away; `.Lit(lit)` paints a
row of a list without input as selected (a checkbox white) while true, for a control a page key works on (Playlists'
speedrun clock, flipped with [C] or X anywhere on the page; `.WithKey(key, padAction, hintKey)` writes its key before the label). The row never changes
size: it leaves the list's layout and a spacer before it takes its height (animated over `Tokens.CollapseSeconds`,
ease-out), so the rows below slide; the row itself fades in moving down `Tokens.CollapseSlide` px into place, or fades
out moving up. **Kit rule:** sub-settings fold away with their parent switch; whole settings the mode doesn't allow are
dimmed, so the player still sees they exist.

`ChoiceRow` values sit centred between the arrows as they are drawn (the right arrow is mirrored about its pivot),
with `Tokens.ValuePadding` on both sides, so a value that shrinks to fit stays centred.
`.WithIcon(sprite, lit)` adds a leading icon as in the Daily Rides panel.

`RowList.Clear()` empties a list that is filled anew each time (pop-ups). `RowList.Scrolling(slots)` caps how many rows show (pick lists, whose rows do not fold): a longer list scrolls with the selection (still wrapping), a chevron row standing in at the top / bottom for rows out of view, like the `Table`; a click on a chevron or the mouse wheel moves the selection. `Slots` is the rows on screen, for sizing what holds the list (the pop-up uses `Tokens.PopupMaxRows`). `ItemRow`s give the name the room of the arrows (the value sits in a narrow column at the right).

## Titles, figures and tables

- `ChevronTitle`: a copy of Progression's page selector (`SubscreenNavigation`, Achievements / Statistics): the dark
  band at (0, -260), a small white line over the orange headline, a chevron each side. `Set(subLine, headline)`;
  `SetArrows(previous, next)` lights the chevrons while the title has the input (else `Tokens.ArrowOff`); `Clicked()` gives -1 / 1 for a chevron click; `HideHeadline` while a `TextField` edits it in place
  (`HeadlinePosition`, `HeadlineWidth`, `HeadlineFontSize`). Long headlines shrink to fit between the chevrons.
- `StatBlock`: a copy of a Statistics page figure (`StatisticsDisplay_Small` "Total Time") without its strips: a white
  caption over a large orange value, and optionally a difference on the value as the result screen puts one on a time
  (`Text_ChangeTime`: small, green/red, top right). `Set(caption, value, change, color)`; a null caption hides it.
  Lines under it go in the page's `Stats`.
- `Table`: a copy of the Daily Rides leaderboard: header captions, strips stacked edge to edge, its 64 px margins.
  Columns run from the left (`TableColumn(header, width, icons)`: a column's icon slots come first, from its left
  edge, then its text; a null header leaves the caption empty); cells are `TableCell(text, icons)` with
  `TableIcon(sprite)`.
  Icons take the row's colour (the game's icons are white sprites, so a selected row's icons go green, a held one's
  orange). The last column may float (`Floats`): it sits centred between the fixed columns and the time's figures
  (`Tokens.TableTimeInk`), so its gaps stay even with or without the difference column. The time and, when asked, the difference sit at the right (`ShowDelta`; without it the time moves to the right
  edge). Styles `Normal`, `Selected` (green), `Held` (orange, being moved). It shows a window over
  a longer list (a `ScrollWindow`, shared with `RowList.Scrolling`): `Follow(entry, entries)` scrolls just enough to keep an entry in view (as the leaderboards do); when
  entries are out of view the first / last row shows the leaderboard filters' chevron instead (`EntryAt(row)` gives
  an entry or `ArrowUp` / `ArrowDown`, `SetArrow` draws it; a click on it scrolls); `Scroll()` takes the mouse wheel,
  `Clicked()` gives the clicked entry, `Finish(rowsShown)` ends the strips; `SetShown(false)` hides the whole table.

```csharp
var table = new Table(parent, Tokens.TablePosition, Tokens.TableWidth, 16, RideIcons.Columns, "LMDMOD_COLBEST", "LMDMOD_COLDELTA");
table.Follow(selected, rides.Count);
for (var row = 0; row < table.Count; row++)
{
    var entry = table.EntryAt(row);
    if (entry is Table.ArrowUp or Table.ArrowDown) table.SetArrow(row, entry == Table.ArrowUp);
    else if (entry >= 0) table.Set(row, RideIcons.Cells(rides[entry], entry + 1), "1:23.45", TableStyle.Selected, "-0.80", Tokens.Faster);
    else table.Set(row, null);
}
table.Finish(table.RowsShown);
table.ShowDelta(afterRun);
```

`RideIcons` holds the playlist table's columns and a ride's cells: # / Trail (the mountain) / the trail ("2/4", no
caption; DLC fifth trails "5/4") / Mode (bike icon, time of day icon, "Normal" or "Free Rider": the game has no mode
icon), then the ride's modifiers packed after the text in New Ride's order (obstacles, checkpoints, mirrored,
sprinting, flying). The mode floats between the trail and the time.

## Bottom bar

- `HintBar`: hints in the middle of the bottom bar, a copy of the result screen's middle controls, lined up with the
  back and action labels (`Align`). `Add(GameAction, "{0} Text")` is a native hint showing the device's key or button;
  `AddKeys("Ctrl+C", key)` a keyboard-only one. `.When(visible)`, `.OnlyWithKeyboard()`, `Say(message)` for a few
  seconds. Clipboard keys get no hint (a power-user trick).
- `BottomBar`: the action button's label (`SetAction("{0} Start Ride")`), the back button's label through its own
  setter (`SetBack("{0} Back to Playlist")`; pages return to their screen's `m_backLocaID`), its fade (`Alpha`) and
  label rect (`BackLabel`). Up to five short hints fit between back and action.
- `GameInput.Back`: Escape for the page showing; a page that uses it (leaving its table) keeps the menu where it is.
- `GameInput.Key(KeyCode)`: a keyboard-only key of the mod's (R: rename), hinted with `AddKeys`.

## Pop-ups and text

- `Popup`: a copy of the Daily Rides pop-up. It shows and hides itself with the element's own animations
  (`ScreenElement` virtual slots 14 / 15: `Method_Public_Virtual_New_Void_3` / `_4`) and registers with the game menu
  (`GameMenu.Method_Public_Void_ScreenElement_0` / `_1`, which only lock the tabs; they never show or hide anything).
  `Open(headline, message, acceptKey, accept)`; with a null `accept`, Enter picks the `List`'s selected entry.
  `OpenInput(headline, text, acceptKey, done)` turns it into a name field (typed on a keyboard; a gamepad's A keeps the
  text filled in). Escape closes it without leaving the menu (`GameInput.MenuHeld` blocks `GameMenu.OnBackButton`). `Tick()` switches it off after its hide animation;
  `Reset()` closes it at once (the page calls it when it shows and hides: a fallback). Every `Page` has one.
- `TextField`: a one-line text input (the debug seed field restyled), optionally at a given type size (to edit a
  headline in place). While it is open the game menu ignores keys (the game's typing events 700/701); Enter or A confirms,
  Escape or B cancels.

## Ride HUD

- `HudPanel`: a panel in the HUD's free bottom right corner: the live time panel mirrored at half size (shadow and
  time laid out as the game lays them out, scaled), a caption in the "Section" type at the game's 24 pt, as far above
  the digits as the game's section line is (measured at runtime from baselines and cap height: TMP `textBounds` are
  line metrics, not ink), and the difference in the game's delta type outside the shadow, under it (the game's delta
  strip gap, scaled). It slides (from the right) and fades with the live time panel.
  `Show(caption, time, delta)`; a null caption hides it.

## Other

- `Fade`: a black screen over everything, for hops between menus.

## Pages

Pages (`Ui\Pages\Page`) lay these out the same way: a `RowList` on the left (`List`) or a title with lines of figures
under it (`Stats`), more row lists where it needs them (`AddRows(position, width)`: shown, animated and painted with
the page, without input; Playlists' speedrun clock checkbox), what the page adds on the right (a map, a `Table`), the
`HintBar`, the action button (`ActionKey`), the back label (`BackKey`) and a `Popup`. Lifecycle: `Build`, `OnShow`,
`OnHide`, `HandleInput` (only while the page has the input), `Draw` (every frame while shown), `OnEnter`, `OnBack`
(Escape; true keeps the menu). A page with two places for the input (Playlists: the `ChevronTitle` and the `Table`)
keeps a flag for which has it; the hints, the back label and the lit chevrons / green row follow it.
