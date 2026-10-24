using System.IO;
using BepInEx;
using BepInEx.Configuration;
using SeaPowerBorders.Map;
using UnityEngine;

namespace SeaPowerBorders;

/// <summary>
/// BepInEx config at <c>BepInEx/config/dev.sewerlabs.seapowerborders.cfg</c>. Sections are numbered so they sort in
/// the order a player would look for them. Changes made while the game runs apply straight away.
/// </summary>
internal static class Settings
{
    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<bool> VerboseLog;

    public static ConfigEntry<bool> CitiesVisible;
    public static ConfigEntry<KeyboardShortcut> CitiesKey;
    public static ConfigEntry<Density> Density;
    public static ConfigEntry<bool> NavalBases;
    public static ConfigEntry<bool> AirBases;
    public static ConfigEntry<bool> Airports;
    public static ConfigEntry<float> LabelSize;

    /// <summary>Bumped on every config change, so the labels are made again.</summary>
    public static int Version { get; private set; }

    public static void Init()
    {
        var file = new ConfigFile(Path.Combine(Paths.ConfigPath, Plugin.Guid + ".cfg"), saveOnInit: true);

        Enabled = file.Bind("1. General", "Enable mod", true,
            "Master switch. Needs a restart.");
        VerboseLog = file.Bind("1. General", "Verbose log", false,
            "Write debug detail to BepInEx/LogOutput.log.");

        CitiesVisible = file.Bind("2. Cities", "Visible", true,
            "Whether the Cities map layer is on. Written by the mod when you toggle it in the map's Layers menu or " +
            "with the toggle key.");
        CitiesKey = file.Bind("2. Cities", "Toggle key", new KeyboardShortcut(KeyCode.F7),
            "Turns the Cities map layer on and off (same as its entry in the map's Layers menu).");
        Density = file.Bind("2. Cities", "Density", Map.Density.Normal,
            "How many places show at a given zoom: Fewer, Normal or More.");
        NavalBases = file.Bind("2. Cities", "Naval bases", true,
            "Mark naval bases with an anchor in the colour of their side (blue West, red East, grey others).");
        AirBases = file.Bind("2. Cities", "Air bases", true,
            "Show the military airfields of the game's lists.");
        Airports = file.Bind("2. Cities", "Civil airports", false,
            "Show the civil airports of the game's lists too (close in only).");
        LabelSize = file.Bind("2. Cities", "Label size", 1f,
            new ConfigDescription("Size of names and symbols.", new AcceptableValueRange<float>(0.7f, 1.6f)));

        // Turning the layer on and off is not a reason to make the labels again.
        file.SettingChanged += (_, e) =>
        {
            if (e.ChangedSetting != CitiesVisible)
                Version++;
        };
    }
}
