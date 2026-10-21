using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using EFT.Settings;
using EFT.UI;
using EFT.UI.Screens;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace LookyLoot;

/// <summary>
/// Replays the vanilla free-look input (Look + ResetLookDirection) for the main player
/// while an in-raid inventory/loot screen is open, which vanilla input routing blocks.
/// </summary>
internal sealed class LookInjectPatch : ModulePatch
{
    private static int _lastFrame = -1;
    private static Vector2 _lastCursor;
    private static Vector2Int _lastScreen;
    private static CursorLockMode _lastMode;

    protected override MethodBase GetTargetMethod()
        => AccessTools.Method(typeof(InputPreset), nameof(InputPreset.UpdateInput));

    // UpdateInput(List<ECommand> commands, float[] axis, float deltaTime)
    [PatchPostfix]
    private static void Postfix(InputPreset __instance, List<ECommand> commands, float[] axis)
    {
        if (!Plugin.Operational)
        {
            _lastFrame = -1;
            return;
        }

        try
        {
            if (EftScreenManager.Instance?.CurrentBaseScreenController
                is not InventoryScreen.RaidInventoryScreenController controller)
            {
                _lastFrame = -1;
                PeekSession.Stop();
                return;
            }

            var player = Singleton<GameWorld>.Instance?.MainPlayer;
            if (player == null || player.HealthController is not { IsAlive: true })
            {
                _lastFrame = -1;
                PeekSession.Stop(resetLook: true);
                return;
            }
            if (PeekSession.Begin(controller, player))
                _lastFrame = -1;

            bool continuous = SampleCursor(out Vector2 delta);
            bool wasHeld = PeekSession.Held;
            PeekSession.Held = Application.isFocused && CursorLookInput.IsLookHeld(__instance);

            // Commands precede Look. Clear the latch if focus/ownership interrupts a hold.
            if (commands.Contains(ECommand.ResetLookDirection) || (wasHeld && !PeekSession.Held))
                player.ResetLookDirection();

            float lookX = 0f, lookY = 0f;
            if (PeekSession.Held)
            {
                lookX = axis[(int)EAxis.LookX];
                lookY = axis[(int)EAxis.LookY];
                // Unity's raw mouse axes can go silent with an unlocked cursor. Only fill
                // missing axes from cursor movement; never add on top of native input.
                if (continuous)
                {
                    float scale = Plugin.CursorDeltaScale.Value;
                    if (lookX == 0f)
                        lookX = CursorLookInput.Read(__instance, EAxis.LookX, delta, scale);
                    if (lookY == 0f)
                        lookY = CursorLookInput.Read(__instance, EAxis.LookY, delta, scale);
                }
            }

            var s = Singleton<SettingsManager>.Instance.Control.Settings;
            float x = (s.InvertedXAxis.Value ? -1f : 1f) * lookX;
            float y = (s.InvertedYAxis.Value ? -1f : 1f) * lookY;

            // Player.Look(deltaLookY, deltaLookX) takes (LookX, LookY) — same argument
            // order MoveInputTranslator uses; the param names are misleading, do not "fix".
            // Called every frame (zero axes included) so the vanilla return-to-centre lerp runs.
            player.Look(x, y);
        }
        catch (Exception e)
        {
            Plugin.FailOnce(e);
        }
    }

    /// <summary>Cursor movement since the previous frame; false if there is no continuous sample.</summary>
    private static bool SampleCursor(out Vector2 delta)
    {
        delta = Vector2.zero;
        Vector2 cursor = Input.mousePosition;
        var screen = new Vector2Int(Screen.width, Screen.height);
        var mode = Cursor.lockState;
        int frame = Time.frameCount;

        if (!Application.isFocused || mode == CursorLockMode.Locked ||
            !new Rect(0f, 0f, screen.x, screen.y).Contains(cursor))
        {
            _lastFrame = -1;
            return false;
        }

        bool continuous = _lastFrame >= 0 && frame == _lastFrame + 1 &&
                          mode == _lastMode && screen == _lastScreen;
        if (continuous)
            delta = cursor - _lastCursor;
        _lastFrame = frame;
        _lastCursor = cursor;
        _lastScreen = screen;
        _lastMode = mode;
        return continuous;
    }
}
