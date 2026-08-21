using HarmonyLib;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;

namespace osu_replay_renderer_netcore.Patching;

public class CosmeticPatcher : PatcherBase
{
    public override string PatcherId() => "osureplayrenderer.Cosmetic";

    public override void DoPatching()
    {
        Harmony = new Harmony(PatcherId());

        // Scrolling "Watching ..." text on the replay overlay
        var setMessage = typeof(ReplayOverlay).GetMethod(nameof(ReplayOverlay.SetMessage));
        Harmony.Patch(setMessage, prefix: new HarmonyMethod(AccessTools.Method(typeof(CosmeticPatcher), nameof(SkipOriginal))));

        // "Loading paused..." flash in the beatmap metadata display during intro
        var setUserBlocked = AccessTools.PropertySetter(typeof(BeatmapMetadataDisplay), nameof(BeatmapMetadataDisplay.UserBlocked));
        Harmony.Patch(setUserBlocked, prefix: new HarmonyMethod(AccessTools.Method(typeof(CosmeticPatcher), nameof(SkipOriginal))));
    }

    private static bool SkipOriginal() => false;
}
