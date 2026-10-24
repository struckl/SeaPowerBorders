using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace SeaPowerBorders;

/// <summary>
/// BepInEx config at <c>BepInEx/config/dev.sewerlabs.seapowerborders.cfg</c>. Sections are numbered so they sort in
/// the order a player would look for them. The layer's own settings (era, territorial waters, labels, colours) come
/// with the layer – see <c>docs/plan.md</c>.
/// </summary>
internal static class Settings
{
    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<bool> VerboseLog;

    public static void Init()
    {
        var file = new ConfigFile(Path.Combine(Paths.ConfigPath, Plugin.Guid + ".cfg"), saveOnInit: true);

        Enabled = file.Bind("1. General", "Enable mod", true,
            "Master switch. Needs a restart.");
        VerboseLog = file.Bind("1. General", "Verbose log", false,
            "Write debug detail to BepInEx/LogOutput.log.");
    }
}
