using MelonLoader.Utils;

namespace LMDCustomRides;

/// <summary>
/// A playlist: rides (ride codes, up to <see cref="MaxRides"/>) ridden one after the other as a timed run. Every
/// playlist is a file in UserData\LMDCustomRides\Playlists, in the same text format as its share code (see
/// <see cref="Text"/>) plus its id; the mod's own (see <see cref="PlaylistStore"/>) are written there once, then they
/// are the player's like any other.
/// </summary>
internal sealed class Playlist
{
    /// <summary>Every trail of the game (24) by day and by night.</summary>
    public const int MaxRides = 48;

    /// <summary>As long as the name field takes (TextField): a pasted name too, so it always makes a file name.</summary>
    public const int MaxName = 32;

    private const string Header = "LMD Custom Rides playlist:";

    private static bool IsHeader(string line) => line.StartsWith(Header, StringComparison.OrdinalIgnoreCase);

    public string Name { get; set; }

    /// <summary>Stays the same through renames ("builtin:..." for the mod's own playlists).</summary>
    public string Id { get; set; }


    public List<RideCode> Rides { get; } = new();

    /// <summary>The share code: a name line, then one ride code per line (the id stays in the player's file only).</summary>
    public string Text => $"{Header} {Name}\n" + string.Join("\n", Rides.Select(r => r.ToString()));

    /// <summary>The file: the share code with the id.</summary>
    public string FileText => $"{Header} {Name}\nid={Id}\n" + string.Join("\n", Rides.Select(r => r.ToString()));

    public static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 12);

    /// <summary>
    /// A playlist from its share code (or file); null if the text has no ride codes (a file may, with
    /// <paramref name="allowEmpty"/>). Lines it does not know are skipped.
    /// </summary>
    public static Playlist Parse(string text, string fallbackName, bool allowEmpty = false)
    {
        var rides = RideCode.ParseAll(text);
        if (rides.Count == 0 && !(allowEmpty && IsHeader(text.TrimStart())))
        {
            return null;
        }

        var name = fallbackName;
        string id = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (IsHeader(line))
            {
                var given = line.Substring(line.IndexOf(':') + 1).Trim();
                name = given.Length > 0 ? given.Substring(0, Math.Min(given.Length, MaxName)).Trim() : name;
            }
            else if (line.StartsWith("id=", StringComparison.OrdinalIgnoreCase))
            {
                id = line.Substring(3).Trim();
            }
        }

        var playlist = new Playlist { Name = name, Id = string.IsNullOrEmpty(id) ? null : id };
        playlist.Rides.AddRange(rides.Take(MaxRides));
        return playlist;
    }
}

/// <summary>The playlists, loaded from and saved to UserData; the mod's own are written there on the first start.</summary>
internal static class PlaylistStore
{
    private static readonly string Folder = Path.Combine(MelonEnvironment.UserDataDirectory, "LMDCustomRides", "Playlists");

    public static List<Playlist> All { get; } = new();

    public static void Load()
    {
        All.Clear();
        try
        {
            Directory.CreateDirectory(Folder);
            foreach (var file in Directory.GetFiles(Folder, "*.txt").OrderBy(f => f))
            {
                // A file may hold no rides yet (a new playlist).
                var playlist = Playlist.Parse(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file), true);
                if (playlist == null)
                {
                    continue;
                }

                // The file decides the name when its header names another file (a copy made in Explorer), so every
                // playlist keeps its own file; a missing or copied id gets a new one. Either is written back once.
                var rewrite = false;
                if (!string.Equals(FileOf(playlist), file, StringComparison.OrdinalIgnoreCase))
                {
                    playlist.Name = Path.GetFileNameWithoutExtension(file);
                    rewrite = true;
                }

                if (playlist.Id == null || All.Any(p => p.Id == playlist.Id))
                {
                    playlist.Id = Playlist.NewId();
                    rewrite = true;
                }

                All.Add(playlist);
                if (rewrite)
                {
                    Save(playlist);
                }
            }
        }
        catch (Exception e)
        {
            Mod.Log.Warning("playlists could not be read: " + e.Message);
        }

        WriteModPlaylists();
        SortByName();
    }

    /// <summary>The list goes by name (so players can order it by naming).</summary>
    private static void SortByName() => All.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The mod's own playlists, written once as files (from then on the player's like any other: deleted, they stay
    /// gone; their share codes are in the README). One kept from before (same id) is left as it is.
    /// </summary>
    private static void WriteModPlaylists()
    {
        if (Settings.ModPlaylistsWritten.Value)
        {
            return;
        }

        foreach (var playlist in ModPlaylists().Where(p => All.All(o => o.Id != p.Id)))
        {
            if (NameTaken(playlist.Name))
            {
                playlist.Name = FreeName();
            }

            All.Add(playlist);
            Save(playlist);
        }

        Settings.ModPlaylistsWritten.Value = true;
        Settings.Save();
    }

    /// <summary>A ride of the mod's playlists: the Super Bike, by day, Normal, no modifiers.</summary>
    private static RideCode Ride(string route) => new RideCode
    {
        Route = route,
        Bike = 7,
        Modifiers = new[] { Loadout.SuperBikeId },
    }.Normalized();

    private static Playlist Make(string id, string name, IEnumerable<string> routes)
    {
        var playlist = new Playlist { Name = name, Id = "builtin:" + id };
        playlist.Rides.AddRange(routes.Select(Ride));
        return playlist;
    }

    /// <summary>A mountain's four trails in the game's numbering (Graterhorn's route IDs are not in trail order: A2 is its Trail 3).</summary>
    private static IEnumerable<string> Trails(string mountain) =>
        (mountain == "AL" ? new[] { 1, 3, 2, 4 } : new[] { 1, 2, 3, 4 }).Select(t => $"{mountain}_01_A{t}");

    /// <summary>The base game's 16 trails, then each mountain's (Eldfjall Island is a DLC); the names order the list.</summary>
    private static IEnumerable<Playlist> ModPlaylists()
    {
        yield return Make("16-trails", "- 16 Trails -", new[] { "AL", "AU", "CA", "RO" }.SelectMany(Trails));
        yield return Make("graterhorn", "1. Graterhorn", Trails("AL"));
        yield return Make("redmoor-peaks", "2. Redmoor Peaks", Trails("AU"));
        yield return Make("sierra-rivera", "3. Sierra Rivera", Trails("CA"));
        yield return Make("mount-riley", "4. Mount Riley", Trails("RO"));
        yield return Make("eldfjall", "5. Eldfjall", Trails("IS"));
    }

    /// <summary>Writes the playlist's file; false if it could not be written.</summary>
    public static bool Save(Playlist playlist)
    {
        try
        {
            Directory.CreateDirectory(Folder);

            // Written aside, then moved over the old file: a failed write never leaves half a playlist.
            var file = FileOf(playlist);
            File.WriteAllText(file + ".tmp", playlist.FileText);
            File.Move(file + ".tmp", file, true);
            return true;
        }
        catch (Exception e)
        {
            Mod.Log.Warning($"playlist \"{playlist.Name}\" could not be saved: {e.Message}");
            return false;
        }
    }

    private static void DeleteFile(Playlist playlist)
    {
        try
        {
            File.Delete(FileOf(playlist));
        }
        catch (Exception e)
        {
            Mod.Log.Warning("playlist file could not be deleted: " + e.Message);
        }
    }

    /// <summary>Deletes the playlist (its times stay: the same rides find them again).</summary>
    public static void Delete(Playlist playlist)
    {
        All.Remove(playlist);
        DeleteFile(playlist);
    }

    /// <summary>Renames a playlist (and its file); false if the name is taken or its file cannot be written.</summary>
    public static bool Rename(Playlist playlist, string name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || NameTaken(name, playlist))
        {
            return false;
        }

        // The new file first; the old one goes only once it is written (and not when only the letter case changed:
        // same file). A new file that cannot be written leaves the playlist as it was.
        var old = FileOf(playlist);
        var oldName = playlist.Name;
        playlist.Name = name;
        if (!Save(playlist))
        {
            playlist.Name = oldName;
            return false;
        }

        SortByName();
        if (!string.Equals(old, FileOf(playlist), StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                File.Delete(old);
            }
            catch (Exception e)
            {
                Mod.Log.Warning("old playlist file could not be removed: " + e.Message);
            }
        }

        return true;
    }

    /// <summary>The first free "Playlist N" name.</summary>
    public static string FreeName()
    {
        var n = 1;
        while (NameTaken($"Playlist {n}"))
        {
            n++;
        }

        return $"Playlist {n}";
    }

    /// <summary>A name another playlist has. Names compare as their file names, so "A/B" and "A_B" never share a file.</summary>
    public static bool NameTaken(string name, Playlist except = null) => All.Any(p => p != except && Same(p.Name, name));

    private static bool Same(string a, string b) =>
        a != null && b != null && string.Equals(SafeName(a), SafeName(b), StringComparison.OrdinalIgnoreCase);

    private static string SafeName(string name) => new(name.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());

    /// <summary>
    /// A new playlist of the player's: under the given name, or the copied playlist's, or a free one ("Playlist 2")
    /// when that is taken; with a copy of another's rides.
    /// </summary>
    public static Playlist Create(Playlist from = null, string named = null)
    {
        var name = (named ?? from?.Name)?.Trim();
        if (string.IsNullOrWhiteSpace(name) || NameTaken(name))
        {
            name = FreeName();
        }

        var playlist = new Playlist { Name = name, Id = Playlist.NewId() };
        if (from != null)
        {
            playlist.Rides.AddRange(from.Rides.Take(Playlist.MaxRides));
        }

        All.Add(playlist);
        SortByName();
        Save(playlist);
        return playlist;
    }

    private static string FileOf(Playlist playlist)
    {
        return Path.Combine(Folder, SafeName(playlist.Name) + ".txt");
    }
}
