using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeaPowerBorders.Places;
using UnityEngine;

namespace SeaPowerBorders.Features;

/// <summary>
/// The places of the mission's date: the game's own campaign lists, the mod's naval bases and extra places, Natural
/// Earth's places (read once per game session), put together per date. What the maps label is handed out as lists
/// that are new objects whenever they change, so a map makes its labels again on a new list.
/// </summary>
internal sealed class PlaceBook
{
    private readonly MarkedPlaces marks = new MarkedPlaces();
    private List<Place> gamePlaces, worldPlaces;
    private List<BaseEntry> bases;
    private List<EraRule> eras;
    private List<Place> all;
    private bool editorGameLabels;

    /// <summary>The live maps' places: all but those the mission marks itself. Null until built.</summary>
    public IReadOnlyList<Place> ForMission { get; private set; }

    /// <summary>The editor map's places (see <see cref="EditorShowsGameLabels"/>). Null until built.</summary>
    public IReadOnlyList<Place> ForEditor { get; private set; }

    public bool Built => all != null;

    public void Build(DateTime date)
    {
        try
        {
            if (gamePlaces == null)
                LoadLists();
            all = Atlas.Build(gamePlaces, worldPlaces, bases, eras, date);
            Rig.Log.Info($"{all.Count} places for {date:yyyy-MM-dd} ({gamePlaces.Count} from the game's lists, " +
                        $"{all.Count(p => p.Naval)} naval bases, {all.Count(p => p.Capital)} capitals).");
        }
        catch (Exception ex)
        {
            Rig.Log.Error($"Could not put the places together: {ex}");
            all = new List<Place>();
        }
        Hand();
    }

    public void Forget()
    {
        all = null;
        ForMission = ForEditor = null;
    }

    /// <summary>
    /// The editor draws the game's own cities and bases itself when its Map Labels layer is on: the editor map then
    /// gets ours only for the rest.
    /// </summary>
    public void EditorShowsGameLabels(bool shown)
    {
        if (shown == editorGameLabels)
            return;
        editorGameLabels = shown;
        Hand();
    }

    /// <summary>The mission's land units and city markers, read again; the maps' lists follow when they changed.</summary>
    public void ReadMarks()
    {
        if (marks.Replace(GameMarkers.Read()))
            Hand();
    }

    public void ClearMarks()
    {
        marks.Clear();
        Hand();
    }

    private void LoadLists()
    {
        gamePlaces = GameLists.Load(GameLists.DirIn(Application.streamingAssetsPath));
        string dir = ModFolder.Sub("places");
        worldPlaces = ModLists.LoadWorld(dir);
        bases = ModLists.LoadBases(dir);
        eras = ModLists.LoadEras(dir);
    }

    private void Hand()
    {
        if (all == null)
            return;
        List<Place> unmarked = all.Where(p => !marks.Marks(p)).ToList();
        ForMission = unmarked;
        ForEditor = editorGameLabels ? unmarked.Where(p => !p.FromGame).ToList() : unmarked;
    }
}

/// <summary>The mod's own folder (next to its DLL), where its data files are.</summary>
internal static class ModFolder
{
    public static string Sub(string name) =>
        Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", name);
}
