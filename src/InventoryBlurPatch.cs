using System;
using System.Reflection;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace LookyLoot;

/// <summary>
/// Scale only the inventory blur's render-time value. Never change its Enable/Disable
/// state, ramp, material asset, SSAA state, injury blur or other camera effects.
/// </summary>
internal sealed class InventoryBlurPatch : ModulePatch
{
    internal struct RenderState
    {
        internal float Original;
        internal bool Changed;
    }

    protected override MethodBase GetTargetMethod()
        => AccessTools.Method(typeof(InventoryBlur), nameof(InventoryBlur.OnRenderImage));

    [PatchPrefix]
    private static void Prefix(InventoryBlur __instance, out RenderState __state)
    {
        __state = default;
        try
        {
            float multiplier = PeekPresentation.BlurMultiplier(__instance);
            if (multiplier == 1f)
                return;
            __state.Original = __instance._value;
            __state.Changed = true;
            __instance._value *= multiplier;
        }
        catch (Exception e)
        {
            Plugin.FailOnce(e);
        }
    }

    // Restore even if the renderer or another patch throws; never suppress that exception.
    [PatchFinalizer]
    private static Exception? Finalizer(InventoryBlur __instance, RenderState __state, Exception? __exception)
    {
        if (__state.Changed && __instance != null)
            __instance._value = __state.Original;
        return __exception;
    }
}
