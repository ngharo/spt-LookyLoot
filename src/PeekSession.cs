using EFT;
using EFT.UI;
using EFT.UI.Screens;
using UnityEngine;

namespace LookyLoot;

internal static class PeekSession
{
    internal static bool Held { get; set; }
    internal static InventoryScreen.RaidInventoryScreenController? Controller { get; private set; }
    internal static Player? Player { get; private set; }

    internal static bool Valid => Plugin.Operational && Application.isFocused && Controller != null &&
        ReferenceEquals(EftScreenManager.Instance.CurrentBaseScreenController, Controller) &&
        Player != null && Player.HealthController is { IsAlive: true } &&
        Controller.Screen != null && Controller.Screen.gameObject.activeInHierarchy;

    internal static bool Begin(InventoryScreen.RaidInventoryScreenController controller, Player player)
    {
        if (ReferenceEquals(Controller, controller) && Player == player)
            return false;
        Stop();
        Controller = controller;
        Player = player;
        return true;
    }

    internal static void Stop(bool resetLook = false)
    {
        try
        {
            if (resetLook && Held && Player != null && Player.HealthController is { IsAlive: true } &&
                ReferenceEquals(EftScreenManager.Instance.CurrentBaseScreenController, Controller))
                Player.ResetLookDirection();
        }
        finally
        {
            Held = false;
            Controller = null;
            Player = null;
            PeekPresentation.Restore();
        }
    }
}
