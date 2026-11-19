using System.Runtime.CompilerServices;
using AnchorChain;

namespace SeaPowerBorders;

/// <summary>
/// Entry point, started by a mod loader (Keel or Anchor Chain) through TriggerEntryPoint.
/// Sea Power exists in two versions with different map code (0.8.7's flat map, and the 3D globe build), and a mod compiled
/// against one fails on the other. So the package carries two builds of the same sources: SeaPowerBorders.dll for the globe
/// build and SeaPowerBordersFlat.dll (FLATMAP defined) for 0.8.7. Each looks at the game it runs on and stays off when it is
/// not the one it was made for (ModEntry, in the shared skeleton).
/// </summary>
[ACPlugin(PluginGuid, Name, Version)]
public class Plugin : IAnchorChainMod
{
    public const string Guid = "dev.sewerlabs.seapowerborders";
    public const string Version = "1.1.3";
    public const string Name = "R.A.T.S. Cities and Borders";

#if FLATMAP
    // Both loaders skip a plugin whose GUID they have already seen, so the two builds register under different ones.
    public const string PluginGuid = Guid + ".flatmap";
#else
    public const string PluginGuid = Guid;
#endif

    public void TriggerEntryPoint() => ModEntry.Trigger(Start);

    // Apart from the check in Trigger, so nothing in here is compiled on a game it isn't for.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Start() => ModEntry.Start<Host>(Settings.Bind, Hooks.Declare);
}
