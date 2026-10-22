using HarmonyLib;
using Il2CppMegagon.Downhill;
using Il2CppMegagon.Downhill.Managers;

namespace LMDCustomRides.Game;

/// <summary>
/// Text for the UI. Modifier, bike, mode and trail names come from the game's own localization (so they follow the
/// game language); the few lines the game has no text for are this mod's, in English, under keys starting with
/// LMDMOD_. Those are added to the game's loaded text table, so the game's own widgets (top bar, headers) show them.
/// </summary>
internal static class Texts
{
    private const string Probe = "MODIFIER_MIRRORMODE";

    private static readonly Dictionary<string, string> Own = new()
    {
        // the tab and its pages
        [CustomTab.TitleKey] = "Custom Rides",
        [CustomTab.NewRideKey] = "New Ride",
        [CustomTab.PlaylistsKey] = "Playlists",
        ["LMDMOD_EMPTY"] = " ",

        // bottom bar actions ({0} = the key)
        [CustomTab.ActionStartPlaylistKey] = "{0} Start Playlist",
        [CustomTab.ActionRideAgainKey] = "{0} Ride Again",
        [CustomTab.ActionCreatePlaylistKey] = "{0} Create Playlist",
        ["LMDMOD_ACTION_DROPRIDE"] = "{0} Drop Ride",
        ["LMDMOD_ACTION_STARTRIDE"] = "{0} Start Ride",
        ["LMDMOD_ACTION_SAVERIDE"] = "{0} Save Ride",
        ["LMDMOD_ACTION_SAVEALL"] = "{0} Save All Rides",
        ["LMDMOD_ACTION_DISCARD"] = "{0} Discard",
        ["LMDMOD_ACTION_CONFIRM"] = "{0} Confirm",
        ["LMDMOD_ACTION_ADD"] = "{0} Add",
        ["LMDMOD_ACTION_DELETE"] = "{0} Delete",
        ["LMDMOD_ACTION_BACK"] = "{0} Back",
        ["LMDMOD_ACTION_CREATE"] = "{0} Create",

        // hints
        ["LMDMOD_HINT_ADDTOPLAYLIST"] = "{0} Add to Playlist",
        ["LMDMOD_HINT_REMOVERIDE"] = "{0} Remove",
        ["LMDMOD_HINT_DELETEPLAYLIST"] = "{0} Delete",
        ["LMDMOD_HINT_MOVERIDE"] = "{0} Move",
        ["LMDMOD_HINT_DROPRIDE"] = "{0} Drop",
        ["LMDMOD_HINT_EDITRIDE"] = "{0} Edit Ride",
        ["LMDMOD_HINT_TOGGLE"] = "{0} Toggle",
        ["LMDMOD_HINT_EDITALL"] = "{0} Edit All Rides",
        ["LMDMOD_RENAME"] = "Rename",
        ["LMDMOD_BACKTOPLAYLIST"] = "{0} Back to Playlist",
        ["LMDMOD_HINT_CANCEL"] = "{0} Cancel",
        ["LMDMOD_HINT_RENAME"] = "{0} Rename",
        ["LMDMOD_HINT_SPEEDRUNCLOCK"] = "{0} Speedrun Mode",

        // New Ride rows
        ["LMDMOD_MOUNTAIN"] = "Mountain",
        ["LMDMOD_TRAIL"] = "Trail",
        ["LMDMOD_TIMEOFDAY"] = "Time of Day",
        ["LMDMOD_MODE"] = "Mode",
        ["LMDMOD_BIKE"] = "Bike",
        ["LMDMOD_MODIFIERS"] = "Modifiers",
        ["LMDMOD_MIRRORED"] = "Mirrored Mode",
        ["LMDMOD_OBSTACLES"] = "Obstacles",
        ["LMDMOD_NONE"] = "None",
        ["LMDMOD_FEW"] = "Few",
        ["LMDMOD_MANY"] = "Many",
        ["LMDMOD_LAYOUT"] = "Layout",
        ["LMDMOD_CHANGEDCHECKPOINTS"] = "Changed Checkpoints",
        ["LMDMOD_ROUTE"] = "Checkpoint Route",
        ["LMDMOD_FROMCODE"] = "Pasted",
        ["LMDMOD_UNLIMITEDSPRINTING"] = "Unlimited Sprinting",
        ["LMDMOD_BETTERFLYING"] = "Better Flying Control",

        // Playlists page
        ["LMDMOD_NEWPLAYLIST"] = "+ New Playlist",
        ["LMDMOD_SPEEDRUNCLOCK"] = "Speedrun Mode",
        ["LMDMOD_SUMOFBESTTITLE"] = "Sum of best",
        ["LMDMOD_BESTSECTIONSLINE"] = "Best sections add up to {0}",
        ["LMDMOD_BESTSPLITSLINE"] = "Best splits add up to {0}",
        ["LMDMOD_SPEEDRUNPB"] = "Speedrun PB",
        ["LMDMOD_RIDESWITHBEST"] = "{0}/{1} rides with a best time",
        ["LMDMOD_NEWRIDEBEST"] = "1 new best time!",
        ["LMDMOD_NEWRIDEBESTS"] = "{0} new best times!",
        ["LMDMOD_NONEWRIDEBEST"] = "No new best time",
        ["LMDMOD_NEWPLAYLISTINFO"] = "Create a new playlist, then add rides from New Ride",
        ["LMDMOD_ONERIDE"] = "{0} ride",
        ["LMDMOD_NRIDES"] = "{0} rides",
        ["LMDMOD_PLAYLISTOF"] = "Playlist {0} / {1}",
        ["LMDMOD_NEWPLAYLISTSUB"] = "Make your own",
        ["LMDMOD_NOFINISH"] = "No finished run yet",
        ["LMDMOD_REALLINE"] = "Real time {0}",
        ["LMDMOD_RUNSLINE"] = "{0} runs  ·  {1} finished",
        ["LMDMOD_ONERUNLINE"] = "{0} run  ·  {1} finished",
        ["LMDMOD_PREVBEST"] = "Previous best {0}",
        ["LMDMOD_BESTWAS"] = "Speedrun PB {0}",
        ["LMDMOD_COLNUMBER"] = "#",
        ["LMDMOD_COLMODE"] = "Mode",
        ["LMDMOD_COLTRAIL"] = "Trail",
        ["LMDMOD_COLBEST"] = "Best Time",
        ["LMDMOD_COLPBSPLIT"] = "PB Split",
        ["LMDMOD_COLRUN"] = "Run",
        ["LMDMOD_COLDELTA"] = "+/-",
        ["LMDMOD_PAUSED"] = "paused {0}",
        ["LMDMOD_NEWPLAYLISTTITLE"] = "New Playlist",
        ["LMDMOD_NAMETAKENUSED"] = "That name is taken: created \"{0}\"",
        ["LMDMOD_LASTDONE"] = "Last run: finished in {0}  ·  {1}",
        ["LMDMOD_LASTLEFT"] = "Last run: left at ride {0}  ·  {1}",
        ["LMDMOD_NEWBEST"] = "New speedrun PB!",
        ["LMDMOD_THISRUN"] = "This run",
        ["LMDMOD_EMPTYPLAYLIST"] = "No rides yet: add some from New Ride",
        ["LMDMOD_CREATED"] = "Created \"{0}\"",
        ["LMDMOD_RENAMED"] = "Renamed to \"{0}\"",
        ["LMDMOD_NAMETAKEN"] = "That name is already used",
        ["LMDMOD_NOTSAVED"] = "The playlist could not be saved under that name",
        ["LMDMOD_REMOVED"] = "Removed from the playlist",
        ["LMDMOD_REMOVETITLE"] = "Remove Ride?",
        ["LMDMOD_REMOVEMESSAGE"] = "{0} will be removed.",
        ["LMDMOD_ACTION_REMOVE"] = "{0} Remove",
        ["LMDMOD_REMOVEBESTMESSAGE"] = "{0} will be removed.\nYour speedrun PB of {2}\nwill start over.",
        ["LMDMOD_CHANGETITLE"] = "Change Rides?",
        ["LMDMOD_CHANGEMESSAGE"] = "Your speedrun PB of {1}\nwill start over.",
        ["LMDMOD_ACTION_CHANGE"] = "{0} Change",
        ["LMDMOD_DELETETITLE"] = "Delete Playlist?",
        ["LMDMOD_DELETEMESSAGE"] = "\"{0}\" will be deleted\nalong with its {1}.",
        ["LMDMOD_DELETEEMPTYMESSAGE"] = "\"{0}\" will be deleted.",
        ["LMDMOD_DELETED"] = "Playlist deleted",
        ["LMDMOD_PLAYLISTCOPIED"] = "Playlist copied to the clipboard ({0})",
        ["LMDMOD_PLAYLISTPASTEDAS"] = "Pasted as \"{0}\" ({1})",

        // Result screen
        ["LMDMOD_PLUSMINUS"] = "+/-",
        ["LMDMOD_NEXTRIDE"] = "Next Ride ({0}/{1})",
        ["LMDMOD_RUNRESULTS"] = "Run Results",

        // adding rides
        ["LMDMOD_ADDTOPLAYLIST"] = "Add to Playlist",
        ["LMDMOD_ADDED"] = "Added to \"{0}\" ({1}/{2})",
        ["LMDMOD_FULL"] = "The playlist is full ({0} rides)",
        ["LMDMOD_DISCARDTITLE"] = "Discard Changes?",
        ["LMDMOD_DISCARDMESSAGE"] = "The changes to this ride will be lost.",
        ["LMDMOD_DISCARDALLMESSAGE"] = "The changes to the rides will be lost.",
        ["LMDMOD_MIXED"] = "Mixed",
        ["LMDMOD_ALLSAVED"] = "{0} changed",

        // ride codes
        ["LMDMOD_COPIED"] = "Ride code copied to the clipboard",
        ["LMDMOD_PASTED"] = "Ride code applied",
        ["LMDMOD_NOCODE"] = "No ride code in the clipboard",
        ["LMDMOD_DAILYCOPIED"] = "Today's ride code copied",
        ["LMDMOD_NOTAVAILABLE"] = "Trail not available",
        ["LMDMOD_NEEDSDLC"] = "Needs the DLC: {0}",

        // the run HUD
        ["LMDMOD_HUDRUN"] = "Run {0}/{1}",
        ["LMDMOD_HUDRIDEDONE"] = "Ride {0} done",
    };

    // Numbered values of the setup rows: obstacle layouts and checkpoint routes "#12".
    public const string LayoutKeyPrefix = "LMDMOD_L_";

    private static string Dynamic(string key)
    {
        if (key.StartsWith(LayoutKeyPrefix, StringComparison.Ordinal))
        {
            return "#" + key.Substring(LayoutKeyPrefix.Length);
        }

        if (key.StartsWith(TrailKeyPrefix, StringComparison.Ordinal))
        {
            return CustomRides.TrailName(key.Substring(TrailKeyPrefix.Length));
        }

        return null;
    }

    // A trail's name ("Trail 2") as a value of the New Ride page's Trail row.
    private const string TrailKeyPrefix = "LMDMOD_T_";

    /// <summary>The key showing a trail's name; also put in the game's text table for widgets that read it directly.</summary>
    public static string TrailKey(string route) => Publish(TrailKeyPrefix + route, CustomRides.TrailName(route));

    private static string Publish(string key, string text)
    {
        try
        {
            var manager = Manager<LocalizationManager>.prop_T_0;
            // Button labels look their key up in upper case.
            foreach (var table in new[] { manager?.field_Private_Dictionary_2_String_String_0, manager?.field_Private_Dictionary_2_String_String_1 })
            {
                foreach (var k in new[] { key, key.ToUpperInvariant() })
                {
                    if (table != null && !table.ContainsKey(k))
                    {
                        table[k] = text;
                    }
                }
            }
        }
        catch
        {
            // not loaded yet: the lookup patch still answers
        }

        return key;
    }

    public static string Get(string key)
    {
        if (Own.TryGetValue(key, out var own))
        {
            return own;
        }

        var dynamic = Dynamic(key);
        if (dynamic != null)
        {
            return dynamic;
        }

        try
        {
            return LocalizationManager.Method_Public_Static_String_String_0(key);
        }
        catch
        {
            return key;
        }
    }

    /// <summary>"1 ride", "16 rides".</summary>
    public static string Rides(int count) => string.Format(Get(count == 1 ? "LMDMOD_ONERIDE" : "LMDMOD_NRIDES"), count);

    public static string ModeName(RideMode mode) => Get(mode == RideMode.FreeRider ? "GENERIC_MODE_ONELIFE" : "GENERIC_MODE_NORMAL");

    /// <summary>
    /// Per frame (cheap): adds this mod's lines to the game's text table once it is loaded, and again after the
    /// game reloads it for another language.
    /// </summary>
    public static void Update()
    {
        LocalizationManager manager;
        try
        {
            manager = Manager<LocalizationManager>.prop_T_0;
        }
        catch
        {
            return;
        }

        if (manager == null)
        {
            return;
        }

        Inject(manager.field_Private_Dictionary_2_String_String_0);
        Inject(manager.field_Private_Dictionary_2_String_String_1);
    }

    private static void Inject(Il2CppSystem.Collections.Generic.Dictionary<string, string> table)
    {
        if (table == null || !table.ContainsKey(Probe) || table.ContainsKey(CustomTab.TitleKey))
        {
            return;
        }

        foreach (var (key, text) in Own)
        {
            table[key] = text;
        }

        // The numbered values, for widgets that read the table directly.
        for (var i = 1; i <= Loadout.MaxLayout; i++)
        {
            table[LayoutKeyPrefix + i] = "#" + i;
        }
    }

    /// <summary>Lookups that go straight to this method (not through the table) get the mod's lines too.</summary>
    [HarmonyPatch(typeof(LocalizationManager), nameof(LocalizationManager.Method_Public_Static_String_String_0))]
    private static class Lookup
    {
        private static bool Prefix(string __0, ref string __result)
        {
            if (__0 == null || !__0.StartsWith("LMDMOD_", StringComparison.Ordinal))
            {
                return true;
            }

            var text = Own.TryGetValue(__0, out var own) ? own : Dynamic(__0);
            if (text == null)
            {
                return true;
            }

            __result = text;
            return false;
        }
    }
}
