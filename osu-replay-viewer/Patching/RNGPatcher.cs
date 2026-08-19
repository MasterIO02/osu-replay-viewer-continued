using HarmonyLib;
using osu.Framework.Utils;
using System;

namespace osu_replay_renderer_netcore.Patching;

public class RNGPatcher : PatcherBase
{
    private static readonly Random deterministic = new Random(0);

    public override string PatcherId() => "osureplayrenderer.RNG";

    [HarmonyPatch(typeof(RNG), "Next", new Type[] { })]
    class PatchNext
    {
        static bool Prefix(ref int __result)
        {
            __result = deterministic.Next();
            return false;
        }
    }

    [HarmonyPatch(typeof(RNG), "Next", new[] { typeof(int) })]
    class PatchNextMax
    {
        static bool Prefix(ref int __result, int maxValue)
        {
            __result = deterministic.Next(maxValue);
            return false;
        }
    }

    [HarmonyPatch(typeof(RNG), "Next", new[] { typeof(int), typeof(int) })]
    class PatchNextRange
    {
        static bool Prefix(ref int __result, int minValue, int maxValue)
        {
            __result = deterministic.Next(minValue, maxValue);
            return false;
        }
    }

    [HarmonyPatch(typeof(RNG), "NextDouble", new Type[] { })]
    class PatchNextDouble
    {
        static bool Prefix(ref double __result)
        {
            __result = deterministic.NextDouble();
            return false;
        }
    }

    [HarmonyPatch(typeof(RNG), "NextBytes", new[] { typeof(byte[]) })]
    class PatchNextBytes
    {
        static bool Prefix(byte[] buffer)
        {
            deterministic.NextBytes(buffer);
            return false;
        }
    }
}
