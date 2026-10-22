using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MelonLoader.Utils;

namespace LMDCustomRides;

/// <summary>A speedrun's times: the run clock's (full riding time), real time and pause time, per ride by ride key.</summary>
internal sealed class RunTimes
{
    public long GameMs { get; set; }
    public long RealMs { get; set; }
    public long PausedMs { get; set; }
    public DateTime Date { get; set; }

    /// <summary>Each ride's time, by <see cref="Records.RideKeys"/>.</summary>
    public Dictionary<string, long> Segments { get; set; } = new();
}

/// <summary>The last speedrun of a playlist: finished, or left at a ride.</summary>
internal sealed class LastRun
{
    public DateTime Date { get; set; }
    public bool Completed { get; set; }

    /// <summary>The ride the run reached (1-based); the ride count when completed.</summary>
    public int Reached { get; set; }

    public long GameMs { get; set; }
    public long RealMs { get; set; }
    public long PausedMs { get; set; }
}

/// <summary>A speedrun record for one set of rides (its signature): the speedrun PB, the last run and the best splits.</summary>
internal sealed class PlaylistRecord
{
    /// <summary>The playlist that last rode this set of rides.</summary>
    public string Playlist { get; set; }
    public string Signature { get; set; }

    /// <summary>The playlist's name when the record was written (for reading the file).</summary>
    public string Name { get; set; }

    public RunTimes Pb { get; set; }
    public LastRun Last { get; set; }

    /// <summary>Each ride's best split from finished speedruns only (by ride key), so they add up to a time a run can reach.</summary>
    public Dictionary<string, long> BestSplits { get; set; }

    /// <summary>Runs ridden to the end or left on the way.</summary>
    public int Runs { get; set; }

    /// <summary>Runs ridden to the end.</summary>
    public int Completed { get; set; }
}

/// <summary>
/// Times, kept locally (never uploaded). Two kinds:
/// - Game time (the game's own, the result screen's): each ride's best from any finish, wherever it was started
///   (ridebests.json: the Playlists table's times and their sum, "sum of best"), and each section's best
///   (sectionbests.json: the ride HUD's best time and difference). Both by ride code.
/// - Speedruns (records.json): a run's record belongs to its set of rides (the signature: the ride codes in any order),
///   whatever playlist holds them: moving rides or renaming keeps the speedrun PB, changing the rides starts another
///   record, and the same rides again (a change undone, a deleted playlist pasted back) find theirs again.
/// Nothing is ever deleted.
/// </summary>
internal static class Records
{
    private static readonly string Folder = Path.Combine(MelonEnvironment.UserDataDirectory, "LMDCustomRides");
    private static readonly string File = Path.Combine(Folder, "records.json");
    private static readonly string RideFile = Path.Combine(Folder, "ridebests.json");
    private static readonly string SectionFile = Path.Combine(Folder, "sectionbests.json");
    private static readonly List<PlaylistRecord> All = new();
    private static readonly Dictionary<string, long> RideBests = new();
    private static readonly Dictionary<string, List<long>> SectionBests = new();

    public static void Load()
    {
        All.Clear();
        RideBests.Clear();
        SectionBests.Clear();
        foreach (var (code, times) in Read<Dictionary<string, List<long>>>(SectionFile) ?? new())
        {
            if (times != null)
            {
                SectionBests[code] = times;
            }
        }

        var records = Read<List<PlaylistRecord>>(File);
        // A hand-edited file may hold nulls: those entries are left out.
        All.AddRange((records ?? new()).Where(r => r?.Signature != null));
        foreach (var record in All.Where(r => r.Pb != null))
        {
            record.Pb.Segments ??= new();
            // Records from before best splits were kept: the PB's splits are the best known.
            record.BestSplits ??= new Dictionary<string, long>(record.Pb.Segments);
        }

        foreach (var (code, ms) in Read<Dictionary<string, long>>(RideFile) ?? new())
        {
            RideBests[code] = ms;
        }

        MergeSameRides();
    }

    /// <summary>
    /// A file's contents, or null when it is missing or unreadable. An unreadable file is copied aside first
    /// ("records.json.20261019-101500.bad"), so the next save cannot overwrite the times it holds.
    /// </summary>
    internal static T Read<T>(string file) where T : class
    {
        if (!System.IO.File.Exists(file))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(System.IO.File.ReadAllText(file));
        }
        catch (Exception e)
        {
            var aside = $"{file}.{DateTime.Now:yyyyMMdd-HHmmss}.bad";
            try
            {
                System.IO.File.Copy(file, aside, true);
            }
            catch (Exception copy)
            {
                Mod.Log.Warning($"{System.IO.Path.GetFileName(file)} could not be copied aside: {copy.Message}");
            }

            Mod.Log.Warning($"{System.IO.Path.GetFileName(file)} could not be read ({e.Message}); kept as {System.IO.Path.GetFileName(aside)}");
            return null;
        }
    }

    /// <summary>Records of one set of rides kept by different playlists (before records followed the rides) become one.</summary>
    private static void MergeSameRides()
    {
        var merged = false;
        foreach (var group in All.GroupBy(r => r.Signature).Where(g => g.Count() > 1).ToList())
        {
            var records = group.ToList();
            var keep = records.OrderBy(r => r.Pb?.GameMs ?? long.MaxValue).First();
            keep.Runs = records.Sum(r => r.Runs);
            keep.Completed = records.Sum(r => r.Completed);
            keep.Last = records.Select(r => r.Last).Where(l => l != null).OrderByDescending(l => l.Date).FirstOrDefault();
            keep.BestSplits = records.Where(r => r.BestSplits != null).SelectMany(r => r.BestSplits)
                .GroupBy(s => s.Key).ToDictionary(g => g.Key, g => g.Min(s => s.Value));
            All.RemoveAll(r => records.Contains(r) && r != keep);
            merged = true;
        }

        if (merged)
        {
            Save();
        }
    }

    private static void Save() => Write(File, All);


    // Written aside, then moved over the old file: a failed write never leaves half a file.
    private static void Write<T>(string file, T value)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var temp = file + ".tmp";
            System.IO.File.WriteAllText(temp, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            System.IO.File.Move(temp, file, true);
        }
        catch (Exception e)
        {
            Mod.Log.Warning("records could not be saved: " + e.Message);
        }
    }

    /// <summary>A ride's best time from any attempt, or null.</summary>
    public static long? RideBest(RideCode ride) => RideBests.TryGetValue(ride.ToString(), out var ms) ? ms : null;

    /// <summary>A finished ride's game time (any custom ride, from anywhere): kept when it is the ride's best.</summary>
    public static void RideFinished(RideCode ride, long ms)
    {
        var code = ride.ToString();
        if (ms > 0 && (!RideBests.TryGetValue(code, out var best) || ms < best))
        {
            RideBests[code] = ms;
            Write(RideFile, RideBests);
        }
    }

    /// <summary>A section's best time on a ride (sections from 0), or 0 for none.</summary>
    public static long SectionBest(RideCode ride, int section) =>
        section >= 0 && SectionBests.TryGetValue(ride.ToString(), out var times) && section < times.Count ? times[section] : 0;

    /// <summary>
    /// A section of a ride passed (sections from 0): its best time so far (0 for none), and the time kept if better.
    /// Best section times come from any attempt, finished or not (sectionbests.json, by ride code).
    /// </summary>
    public static long SectionPassed(RideCode ride, int section, long ms)
    {
        if (section < 0 || ms <= 0)
        {
            return 0;
        }

        var code = ride.ToString();
        if (!SectionBests.TryGetValue(code, out var times))
        {
            SectionBests[code] = times = new List<long>();
        }

        while (times.Count <= section)
        {
            times.Add(0);
        }

        var best = times[section];
        if (best <= 0 || ms < best)
        {
            times[section] = ms;
            Write(SectionFile, SectionBests);
        }

        return best;
    }

    /// <summary>The sum of the rides' best times (one per ride in the playlist, those that have one), or null while none has.</summary>
    public static long? SumOfBest(Playlist playlist)
    {
        var bests = playlist.Rides.Select(RideBest).Where(ms => ms != null).ToList();
        return bests.Count > 0 ? bests.Sum() : null;
    }

    /// <summary>
    /// The best sections of the rides that have a best time, added up: what the sum of best can come down to (a ride
    /// without section times, like Free Rider's, counts its best time). Null while no ride has a best.
    /// </summary>
    public static long? SumOfBestSections(Playlist playlist)
    {
        long? sum = null;
        foreach (var ride in playlist.Rides)
        {
            if (RideBest(ride) is not { } best)
            {
                continue;
            }

            var sections = SectionBests.TryGetValue(ride.ToString(), out var times) && times.Count > 0 && times.All(ms => ms > 0) ? times.Sum() : best;
            sum = (sum ?? 0) + Math.Min(best, sections);
        }

        return sum;
    }

    /// <summary>The best splits of finished speedruns added up (one per ride), or null until every ride has one.</summary>
    public static long? SumOfBestSplits(Playlist playlist)
    {
        var splits = Of(playlist)?.BestSplits;
        long sum = 0;
        foreach (var key in RideKeys(playlist))
        {
            if (splits == null || !splits.TryGetValue(key, out var ms))
            {
                return null;
            }

            sum += ms;
        }

        return sum;
    }

    /// <summary>The rides as an order-independent key: the same rides in any order give the same signature.</summary>
    public static string Signature(Playlist playlist)
    {
        var codes = string.Join("\n", playlist.Rides.Select(r => r.ToString()).OrderBy(c => c, StringComparer.Ordinal));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(codes));
        return $"{playlist.Rides.Count}:{Convert.ToHexString(hash, 0, 8).ToLowerInvariant()}";
    }

    /// <summary>
    /// Each ride's key in a record: its code, numbered when the same ride is in the playlist more than once
    /// ("code#2"), so times stay with their rides when the order changes.
    /// </summary>
    public static List<string> RideKeys(Playlist playlist)
    {
        var seen = new Dictionary<string, int>();
        var keys = new List<string>();
        foreach (var ride in playlist.Rides)
        {
            var code = ride.ToString();
            seen[code] = seen.TryGetValue(code, out var n) ? n + 1 : 1;
            keys.Add(seen[code] == 1 ? code : $"{code}#{seen[code]}");
        }

        return keys;
    }

    /// <summary>The record for the playlist's current rides, or null.</summary>
    public static PlaylistRecord Of(Playlist playlist)
    {
        if (playlist == null)
        {
            return null;
        }

        var signature = Signature(playlist);
        return All.FirstOrDefault(r => r.Signature == signature);
    }

    private static PlaylistRecord Get(Playlist playlist)
    {
        var record = Of(playlist);
        if (record == null)
        {
            record = new PlaylistRecord { Signature = Signature(playlist) };
            All.Add(record);
        }

        record.Playlist = playlist.Id;
        record.Name = playlist.Name;
        return record;
    }

    /// <summary>A finished run: the last run, and the personal best if faster. True for a new personal best.</summary>
    public static bool Finished(Playlist playlist, RunTimes run)
    {
        var record = Get(playlist);
        record.Last = new LastRun
        {
            Date = run.Date, Completed = true, Reached = playlist.Rides.Count, GameMs = run.GameMs, RealMs = run.RealMs, PausedMs = run.PausedMs,
        };
        record.Runs++;
        record.Completed++;
        var best = record.Pb == null || run.GameMs < record.Pb.GameMs;
        if (best)
        {
            record.Pb = run;
        }

        record.BestSplits ??= new();
        foreach (var (key, ms) in run.Segments)
        {
            if (!record.BestSplits.TryGetValue(key, out var old) || ms < old)
            {
                record.BestSplits[key] = ms;
            }
        }

        Save();
        return best;
    }

    /// <summary>A run left before its end.</summary>
    public static void Left(Playlist playlist, int reached, RunTimes run)
    {
        var record = Get(playlist);
        record.Last = new LastRun
        {
            Date = run.Date, Completed = false, Reached = reached, GameMs = run.GameMs, RealMs = run.RealMs, PausedMs = run.PausedMs,
        };
        record.Runs++;
        Save();
    }
}
