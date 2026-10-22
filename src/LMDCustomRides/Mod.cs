using LMDCustomRides.Game;
using LMDCustomRides.Ui;
using LMDCustomRides.Ui.Kit;
using LMDCustomRides.Ui.Pages;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(LMDCustomRides.Mod), "LMD Custom Rides", LMDCustomRides.Mod.Version, "LMD Custom Rides")]
[assembly: MelonGame("Megagon Industries", "Lonely Mountains: Downhill")]

namespace LMDCustomRides;

public class Mod : MelonMod
{
    public const string Version = "1.0.0";

    internal static MelonLogger.Instance Log { get; private set; }

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        Settings.Init();
        PlaylistStore.Load();
        Records.Load();
        Log.Msg("Custom ride setup: " + Loadout.FromSettings(false) + " (UserData\\LMDCustomRides.cfg).");
    }

    public override void OnUpdate()
    {
        Texts.Update();
        CustomSession.Update();
        SuperBike.Update();
        Fade.Update();
        TextField.Tick();
        PlaylistRunner.Update();
        Page.UpdateAll();
        DailyHint.Update();
        PlaylistHud.Update();

        if (Input.GetKeyDown(KeyCode.F11))
        {
            Settings.Reload();
            Log.Msg("Settings reloaded: " + Loadout.FromSettings(GameState.SuperBikeSelected));
        }
    }

    public override void OnLateUpdate()
    {
        RideHud.LateUpdate();
        RideResults.LateUpdate();
        Page.LateUpdateAll();
    }
}
