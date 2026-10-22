namespace LMDCustomRides;

/// <summary>The parts of a ride that editing all rides of a playlist can set (the trail stays each ride's own).</summary>
internal enum RideField
{
    Bike,
    TimeOfDay,
    Mode,
    Obstacles,
    Layout,
    Checkpoints,
    Route,
    Mirrored,
    Sprinting,
    Flying,
}

/// <summary>Editing all rides at once: where rides differ, what they start from, and the edit merged into each ride.</summary>
internal static class RideFields
{
    /// <summary>A ride's value for a field, to compare rides; null where it does not apply (a layout without obstacles).</summary>
    private static string Value(RideCode code, RideField field) => field switch
    {
        RideField.Bike => code.Bike.ToString(),
        RideField.TimeOfDay => code.Night ? "N" : "D",
        RideField.Mode => code.Mode.ToString(),
        RideField.Obstacles => code.Has(Loadout.ManyObstaclesId) ? "M" : code.Has(Loadout.FewObstaclesId) ? "F" : "",
        RideField.Layout => HasObstacles(code) ? code.Seed.ToString() : null,
        RideField.Checkpoints => code.Has(Loadout.ChangedCheckpointsId) ? "1" : "0",
        RideField.Route => code.Has(Loadout.ChangedCheckpointsId) ? code.Path : null,
        RideField.Mirrored => code.Has(Loadout.MirrorId) ? "1" : "0",
        RideField.Sprinting => code.Has(Loadout.UnlimitedSprintingId) ? "1" : "0",
        RideField.Flying => code.Has(Loadout.BetterFlyingControlId) ? "1" : "0",
        _ => null,
    };

    private static bool HasObstacles(RideCode code) => code.Has(Loadout.FewObstaclesId) || code.Has(Loadout.ManyObstaclesId);

    /// <summary>True when the rides have different values for the field.</summary>
    public static bool Differ(IEnumerable<RideCode> rides, RideField field) =>
        rides.Select(r => Value(r, field)).Where(v => v != null).Distinct().Skip(1).Any();

    /// <summary>What the edit starts from: the first ride, with the first layout and route that are in use.</summary>
    public static RideCode Base(IReadOnlyList<RideCode> rides)
    {
        var first = rides[0];
        return new RideCode
        {
            Route = first.Route,
            Night = first.Night,
            Mode = first.Mode,
            Bike = first.Bike,
            Seed = rides.FirstOrDefault(HasObstacles)?.Seed ?? first.Seed,
            Path = rides.FirstOrDefault(r => r.Has(Loadout.ChangedCheckpointsId))?.Path ?? first.Path,
            Modifiers = first.Modifiers,
        };
    }

    /// <summary>
    /// A ride with the changed fields taken from the edit, the rest (its trail first) kept. <see cref="RideCode.Normalized"/>
    /// puts the modifiers in order, gives the Super Bike its "MB" and keeps Free Rider on the standard checkpoints.
    /// </summary>
    public static RideCode Merge(RideCode ride, RideCode edit, ICollection<RideField> changed)
    {
        RideCode From(RideField field) => changed.Contains(field) ? edit : ride;

        var obstacles = From(RideField.Obstacles);
        var mods = new[]
        {
            (RideField.Mirrored, Loadout.MirrorId),
            (RideField.Checkpoints, Loadout.ChangedCheckpointsId),
            (RideField.Sprinting, Loadout.UnlimitedSprintingId),
            (RideField.Flying, Loadout.BetterFlyingControlId),
        }.Where(m => From(m.Item1).Has(m.Item2)).Select(m => m.Item2).ToList();
        if (obstacles.Has(Loadout.FewObstaclesId) || obstacles.Has(Loadout.ManyObstaclesId))
        {
            mods.Add(obstacles.Has(Loadout.FewObstaclesId) ? Loadout.FewObstaclesId : Loadout.ManyObstaclesId);
        }

        return new RideCode
        {
            Route = ride.Route,
            Night = From(RideField.TimeOfDay).Night,
            Mode = From(RideField.Mode).Mode,
            Bike = From(RideField.Bike).Bike,
            Seed = From(RideField.Layout).Seed,
            Path = From(RideField.Route).Path,
            Modifiers = mods,
        }.Normalized();
    }
}
