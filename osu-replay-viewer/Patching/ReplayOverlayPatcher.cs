using HarmonyLib;
using osu.Game.Screens.Play.HUD;

namespace osu_replay_renderer_netcore.Patching;

public class ReplayOverlayPatcher : PatcherBase
{
    public override string PatcherId() => "osureplayrenderer.ReplayOverlay";

    public override void DoPatching()
    {
        Harmony = new Harmony(PatcherId());

        var setMessage = typeof(ReplayOverlay).GetMethod(nameof(ReplayOverlay.SetMessage));
        Harmony.Patch(setMessage, prefix: new HarmonyMethod(AccessTools.Method(typeof(ReplayOverlayPatcher), nameof(BlockMessage))));
    }

    private static bool BlockMessage() => false;
}
