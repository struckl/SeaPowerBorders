using System;
using AnchorChain;
using HarmonyLib;
using UnityEngine;

namespace SeaPowerBorders;

/// <summary>
/// Entry point. Loaded by Anchor Chain (the Sea Power community chainloader on top of BepInEx 5), which scans
/// every mod folder — Steam Workshop items and <c>StreamingAssets/&lt;folder&gt;</c> — for classes implementing
/// <see cref="IAnchorChainMod"/>.
/// <para>Cities and naval bases are in; the borders are next (<c>docs/plan.md</c>).</para>
/// </summary>
[ACPlugin(Guid, Name, Version)]
public class Plugin : IAnchorChainMod
{
    public const string Guid = "dev.sewerlabs.seapowerborders";
    public const string Name = "Sea Power Cities and Borders";
    public const string Version = "1.0.0";

    // Anchor Chain loads assemblies with Assembly.LoadFile, so the same DLL found in two folders would be two
    // separate assemblies with separate statics. The marker lives on the AppDomain so only the first one starts.
    private const string LoadedMarker = Guid + ".loaded";

    public void TriggerEntryPoint()
    {
        if (AppDomain.CurrentDomain.GetData(LoadedMarker) != null)
        {
            ModLog.Warn("Already loaded from another folder; skipping this copy.");
            return;
        }
        AppDomain.CurrentDomain.SetData(LoadedMarker, true);

        try
        {
            Settings.Init();
            if (!Settings.Enabled.Value)
            {
                ModLog.Info("Disabled in config.");
                return;
            }

            // Only the hooks on plain game classes now; the map hook follows once NoesisGUI runs (see Patches).
            if (!Patches.ApplyGameHooks(new Harmony(Guid)))
                return;

            var host = new GameObject("SeaPowerBorders");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<AtlasHost>();

            ModLog.Info($"{Name} {Version} loaded.");
        }
        catch (Exception ex)
        {
            // Never take the game down with us.
            ModLog.Error($"Startup failed, mod inactive: {ex}");
        }
    }
}
