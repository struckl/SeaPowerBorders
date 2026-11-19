using System;
using System.Collections.Generic;
using MapControl;
using SeaPower;
using SeapowerUI;
using SeapowerUI.ViewModels;
using SeaPowerBorders.Ui;
using UnityEngine;

namespace SeaPowerBorders.Features;

/// <summary>
/// The mission editor's map (a flat map of its own: the editor's view model holds it, the control showing it is found
/// in the UI the way Topo finds it). It gets the lines and labels, drawn into the map view model's background
/// elements, and two buttons, since it has no Layers menu to hold the mod's entries.
/// </summary>
internal sealed class EditorMap
{
    private const int MaxVisualNodes = 50000;          // a safety net for the visual-tree search

    private readonly Action toggleBorders, toggleCities;
    private EditorButtons buttons;

    public EditorMap(Action toggleBorders, Action toggleCities)
    {
        this.toggleBorders = toggleBorders;
        this.toggleCities = toggleCities;
    }

    /// <summary>The editor map's layers; null while it is not on screen.</summary>
    public MapDecor Decor { get; private set; }

    /// <summary>
    /// Looks for the map (once a second while no mission runs): takes it on when it came, lets go when it went.
    /// True when the editor map changed.
    /// </summary>
    public bool Follow(DecoratedMaps maps)
    {
        MapBase found = Find();
        if (ReferenceEquals(found, Decor?.Map))
            return false;
        Leave(maps);
        if (found != null)
            Enter(found, maps);
        return true;
    }

    public void Leave(DecoratedMaps maps)
    {
        if (Decor == null)
            return;
        maps.Remove(Decor);
        Decor = null;
        buttons?.Detach();
        buttons = null;
        ModLog.Info("Mission editor map closed.");
    }

    public void UpdateButtons(bool bordersOn, bool citiesOn) => buttons?.Update(bordersOn, citiesOn);

    /// <summary>The date set in the mission editor; null while there is none.</summary>
    public static DateTime? Date() => GameDate.Valid(MissionEditorViewModel.Instance?.DateTimeMission?.DateTimeUsed ?? default);

    /// <summary>The editor's own "Map Labels" layer (on by default) is on; true if that can't be read.</summary>
    public static bool ShowsGameLabels()
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

    private void Enter(MapBase map, DecoratedMaps maps)
    {
        if (map.DataContext is not MapViewModel model)
            return;
        MapDecor decor = MapDecor.TryMake(map, model.BackgroundElements);
        if (decor == null)
            return;
        Decor = decor;
        maps.Add(decor);
        buttons = EditorButtons.TryAttach(map, toggleBorders, toggleCities);
        ModLog.Info("Mission editor map found: borders and cities drawn there too.");
    }

    private MapBase Find()
    {
        MapViewModel model = MissionEditorViewModel.Instance?.Map?.Map;
        if (model == null)
            return null;
        MapBase known = Decor?.Map;
        if (known != null && ReferenceEquals(known.DataContext, model) && Noesis.VisualTreeHelper.GetParent(known) != null &&
            known.ActualWidth >= 2f && known.IsVisible)
            return known;
        foreach (NoesisView view in UnityEngine.Object.FindObjectsByType<NoesisView>(FindObjectsSortMode.None))
        {
            MapBase found = view.Content == null ? null : Search(view.Content, model);
            if (found != null)
                return found;
        }
        return null;
    }

    // The visible map control showing the model, below one view's content.
    private static MapBase Search(Noesis.DependencyObject root, MapViewModel model)
    {
        var stack = new Stack<Noesis.DependencyObject>();
        stack.Push(root);
        for (int seen = 0; stack.Count > 0 && seen < MaxVisualNodes; seen++)
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
        return null;
    }
}

/// <summary>The game's dates: a year up to 1900 means "not set" (the editor before a mission is loaded).</summary>
internal static class GameDate
{
    private static bool IsSet(int year) => year > 1900;

    public static DateTime? Valid(DateTime date) => IsSet(date.Year) ? date : (DateTime?)null;

    /// <summary>The mission's date as the game set it from the mission file, before the mission starts.</summary>
    public static DateTime? Mission()
    {
        if (!Singleton<SeaPower.Environment>.InstanceExists())
            return null;
        SeaPower.Environment environment = Singleton<SeaPower.Environment>.Instance;
        int year = environment.Year, month = environment.Month, day = environment.Day;
        if (!IsSet(year) || month < 1 || month > 12 || day < 1)
            return null;
        return new DateTime(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
    }
}
