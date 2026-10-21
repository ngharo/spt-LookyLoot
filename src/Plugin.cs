using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using SPT.Reflection.Patching;

namespace LookyLoot;

[BepInPlugin(GUID, Name, Version)]
[BepInDependency("com.SPT.core", "4.1.6")]
[BepInProcess("EscapeFromTarkov.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string GUID = "com.ngharo.lookyloot";
    public const string Name = "LookyLoot";
    public const string Version = "1.1.1";

    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<float> CursorDeltaScale = null!;
    internal static ConfigEntry<float> FullEffectAngle = null!;
    internal static ConfigEntry<float> PeekScale = null!;
    internal static ConfigEntry<float> PeekOpacity = null!;
    internal static ConfigEntry<float> PeekBlurStrength = null!;
    internal static ConfigEntry<float> ParallaxStrength = null!;

    internal static ManualLogSource Log = null!;

    private static bool _failed, _running;
    private readonly List<ModulePatch> _patches = new();

    internal static bool Operational => _running && !_failed && Enabled.Value;

    private void Awake()
    {
        Log = Logger;

        Enabled = Config.Bind("General", "Enabled", true,
            "Master switch. Lets you free-look while an in-raid inventory or loot screen is open.");
        CursorDeltaScale = Config.Bind("General", "CursorDeltaScale", 0.015f,
            new ConfigDescription("Cursor pixels to raw axis units, before the bound axis sensitivity/dead zone and invert settings. Unlocked cursor speed may differ from vanilla; tune this if needed.",
                new AcceptableValueRange<float>(0.001f, 1f), "Advanced"));
        FullEffectAngle = BindPeek("FullEffectAngle", 45f, 5f, 70f,
            "Head deflection in degrees at which presentation reaches full effect. Continuous gentle response from zero; no activation threshold or timer. Higher values make small pans less noticeable.");
        PeekScale = BindPeek("Scale", 0.35f, 0.25f, 1f, "Inventory scale at full head deflection.");
        PeekOpacity = BindPeek("Opacity", 0.65f, 0f, 1f, "Inventory opacity multiplier at full deflection; never affects the world/HUD.");
        PeekBlurStrength = BindPeek("WorldBlur", 0.25f, 0f, 1f, "Remaining inventory world blur at full deflection. Other camera effects are untouched.");
        ParallaxStrength = BindPeek("Parallax", 1.5f, 0f, 2f, "Inverse head-relative inventory displacement. At least half the panel area stays visible.");

        try
        {
            foreach (var patch in new ModulePatch[] { new LookInjectPatch(), new InventoryBlurPatch(), new InventoryClosePatch() })
            {
                patch.Enable();
                _patches.Add(patch);
            }
            _running = true;
            Log.LogInfo($"{Name} {Version} loaded");
        }
        catch (Exception e)
        {
            FailOnce(e);
            DisablePatches();
        }
    }

    private ConfigEntry<float> BindPeek(string key, float value, float min, float max, string description)
        => Config.Bind("Peek", key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

    private void OnEnable() => _running = true;

    private void OnDisable() => Shutdown();

    private void OnDestroy()
    {
        Shutdown();
        DisablePatches();
    }

    private void Shutdown()
    {
        _running = false;
        try { PeekSession.Stop(resetLook: true); }
        catch (Exception e) { FailOnce(e); }
    }

    private void DisablePatches()
    {
        foreach (var patch in _patches)
        {
            try { patch.Disable(); }
            catch (Exception e) { Log.LogWarning($"Patch cleanup failed: {e}"); }
        }
        _patches.Clear();
    }

    private void LateUpdate()
    {
        try
        {
            if (Operational)
                PeekPresentation.Tick();
            else
                PeekSession.Stop(resetLook: true);
        }
        catch (Exception e)
        {
            FailOnce(e);
        }
    }

    internal static void FailOnce(Exception e)
    {
        if (_failed)
            return;
        _failed = true;
        try { PeekSession.Stop(resetLook: true); }
        catch (Exception cleanup) { Log.LogWarning($"Cleanup failed: {cleanup}"); }
        Log.LogError($"{Name} hit an unrecoverable error and has been disabled: {e}");
    }
}
