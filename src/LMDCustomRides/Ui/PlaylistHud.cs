using LMDCustomRides.Game;
using LMDCustomRides.Ui.Kit;

namespace LMDCustomRides.Ui;

/// <summary>
/// During a speedrun (Speedrun Mode, Playlists page), the ride HUD's run panel: "RUN 3/16", the run clock and the
/// difference to the speedrun PB at the last finish line; past a finish line, "RIDE 3 DONE" with the ride's time for a moment.
/// </summary>
internal static class PlaylistHud
{
    private static HudPanel _panel;

    public static void Update()
    {
        if (_panel == null || !_panel.Alive)
        {
            _panel = HudPanel.Create(CustomTab.GameMenu?.transform.parent);
            if (_panel == null)
            {
                return;
            }
        }

        var playlist = PlaylistRunner.Active;
        var clock = PlaylistRunner.Clock;
        if (!PlaylistRunner.Speedrun || playlist == null || clock == null || PlaylistRunner.State is not (RunState.Loading or RunState.InControl or RunState.Finishing))
        {
            _panel.Show(null, null, null);
            return;
        }

        if (PlaylistRunner.State == RunState.Finishing)
        {
            _panel.Show(string.Format(Texts.Get("LMDMOD_HUDRIDEDONE"), PlaylistRunner.Index + 1), Times.Format(PlaylistRunner.LastRideMs), PlaylistRunner.Delta);
        }
        else
        {
            _panel.Show(string.Format(Texts.Get("LMDMOD_HUDRUN"), PlaylistRunner.Index + 1, playlist.Rides.Count), Times.Format(clock.GameMs), PlaylistRunner.Delta);
        }
    }
}
