using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppMegagon.CommonInterfaces.Leaderboards;
using Il2CppMegagon.PlatformIntegration.Standalone;
using Il2CppMegagon.PlatformIntegration.Standalone.PcXbl;
using Il2CppMegagon.Platforms.LeaderboardRequests;
using Il2CppInterop.Runtime;
using Task = Il2CppSystem.Threading.Tasks.Task;

namespace LMDCustomRides.Game;

/// <summary>
/// Keeps custom rides out of the game's records: no section or finish times reach the leaderboards (Megagon's
/// server), statistics-based best times or progression (challenges, unlocks). A ride counts as custom when it was
/// started from the Custom Rides tab (always: no Daily Ride starts from there), or modifiers are active or the Super
/// Bike is ridden outside the game's own Daily Ride. The times still show in the HUD and on the result screen.
/// </summary>
internal static class RideGuard
{
    public static bool ModifiedRide =>
        CustomSession.Active || (!GameState.DailyRideActive && (GameState.ActiveModifierCount > 0 || GameState.SuperBikeSelected));

    /// <summary>
    /// The run tracker's per-checkpoint submit (best time check, online upload, rank tier). For custom rides only the
    /// values the HUD and the result screen read are kept, the way the game keeps them: the sections passed (the
    /// HUD's total clock adds up that many section times) and the last one's time, and the run's section times in
    /// milliseconds (index 0 is the total, sections from 1).
    /// </summary>
    [HarmonyPatch(typeof(ObjectPublicBoGaObInSiStObDi2LiUnique), nameof(ObjectPublicBoGaObInSiStObDi2LiUnique.Method_Public_Void_Int32_Single_PDM_0))]
    private static class SectionTime
    {
        private static bool Prefix(ObjectPublicBoGaObInSiStObDi2LiUnique __instance, int __0, float __1)
        {
            if (!ModifiedRide)
            {
                return true;
            }

            var index = __0 + 1;
            __instance.field_Private_Int32_0 = index;
            __instance.field_Private_Single_0 = __1;
            __instance.field_Private_EnumPublicSealedva7v0_0 = default;
            var times = __instance.field_Private_List_1_UInt32_0;
            var ms = (uint)Math.Max(0, (int)Math.Ceiling(__1 * 1000f));
            if (__0 >= 0 && times != null && index < times.Count && times[index] > ms)
            {
                times[index] = ms;
            }

            Mod.Log.Msg($"custom ride: section {__0} time {__1:0.00} s, not uploaded");
            RideHud.SectionPassed(__0, ms);
            return false;
        }
    }

    /// <summary>The run tracker's finish: total time, final upload, statistics and progression (challenges, unlocks).</summary>
    [HarmonyPatch(typeof(ObjectPublicBoGaObInSiStObDi2LiUnique), nameof(ObjectPublicBoGaObInSiStObDi2LiUnique.Method_Public_Void_PDM_0))]
    private static class Finish
    {
        private static bool Prefix(ObjectPublicBoGaObInSiStObDi2LiUnique __instance)
        {
            if (!ModifiedRide)
            {
                return true;
            }

            // Like the game: the total is the sum of the section times, stored at index 0.
            var times = __instance.field_Private_List_1_UInt32_0;
            var sections = __instance.field_Private_Dictionary_2_String_Int32_0?.Count ?? 0;
            ulong total = 0;
            if (times != null && times.Count > 0)
            {
                for (var i = 1; i < sections && i < times.Count; i++)
                {
                    total += times[i];
                }

                times[0] = (uint)Math.Min(total, uint.MaxValue);
                Mod.Log.Msg($"custom ride finished in {total / 1000.0:0.00} s, not uploaded");
            }

            RideHud.Finished((long)total);
            PlaylistRunner.RideFinished((long)total);

            // The flags the game sets for a finished, valid run; the result screen shows its time overview only then.
            var valid = !__instance.field_Private_Boolean_3 && !__instance.field_Private_Boolean_4;
            __instance.field_Private_Boolean_1 = valid;
            __instance.prop_Boolean_0 = true;
            return false;
        }
    }

    /// <summary>
    /// Safety net at the network layer: whatever path reaches it, a score upload during a modified ride is answered
    /// locally with an error and never sent. These are virtual calls, so unlike ScoreRequests.SetScores (inlined by
    /// the compiler) they cannot be bypassed.
    /// </summary>
    [HarmonyPatch]
    private static class Uploads
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(StandaloneLeaderboardConnection), nameof(StandaloneLeaderboardConnection.SendRequestAsync));
            yield return AccessTools.Method(typeof(MagentaLeaderboardConnection), nameof(MagentaLeaderboardConnection.SendRequestAsync));
            yield return AccessTools.Method(typeof(PcXblLeaderboardConnection), nameof(PcXblLeaderboardConnection.SendRequestAsync));
        }

        private static bool Prefix(ILeaderboardRequest __0, ref Il2CppSystem.Threading.Tasks.Task<LeaderboardResponse> __result)
        {
            if (__0 == null || !ModifiedRide || __0.TryCast<ScoreRequestSet>() == null)
            {
                return true;
            }

            Mod.Log.Warning("modified ride: blocked a score upload at the network layer");
            __result = Task.FromResult(new LeaderboardResponse(403, "LMD Custom Rides: modified rides are not uploaded"));
            return false;
        }
    }
}
