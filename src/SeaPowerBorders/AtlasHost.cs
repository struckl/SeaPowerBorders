using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapControl;
using SeaPower;
using SeaPower.LiveMap;
using SeapowerUI;
using SeaPowerBorders.Map;
using SeaPowerBorders.Places;
using UnityEngine;

namespace SeaPowerBorders;

/// <summary>
/// Drives the mod: applies the map hook once NoesisGUI runs, and during a mission keeps the Layers-menu entry, the
/// toggle key and the labels of every live map in step. The mission's places are put together once per mission,
/// for its date: the game's own campaign lists, the mod's naval bases and extra places, Natural Earth.
/// </summary>
internal sealed class AtlasHost : MonoBehaviour
{
    public static AtlasHost Instance { get; private set; }

    private enum HookState { Waiting, Applied, Failed }

    private readonly CitiesLayer layer = new CitiesLayer();
    private readonly List<CityLabels> maps = new List<CityLabels>();
    private readonly HashSet<MapBase> undecoratable = new HashSet<MapBase>();

    // The lists, read once per game session.
    private List<Place> gamePlaces, worldPlaces;
    private List<BaseEntry> bases;
    private List<EraRule> eras;

    private List<Place> places;
    private int placesVersion;
    private DateTime placesDate;

    private HookState mapHooks;
    private float nextHookCheck, nextPrune, nextBuildTry;
    private bool missionActive;
    private int errors;

    private void Awake() => Instance = this;

    private void Update()
    {
        try
        {
            Tick();
        }
        catch (Exception ex)
        {
            // Log the first few, then stay quiet; a broken frame must never break the game's.
            if (errors++ < 5)
                ModLog.Error($"Update failed: {ex}");
        }
    }

    private void Tick()
    {
        if (mapHooks == HookState.Waiting && Time.unscaledTime >= nextHookCheck)
        {
            nextHookCheck = Time.unscaledTime + 1f;
            // A WindowHost registers itself with WindowHostManager when it is built, which only happens while
            // NoesisGUI runs (the main menu view has one) – from then on Noesis types are safe to touch.
            if (Singleton<WindowHostManager>.InstanceExists())
                ApplyMapHooks();
        }
        if (!missionActive)
            return;

        layer.EnsureRegistered();
        if (Settings.CitiesKey.Value.IsDown())
        {
            layer.Toggle();
            ModLog.Info($"Cities {(layer.Visible ? "on" : "off")}.");
        }
        if (places == null && Time.unscaledTime >= nextBuildTry)
        {
            nextBuildTry = Time.unscaledTime + 1f;
            BuildPlaces();
        }

        if (Time.unscaledTime >= nextPrune)
        {
            nextPrune = Time.unscaledTime + 2f;
            for (int i = maps.Count - 1; i >= 0; i--)
            {
                if (!maps[i].Alive)
                {
                    maps[i].Detach();
                    maps.RemoveAt(i);
                }
            }
        }
        bool dark = Singleton<ThemeTracker>.InstanceExists() && Singleton<ThemeTracker>.Instance.CurrentTheme == ThemeMode.Dark;
        bool on = layer.Visible;
        foreach (CityLabels map in maps)
            map.Update(on, places, placesVersion, dark, Time.unscaledTime);
    }

    private void ApplyMapHooks()
    {
        try
        {
            mapHooks = Patches.ApplyMapHooks() ? HookState.Applied : HookState.Failed;
        }
        catch (Exception ex)
        {
            mapHooks = HookState.Failed;
            ModLog.Error($"Map hook failed - no labels on the map: {ex}");
        }
        if (mapHooks == HookState.Applied)
            ModLog.Info("Map hook in place.");
    }

    internal void MissionReady()
    {
        try
        {
            missionActive = true;
            places = null;
            nextBuildTry = 0f;
            layer.EnsureRegistered();
        }
        catch (Exception ex)
        {
            ModLog.Error($"Mission start handling failed: {ex}");
        }
    }

    internal void MissionEnded()
    {
        try
        {
            missionActive = false;
            foreach (CityLabels map in maps)
                map.Detach();
            maps.Clear();
            undecoratable.Clear();
            places = null;
        }
        catch (Exception ex)
        {
            ModLog.Error($"Mission end handling failed: {ex}");
        }
    }

    /// <summary>Every frame from each map control showing the live map: label the ones we haven't yet.</summary>
    internal void MapRendered(RenderedLayer rendered)
    {
        try
        {
            MapBase map = rendered.ParentMap;
            if (!missionActive || map == null || undecoratable.Contains(map))
                return;
            foreach (CityLabels known in maps)
            {
                if (known.Map == map)
                    return;
            }
            CityLabels labels = CityLabels.TryAttach(map);
            if (labels == null)
            {
                undecoratable.Add(map);
                return;
            }
            maps.Add(labels);
            ModLog.Info("Place labels attached to the map.");
        }
        catch (Exception ex)
        {
            if (errors++ < 5)
                ModLog.Error($"Map labels failed: {ex}");
        }
    }

    // The mission's date (the game sets it from the mission file before the mission starts), else today.
    private static DateTime MissionDate()
    {
        if (Singleton<SeaPower.Environment>.InstanceExists())
        {
            SeaPower.Environment environment = Singleton<SeaPower.Environment>.Instance;
            if (environment.Year > 1900 && environment.Month >= 1 && environment.Month <= 12 && environment.Day >= 1)
                return new DateTime(environment.Year, environment.Month, Math.Min(environment.Day, DateTime.DaysInMonth(environment.Year, environment.Month)));
        }
        return DateTime.Today;
    }

    private void BuildPlaces()
    {
        try
        {
            if (gamePlaces == null)
            {
                gamePlaces = GameLists.Load(Path.Combine(Application.streamingAssetsPath, "original", "campaigns"));
                string dir = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", "places");
                worldPlaces = ModLists.LoadWorld(dir);
                bases = ModLists.LoadBases(dir);
                eras = ModLists.LoadEras(dir);
            }
            placesDate = MissionDate();
            places = Atlas.Build(gamePlaces, worldPlaces, bases, eras, placesDate);
            placesVersion++;
            ModLog.Info($"{places.Count} places for {placesDate:yyyy-MM-dd} ({gamePlaces.Count} from the game's lists, " +
                        $"{places.Count(p => p.Naval)} naval bases, {places.Count(p => p.Capital)} capitals).");
        }
        catch (Exception ex)
        {
            ModLog.Error($"Could not put the places together: {ex}");
            places = new List<Place>();
            placesVersion++;
        }
    }
}
