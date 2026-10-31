using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapControl;
using SeaPower;
using SeaPower.LiveMap;
using SeapowerUI;
using SeapowerUI.ViewModels;
using SeaPowerBorders.Ui;
using SeaPowerBorders.Borders;
using SeaPowerBorders.Map;
using SeaPowerBorders.Places;
using UnityEngine;

namespace SeaPowerBorders;

/// <summary>
/// Drives the mod: applies the map hook once NoesisGUI runs, and during a mission keeps the Layers-menu entries, the
/// toggle keys, the border lines and the labels of every live map in step. Out of a mission, the mission editor's map
/// gets the same lines and labels (for the date set in the editor), with two buttons and the toggle keys to switch them. The mission's places and borders are put
/// together once per mission, for its date: the game's own campaign lists, the mod's naval bases and extra places,
/// Natural Earth's places and border lines.
/// </summary>
internal sealed class AtlasHost : MonoBehaviour
{
    public static AtlasHost Instance { get; private set; }

    private enum HookState { Waiting, Applied, Failed }

    /// <summary>The mod's layers on one map control.</summary>
    private sealed class MapDecor
    {
        public MapBase Map;
        public BorderLines Borders;
        public CityLabels Labels;

        public bool Alive => (Borders?.Alive ?? true) && (Labels?.Alive ?? true);

        public void Detach()
        {
            Borders?.Detach();
            Labels?.Detach();
        }
    }

    private readonly LayerEntry bordersLayer = new LayerEntry(601, "SEWERLABS_BORDERS", "Borders", Settings.BordersVisible);
    private readonly LayerEntry citiesLayer = new LayerEntry(602, "SEWERLABS_CITIES", "Cities and bases", Settings.CitiesVisible);
    private readonly List<MapDecor> maps = new List<MapDecor>();
    private readonly HashSet<MapBase> undecoratable = new HashSet<MapBase>();

    // The lists and border lines, read once per game session.
    private List<Place> gamePlaces, worldPlaces;
    private List<BaseEntry> bases;
    private List<EraRule> eras;
    private BorderData borderData;
    private BorderEras borderEras;
    private SeaLimitCache seaLimits;
    private bool bordersMissing;

    private List<Place> places;
    private int placesVersion;
    private DateTime missionDate;
    private HashSet<BorderLine> drawnBorders;
    private int bordersVersion, bordersSettings = -1;

    // The mission editor's map (a flat map of its own, found the way Topo finds it).
    private MapDecor editorDecor;
    private EditorButtons editorButtons;
    private float nextEditorCheck;
    private List<Place> editorPlaces, editorPlacesFrom;
    private bool editorGameLabels;
    private int editorPlacesVersion;

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
        if (!missionActive && mapHooks == HookState.Applied)
            TickEditor();
        if (!missionActive && editorDecor == null)
            return;

        // The editor has no Layers menu: the switches stay in our config there, and the entries come with the first mission.
        if (missionActive)
        {
            bordersLayer.EnsureRegistered();
            citiesLayer.EnsureRegistered();
        }
        if (Settings.CitiesKey.Value.IsDown())
            ToggleCities();
        if (Settings.BordersKey.Value.IsDown())
            ToggleBorders();
        if (places == null && Time.unscaledTime >= nextBuildTry)
        {
            nextBuildTry = Time.unscaledTime + 1f;
            BuildPlaces();
        }
        if (places != null && bordersSettings != Settings.Version)
            SelectBorders();

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
        // Territorial waters come from the game's world grid, read on a worker thread when a map first asks.
        seaLimits ??= new SeaLimitCache(Path.Combine(Application.streamingAssetsPath, "original", "terrain", "data", "lowres.raw"),
                                         () => Settings.VerboseLog.Value);
        seaLimits.DrainLog();
#if FLATMAP
        // Sea Power 0.8.7: one theme for every map.
        bool dark = Singleton<ThemeTracker>.InstanceExists() && Singleton<ThemeTracker>.Instance.CurrentTheme == ThemeMode.Dark;
#endif
        bool bordersOn = bordersLayer.Visible, citiesOn = citiesLayer.Visible;
        foreach (MapDecor map in maps)
        {
#if !FLATMAP
            // The 3D-globe build: each map has its own palette (Auto follows the sun at the centre of its view).
            bool dark = GameApi.IsDark((map.Map.DataContext as MapViewModel)?.Palette);
#endif
            map.Borders?.Update(bordersOn, borderData, drawnBorders, bordersVersion, dark, seaLimits);
            if (map == editorDecor)
            {
                // The editor draws the game's own cities and bases itself (its Map Labels checkbox): ours only for the rest.
                bool gameLabels = EditorShowsGameLabels();
                if (editorPlaces == null || editorPlacesFrom != places || editorGameLabels != gameLabels)
                {
                    editorPlacesFrom = places;
                    editorGameLabels = gameLabels;
                    editorPlaces = places == null ? null : gameLabels ? places.Where(p => !p.FromGame).ToList() : places;
                    editorPlacesVersion++;
                }
                map.Labels?.Update(citiesOn, editorPlaces, editorPlacesVersion, dark, Time.unscaledTime);
            }
            else
                map.Labels?.Update(citiesOn, places, placesVersion, dark, Time.unscaledTime);
        }
        editorButtons?.Update(bordersOn, citiesOn);
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

    /// <summary>
    /// The mission editor: looked for once a second while no mission runs. Its map (the editor's view model holds it;
    /// the control showing it is found in the UI) gets the lines and labels, drawn into the map view model's background
    /// elements, and two buttons. Places and borders are those of the date set in the editor.
    /// </summary>
    private void TickEditor()
    {
        if (Time.unscaledTime < nextEditorCheck)
            return;
        nextEditorCheck = Time.unscaledTime + 1f;
        MapBase found = FindEditorMap();
        if (!ReferenceEquals(found, editorDecor?.Map))
        {
            LeaveEditor();
            if (found != null)
                EnterEditor(found);
        }
        if (editorDecor != null && EditorDate() is DateTime date && date.Date != missionDate.Date && places != null)
        {
            // The date was changed in the editor: other borders, other places.
            places = null;
            drawnBorders = null;
            bordersSettings = -1;
            nextBuildTry = 0f;
        }
    }

    private void EnterEditor(MapBase map)
    {
        var model = map.DataContext as MapViewModel;
        if (model == null)
            return;
        var decor = new MapDecor
        {
            Map = map,
            Borders = BorderLines.TryAttach(map, model.BackgroundElements),
            Labels = CityLabels.TryAttach(map, model.BackgroundElements),
        };
        if (decor.Borders == null && decor.Labels == null)
            return;
        editorDecor = decor;
        maps.Add(decor);
        editorButtons = EditorButtons.TryAttach(map, ToggleBorders, ToggleCities);
        places = null;
        drawnBorders = null;
        bordersSettings = -1;
        nextBuildTry = 0f;
        ModLog.Info("Mission editor map found: borders and cities drawn there too.");
    }

    private void LeaveEditor()
    {
        if (editorDecor == null)
            return;
        editorDecor.Detach();
        maps.Remove(editorDecor);
        editorDecor = null;
        editorButtons?.Detach();
        editorButtons = null;
        ModLog.Info("Mission editor map closed.");
    }

    // The editor's own "Map Labels" layer (on by default) is on; true if that can't be read.
    private static bool EditorShowsGameLabels()
    {
        try
        {
            return MissionEditorViewModel.Instance?.Map?.ShowMapLabels ?? true;
        }
        catch
        {
            return true;
        }
    }

    // The date set in the mission editor, or null.
    private static DateTime? EditorDate()
    {
        DateTime used = MissionEditorViewModel.Instance?.DateTimeMission?.DateTimeUsed ?? default;
        return used.Year > 1900 ? used : (DateTime?)null;
    }

    // The editor's map control, if it is on screen.
    private MapBase FindEditorMap()
    {
        MapViewModel model = MissionEditorViewModel.Instance?.Map?.Map;
        if (model == null)
            return null;
        MapBase known = editorDecor?.Map;
        if (known != null && ReferenceEquals(known.DataContext, model) && Noesis.VisualTreeHelper.GetParent(known) != null &&
            known.ActualWidth >= 2f && known.IsVisible)
            return known;
        foreach (NoesisView view in FindObjectsByType<NoesisView>(FindObjectsSortMode.None))
        {
            if (view.Content == null)
                continue;
            var stack = new Stack<Noesis.DependencyObject>();
            stack.Push(view.Content);
            for (int seen = 0; stack.Count > 0 && seen < 50000; seen++)
            {
                Noesis.DependencyObject d = stack.Pop();
                if (d is MapBase candidate)
                {
                    if (ReferenceEquals(candidate.DataContext, model) && candidate.IsVisible && candidate.ActualWidth > 0f)
                        return candidate;
                    continue;
                }
                if (d is Noesis.Visual v)
                {
                    int n = Noesis.VisualTreeHelper.GetChildrenCount(v);
                    for (int i = 0; i < n; i++)
                        stack.Push(Noesis.VisualTreeHelper.GetChild(v, i));
                }
            }
        }
        return null;
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
            LeaveEditor();                     // a test run from the editor
            missionActive = true;
            places = null;
            drawnBorders = null;
            bordersSettings = -1;
            nextBuildTry = 0f;
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
            LeaveEditor();
            foreach (MapDecor map in maps)
                map.Detach();
            maps.Clear();
            undecoratable.Clear();
            places = null;
            drawnBorders = null;
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
            if (!missionActive || map == null || undecoratable.Contains(map))
                return;
            foreach (MapDecor known in maps)
            {
                if (known.Map == map)
                    return;
            }
            var decor = new MapDecor { Map = map, Borders = BorderLines.TryAttach(map), Labels = CityLabels.TryAttach(map) };
            if (decor.Borders == null && decor.Labels == null)
            {
                undecoratable.Add(map);
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

    // The mission's date (the game sets it from the mission file before the mission starts; in the editor, the date set
    // there), else today.
    private DateTime MissionDate()
    {
        if (!missionActive && EditorDate() is DateTime editorDate)
            return editorDate.Date;
        if (Singleton<SeaPower.Environment>.InstanceExists())
        {
            SeaPower.Environment environment = Singleton<SeaPower.Environment>.Instance;
            if (environment.Year > 1900 && environment.Month >= 1 && environment.Month <= 12 && environment.Day >= 1)
                return new DateTime(environment.Year, environment.Month, Math.Min(environment.Day, DateTime.DaysInMonth(environment.Year, environment.Month)));
        }
        return DateTime.Today;
    }

    private static string ModDir(string sub) =>
        Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", sub);

    private void BuildPlaces()
    {
        try
        {
            if (gamePlaces == null)
            {
                gamePlaces = GameLists.Load(Path.Combine(Application.streamingAssetsPath, "original", "campaigns"));
                string dir = ModDir("places");
                worldPlaces = ModLists.LoadWorld(dir);
                bases = ModLists.LoadBases(dir);
                eras = ModLists.LoadEras(dir);
            }
            missionDate = MissionDate();
            places = Atlas.Build(gamePlaces, worldPlaces, bases, eras, missionDate);
            placesVersion++;
            ModLog.Info($"{places.Count} places for {missionDate:yyyy-MM-dd} ({gamePlaces.Count} from the game's lists, " +
                        $"{places.Count(p => p.Naval)} naval bases, {places.Count(p => p.Capital)} capitals).");
        }
        catch (Exception ex)
        {
            ModLog.Error($"Could not put the places together: {ex}");
            places = new List<Place>();
            placesVersion++;
        }
    }

    // The border lines of the mission's date (or today's, by the Era setting); again when a setting changes.
    private void SelectBorders()
    {
        bordersSettings = Settings.Version;
        if (bordersMissing)
            return;
        try
        {
            if (borderData == null)
            {
                string dir = ModDir("borders");
                borderData = BorderData.Load(Path.Combine(dir, "borders.bin"));
                borderEras = BorderEras.Load(dir);
            }
            BorderEra era = Settings.Era.Value;
            drawnBorders = new HashSet<BorderLine>(borderData.Lines.Where(l => borderEras.Drawn(l, missionDate, era)));
            bordersVersion++;
            List<string> added = drawnBorders.Where(l => l.Class == BorderClass.Added).Select(l => l.Name).Distinct().ToList();
            ModLog.Info($"{drawnBorders.Count} of {borderData.Lines.Count} border lines for " +
                        $"{(era == BorderEra.Today ? "today" : missionDate.ToString("yyyy-MM-dd"))}" +
                        $"{(added.Count > 0 ? " with " + string.Join(", ", added) : "")}.");
        }
        catch (Exception ex)
        {
            bordersMissing = true;
            ModLog.Error($"Could not load the border lines - no borders this session: {ex}");
        }
    }
}
