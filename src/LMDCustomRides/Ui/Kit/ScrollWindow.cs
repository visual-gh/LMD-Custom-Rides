namespace LMDCustomRides.Ui.Kit;

/// <summary>
/// A window of a fixed number of slots over a longer list, as the game's leaderboards scroll: it follows the selected
/// entry just enough to keep it in view, and when entries are out of view the first slot (above) or the last slot
/// (below) holds a chevron instead, pointing the way. Used by <see cref="Table"/> and <see cref="RowList"/>.
/// </summary>
internal sealed class ScrollWindow
{
    /// <summary>What a slot shows: an entry index, or one of these.</summary>
    public const int ArrowUp = -1;
    public const int ArrowDown = -2;
    public const int Empty = -3;

    public ScrollWindow(int slots) => Slots = slots;

    /// <summary>Slots on screen.</summary>
    public int Slots { get; }

    /// <summary>Entries in the list.</summary>
    public int Count { get; private set; }

    /// <summary>The first entry in view.</summary>
    public int First { get; private set; }

    /// <summary>The slots from an entry: whether the first is a chevron up, the last a chevron down, how many entries show.</summary>
    private (bool Up, bool Down, int Shown) Layout(int first)
    {
        var up = first > 0;
        var shown = Slots - (up ? 1 : 0);
        var down = first + shown < Count;
        if (down)
        {
            shown--;
        }

        return (up, down, Math.Max(0, Math.Min(shown, Count - first)));
    }

    /// <summary>Scrolls just enough to have the entry in view.</summary>
    public void Follow(int entry, int count)
    {
        Count = count;
        for (var i = 0; i < 4; i++)
        {
            var (_, _, shown) = Layout(First);
            if (entry < First)
            {
                First = entry;
            }
            else if (entry >= First + shown && shown > 0)
            {
                First += entry - (First + shown) + 1;
            }
            else
            {
                break;
            }
        }

        Clamp();
    }

    /// <summary>Scrolls by some entries (the wheel, a click on a chevron): true if it moved.</summary>
    public bool Scroll(int by)
    {
        var before = First;
        First += by;
        Clamp();
        return First != before;
    }

    /// <summary>More entries than slots.</summary>
    public bool Scrolls => Count > Slots;

    // The last window has a chevron up and no chevron down.
    private void Clamp() => First = Math.Clamp(First, 0, Count <= Slots ? 0 : Count - (Slots - 1));

    /// <summary>What a slot shows: an entry index, <see cref="ArrowUp"/>, <see cref="ArrowDown"/> or <see cref="Empty"/>.</summary>
    public int EntryAt(int slot)
    {
        var (up, down, shown) = Layout(First);
        if (up && slot == 0)
        {
            return ArrowUp;
        }

        if (down && slot == Slots - 1)
        {
            return ArrowDown;
        }

        var index = slot - (up ? 1 : 0);
        return index < shown ? First + index : Empty;
    }

    /// <summary>The slots in use (entries and chevrons).</summary>
    public int SlotsShown
    {
        get
        {
            var (up, down, shown) = Layout(First);
            return shown + (up ? 1 : 0) + (down ? 1 : 0);
        }
    }

    public bool ArrowAbove => Layout(First).Up;

    public bool ArrowBelow => Layout(First).Down;

    /// <summary>The first and last entries in view.</summary>
    public (int First, int Last) InView
    {
        get
        {
            var (_, _, shown) = Layout(First);
            return (First, First + Math.Max(0, shown - 1));
        }
    }

    /// <summary>Whether an entry is in view.</summary>
    public bool Shows(int entry)
    {
        var (first, last) = InView;
        return entry >= first && entry <= last && Count > 0;
    }
}
