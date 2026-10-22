namespace LMDCustomRides;

/// <summary>
/// Checks of the data logic (ride codes, playlist text, record keys, damaged files), run on demand by the dev-only
/// recon mod (`selfcheck`); touches no player data (a damaged file is made in the temp folder).
/// </summary>
internal static class SelfCheck
{
    public static string Run()
    {
        var failed = new List<string>();
        var count = 0;

        void Check(bool ok, string what)
        {
            count++;
            if (!ok)
            {
                failed.Add(what);
            }
        }

        const string code = "IS_01_A1/D/N/7/33979052/221790175527461/MB/MM/OM/ACP/US/BAC";
        Check(RideCode.TryParse(code, out var ride) && ride.ToString() == code, "ride code round trip");
        Check(RideCode.TryParse(" \"is_01_a1/n/n/3/12/7/acp\" ", out var loose) && loose.Path == "700000000000000" && loose.Night && loose.Mode == RideMode.Normal,
            "ride code: case, quotes, short path padded");
        Check(RideCode.TryParse("AL_01_A1/D/N/1/5/1", out var plain) && RideCode.TryParse("AL_01_A1/D/N/1/99/2", out var plain2) &&
              plain.ToString() == plain2.ToString() && plain.Seed == Loadout.LayoutSeed(1) && plain.Path == Loadout.RoutePath(1) &&
              RideCode.TryParse("AL_01_A1/D/N/1/5/1/OF", out var few) && few.Seed == 5 && few.Path == Loadout.RoutePath(1),
            "ride code: seed and path a ride does not use are the defaults");
        Check(RideCode.TryParse("AL_01_A1/D/N/7/5/1/US/MM", out var unordered) && RideCode.TryParse("AL_01_A1/D/N/7/9/2/MB/MM/US", out var ordered) &&
              unordered.ToString() == ordered.ToString() && RideCode.TryParse("AL_01_A1/D/O/2/5/1/MB/ACP/US", out var freeRider) &&
              freeRider.ToString() == $"AL_01_A1/D/O/2/{Loadout.LayoutSeed(1)}/{Loadout.RoutePath(1)}/US",
            "ride code: one code per ride (modifier order, MB with bike 7 only, no changed checkpoints in Free Rider)");
        Check(!RideCode.TryParse("IS_01_A1/D/N/8/1/1", out _), "ride code: bike 8 refused");
        Check(RideCode.ParseAll("x AL_01_A1/D/N/1/5/1, junk\nAU_01_A2/N/N/2/6/2/ZZ").Count == 2, "ride codes in a text");

        var text = "LMD Custom Rides playlist: " + new string('n', 50) + "\nAL_01_A1/D/N/1/5/1\nAL_01_A1/D/N/1/5/1\nAU_01_A2/D/N/1/5/1";
        var playlist = Playlist.Parse(text, "fallback");
        Check(playlist != null && playlist.Name.Length == Playlist.MaxName && playlist.Rides.Count == 3, "playlist text: name cut to the field's length");
        Check(Playlist.Parse("no codes here", "x") == null && Playlist.Parse("LMD Custom Rides playlist: E", "x", true)?.Rides.Count == 0,
            "playlist text: empty only as a file");

        var keys = Records.RideKeys(playlist);
        Check(keys[0] == $"AL_01_A1/D/N/1/{Loadout.LayoutSeed(1)}/{Loadout.RoutePath(1)}" && keys[1] == keys[0] + "#2", "ride keys number a ride that is in twice");
        var reordered = new Playlist { Name = "r" };
        reordered.Rides.AddRange(playlist.Rides.AsEnumerable().Reverse());
        Check(Records.Signature(reordered) == Records.Signature(playlist), "signature ignores the order");
        reordered.Rides.RemoveAt(0);
        Check(Records.Signature(reordered) != Records.Signature(playlist), "signature follows the rides");

        RideCode.TryParse("AL_01_A1/D/O/2/5/1/OF", out var free);
        RideCode.TryParse("IS_01_A1/D/N/7/1/1/MB/MM/ACP", out var edit);
        var merged = RideFields.Merge(free, edit, new[] { RideField.Checkpoints, RideField.Bike });
        Check(merged.Route == free.Route && merged.Bike == 7 && merged.Has(Loadout.SuperBikeId) && !merged.Has(Loadout.ChangedCheckpointsId) &&
              merged.Has(Loadout.FewObstaclesId) && !merged.Has(Loadout.MirrorId),
            "edit all: bike brings the Super Bike tag, Free Rider keeps the standard checkpoints, the rest stays");

        var damaged = Path.Combine(Path.GetTempPath(), "LMDCustomRides-selfcheck.json");
        File.WriteAllText(damaged, "[{ \"Signature\": ");
        var read = Records.Read<List<PlaylistRecord>>(damaged);
        var aside = Directory.GetFiles(Path.GetTempPath(), "LMDCustomRides-selfcheck.json.*.bad");
        Check(read == null && aside.Length > 0, "a damaged file reads as nothing and is kept aside");
        foreach (var file in aside.Append(damaged))
        {
            File.Delete(file);
        }

        return failed.Count == 0 ? $"selfcheck: all {count} passed" : "selfcheck FAILED: " + string.Join("; ", failed);
    }
}
