using HarmonyLib;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Audio;
using osu.Game.Skinning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using osu_replay_renderer_netcore.CustomHosts.CustomClocks;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Timing;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Rulesets.Osu.UI.Cursor;

namespace osu_replay_renderer_netcore.Patching
{
    public class RenderPatcher : PatcherBase
    {
        public override string PatcherId() => "osureplayrenderer.Render";

        public static bool BlockSwapBuffers { get; set; } = true;

        public override void DoPatching()
        {
            base.DoPatching();
            
            var veldridDeviceType = typeof(IRenderer).Assembly.GetType("osu.Framework.Graphics.Veldrid.VeldridDevice");
            var veldridSwapBuffersMethod = veldridDeviceType.GetMethod("SwapBuffers");
            Harmony.Patch(veldridSwapBuffersMethod, (Delegate)SwapBuffersPrefix);
            
            var glRendererType = typeof(IRenderer).Assembly.GetType("osu.Framework.Graphics.OpenGL.GLRenderer");
            var glSwapBuffersMethod = glRendererType.GetMethod("SwapBuffers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Harmony.Patch(glSwapBuffersMethod, (Delegate)SwapBuffersPrefix);
        }

        public static event Action OnDraw;
        private static void TriggerOnDraw() => OnDraw?.Invoke();

        public static event Action<IRenderer> OnPreSwap;
        private static void TriggerOnPreSwap(IRenderer renderer) => OnPreSwap?.Invoke(renderer);

        [HarmonyPatch(typeof(Renderer))]
        [HarmonyPatch("FinishFrame")]
        class PatchFramedClock
        {
            static void Prefix(Renderer __instance)
            {
                TriggerOnDraw();
            }

            static void Postfix(Renderer __instance)
            {
                TriggerOnPreSwap(__instance);
            }
        }

        static bool SwapBuffersPrefix(object __instance)
        {
            return !BlockSwapBuffers;
        }
    }
}
