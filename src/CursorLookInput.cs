using EFT.InputSystem;
using UnityEngine;

namespace LookyLoot;

/// <summary>
/// Converts a cursor-position delta into a look axis value using the player's bound
/// free-look key and mouse axis direction/sensitivity. Read-only: never mutates bindings.
/// </summary>
internal static class CursorLookInput
{
    internal static bool IsLookHeld(InputPreset preset)
    {
        foreach (var combination in preset._workingAxisCombinations ?? new InputAxisCombination[0])
        {
            if ((combination.GameAxis == EAxis.LookX || combination.GameAxis == EAxis.LookY) && IsFreeLookHeld(combination))
                return true;
        }
        return false;
    }

    internal static float Read(InputPreset preset, EAxis gameAxis, Vector2 delta, float scale)
    {
        foreach (var combination in preset._workingAxisCombinations ?? new InputAxisCombination[0])
        {
            if (combination.GameAxis != gameAxis || !IsFreeLookHeld(combination))
                continue;
            foreach (var side in combination.axises)
            {
                if (side.Key != InputAxisCombination.EUseSide.Positive || side.Value == null)
                    continue;
                foreach (var key in side.Value)
                {
                    if (key is InputAxis { _axisName: "Mouse X" or "Mouse Y" } axis)
                        return (axis._axisName == "Mouse X" ? delta.x : delta.y) * scale * axis._sign * axis._sensitivity;
                }
            }
        }
        return 0f;
    }

    // The free-look binding: Nidnod modifier (MMB by default, rebindable) plus any common keys.
    private static bool IsFreeLookHeld(InputAxisCombination combination)
        => combination.Modificator == EGameKey.Nidnod && combination.modificatorKeys.Length > 0 &&
           AllPressed(combination.modificatorKeys) && AllPressed(combination.commonKeys);

    private static bool AllPressed(IGameKey[] keys)
    {
        foreach (var key in keys)
        {
            if (key is not InputKey || key.GetValue() == 0f)
                return false;
        }
        return true;
    }
}
