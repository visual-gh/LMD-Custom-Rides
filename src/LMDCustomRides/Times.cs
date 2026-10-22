namespace LMDCustomRides;

/// <summary>Times as the game writes them.</summary>
internal static class Times
{
    /// <summary>"1:42.31" or "1:02:03.45", to the nearest hundredth as the game shows times.</summary>
    public static string Format(long ms)
    {
        var t = TimeSpan.FromMilliseconds(Hundredths(Math.Max(0, ms)));
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 10:00}"
            : $"{(int)t.TotalMinutes}:{t.Seconds:00}.{t.Milliseconds / 10:00}";
    }

    /// <summary>Milliseconds to the nearest hundredth of a second.</summary>
    public static long Hundredths(long ms) => (long)Math.Round(ms / 10.0, MidpointRounding.AwayFromZero) * 10;

    /// <summary>A difference against a best time: "-0.80", "+1:02.31".</summary>
    public static string Delta(long ms)
    {
        var sign = ms < 0 ? "-" : "+";
        var t = TimeSpan.FromMilliseconds(Hundredths(Math.Abs(ms)));
        return t.TotalMinutes >= 1
            ? $"{sign}{(int)t.TotalMinutes}:{t.Seconds:00}.{t.Milliseconds / 10:00}"
            : $"{sign}{t.Seconds}.{t.Milliseconds / 10:00}";
    }

    /// <summary>A pause total, to the second: "0:42".</summary>
    public static string Short(long ms)
    {
        var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }
}
