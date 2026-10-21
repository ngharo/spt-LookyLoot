using System;
using System.Reflection;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace LookyLoot;

internal sealed class InventoryClosePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
        => AccessTools.Method(typeof(InventoryScreen), nameof(InventoryScreen.Close));

    [PatchPrefix]
    private static void Prefix(InventoryScreen __instance)
    {
        try
        {
            if (PeekSession.Controller?.Screen == __instance)
                PeekSession.Stop();
        }
        catch (Exception e)
        {
            Plugin.FailOnce(e);
        }
    }
}
