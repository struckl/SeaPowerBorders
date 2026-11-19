using System;
using MapControl;
using SeaPower;
#if FLATMAP
using SeaPower.LiveMap;
#endif
using SeapowerUI;
using SeaPowerBorders.Borders;
using SeaPowerBorders.Features;
using UnityEngine;

namespace SeaPowerBorders;

/// <summary>
/// Drives the mod: applies the map hook once NoesisGUI runs, and during a mission keeps the Layers-menu entries, the
/// toggle keys, the border lines and the labels of every live map in step. Out of a mission, the mission editor's map
/// gets the same lines and labels (for the date set in the editor), with two buttons and the toggle keys to switch
/// them. The places and borders are put together once per mission, for its date (<see cref="PlaceBook"/>,
/// <see cref="BorderBook"/>).
/// </summary>
internal sealed class AtlasHost : MonoBehaviour
{
    public static AtlasHost Instance { get; private set; }

    private enum HookState { Waiting, Applied, Failed }

    private readonly LayerEntry bordersLayer = new LayerEntry(601, "SEWERLABS_BORDERS", "Borders", Settings.BordersVisible);
    private readonly LayerEntry citiesLayer = new LayerEntry(602, "SEWERLABS_CITIES", "Cities and bases", Settings.CitiesVisible);
    private readonly DecoratedMaps maps = new DecoratedMaps();
    private readonly PlaceBook places = new PlaceBook();
    private readonly BorderBook borders = new BorderBook();
    private EditorMap editor;
    private SeaLimitCache seaLimits;

    // The date the places and borders are of; null: put them together again.
    private DateTime? dataDate;
    private int bordersSettings = -1;

    private HookState mapHooks;
    private Every hookCheck, editorCheck, markerCheck, prune;
    private bool missionActive;
    private int errors;

    /// <summary>A gate on the game's unscaled clock for work done every so many seconds.</summary>
    private struct Every
    {
        private float next;

        public bool Due(float seconds)
        {
            if (Time.unscaledTime < next)
                return false;
            next = Time.unscaledTime + seconds;
            return true;
        }
    }

    private void Awake()
    {
        Instance = this;
        editor = new EditorMap(ToggleBorders, ToggleCities);
    }

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
        // A WindowHost registers itself with WindowHostManager when it is built, which only happens while NoesisGUI
        // runs (the main menu view has one) - from then on Noesis types are safe to touch.
        if (mapHooks == HookState.Waiting && hookCheck.Due(1f) && Singleton<WindowHostManager>.InstanceExists())
            ApplyMapHooks();
        if (!missionActive && mapHooks == HookState.Applied && editorCheck.Due(1f))
            FollowEditor();
        if (!missionActive && editor.Decor == null)
            return;
        TickSwitches();
        TickData();
        if (prune.Due(2f))
            maps.Prune();
        // Territorial waters come from the game's world grid, read on a worker thread when a map first asks.
        seaLimits ??= new SeaLimitCache(SeaMask.PathIn(Application.streamingAssetsPath));
        seaLimits.DrainLog();
        DrawMaps();
        editor.UpdateButtons(bordersLayer.Visible, citiesLayer.Visible);
    }

    // The editor has no Layers menu: the switches stay in our config there, and the entries come with the first mission.
    private void TickSwitches()
    {
        if (missionActive)
        {
            bordersLayer.EnsureRegistered();
            citiesLayer.EnsureRegistered();
        }
        if (Settings.CitiesKey.Value.IsDown())
            ToggleCities();
        if (Settings.BordersKey.Value.IsDown())
            ToggleBorders();
    }

    private void TickData()
    {
        if (dataDate == null)
        {
            dataDate = DataDate();
            places.Build(dataDate.Value);
            bordersSettings = -1;
        }
        // Again when a setting changes: the Era decides which lines are drawn.
        if (bordersSettings != Settings.Version)
        {
            bordersSettings = Settings.Version;
            borders.Select(dataDate.Value, Settings.Era.Value);
        }
        // The mission's land units come in a moment after the mission starts and can change: look again every 2 s.
        if (missionActive && markerCheck.Due(2f))
            places.ReadMarks();
    }

    private void DrawMaps()
    {
#if FLATMAP
        // Sea Power 0.8.7: one theme for every map.
        bool dark = Singleton<ThemeTracker>.InstanceExists() && Singleton<ThemeTracker>.Instance.CurrentTheme == ThemeMode.Dark;
#endif
        if (editor.Decor != null)
            places.EditorShowsGameLabels(EditorMap.ShowsGameLabels());
        bool bordersOn = bordersLayer.Visible, citiesOn = citiesLayer.Visible;
        foreach (MapDecor map in maps.All)
        {
#if !FLATMAP
            // The 3D-globe build: each map has its own palette (Auto follows the sun at the centre of its view).
            bool dark = GameApi.IsDark((map.Map.DataContext as MapViewModel)?.Palette);
#endif
            map.Borders?.Update(bordersOn, borders.Selected, Looks.Borders(dark), seaLimits);
            map.Labels?.Update(citiesOn, map == editor.Decor ? places.ForEditor : places.ForMission, Looks.Labels(dark));
        }
    }

    private void ToggleCities()
    {
        citiesLayer.Toggle();
        ModLog.Info($"Cities {(citiesLayer.Visible ? "on" : "off")}.");
    }

    private void ToggleBorders()
    {
        bordersLayer.Toggle();
        ModLog.Info($"Borders {(bordersLayer.Visible ? "on" : "off")}.");
    }

    // A new editor map, or another date set in the editor: other borders, other places.
    private void FollowEditor()
    {
        if (editor.Follow(maps) && editor.Decor != null)
            Forget();
        if (editor.Decor != null && EditorMap.Date() is DateTime date && dataDate is DateTime built && date.Date != built.Date)
            Forget();
    }

    private void Forget()
    {
        dataDate = null;
        places.Forget();
        borders.Forget();
    }

    // The mission's date (in the editor, the date set there), else today.
    private DateTime DataDate()
    {
        if (!missionActive && EditorMap.Date() is DateTime editorDate)
            return editorDate.Date;
        return GameDate.Mission() ?? DateTime.Today;
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
            ModLog.Error($"Map hook failed - nothing on the map: {ex}");
        }
        if (mapHooks == HookState.Applied)
            ModLog.Info("Map hook in place.");
    }

    internal void MissionReady()
    {
        try
        {
            editor.Leave(maps);                     // a test run from the editor
            missionActive = true;
            Forget();
            bordersLayer.EnsureRegistered();
            citiesLayer.EnsureRegistered();
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
            editor.Leave(maps);
            maps.Clear();
            Forget();
            places.ClearMarks();
        }
        catch (Exception ex)
        {
            ModLog.Error($"Mission end handling failed: {ex}");
        }
    }

    /// <summary>Every frame from each map control showing the live map: decorate the ones we haven't yet.</summary>
    internal void MapRendered(RenderedLayer rendered)
    {
        try
        {
            MapBase map = rendered.ParentMap;
            if (!missionActive || map == null || maps.Knows(map))
                return;
            MapDecor decor = MapDecor.TryMake(map);
            if (decor == null)
            {
                maps.AddUndecoratable(map);
                return;
            }
            maps.Add(decor);
            ModLog.Info($"Attached to the map:{(decor.Borders != null ? " border lines" : "")}{(decor.Labels != null ? " place labels" : "")}.");
        }
        catch (Exception ex)
        {
            if (errors++ < 5)
                ModLog.Error($"Map layers failed: {ex}");
        }
    }
}
