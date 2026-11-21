using System;
using System.Collections.ObjectModel;
using MapControl;
using Noesis;
using SeaPowerBorders.Borders;

namespace SeaPowerBorders.Map;

/// <summary>What draws the lines into a map's two panels, for one projection, look and selection of lines.</summary>
internal interface IBorderDrawing
{
    /// <summary>Every frame while the Borders layer is on.</summary>
    void Update(SeaLimitCache waters);

    /// <summary>From the map's own viewport update, before it lays out its children.</summary>
    void ViewportChanged();

    /// <summary>Empties what it drew into the panels (another drawing takes over).</summary>
    void Clear();
}

/// <summary>
/// The border lines on one map control. They live in a <see cref="MapPanel"/> laid into the map below the place
/// labels, the coastal distance line in a second panel below the borders. A flat map (the mission editor's) gets <see cref="FlatDrawing"/>,
/// the mission's globe <c>GlobeDrawing</c>; a new projection, look or set of lines starts a new drawing.
/// </summary>
internal sealed class BorderLines
{
    private readonly MapBase map;
    private readonly ObservableCollection<object> background;      // the editor's map: panels go in here, not in Children
    private readonly MapPanel panel, waterPanel;
    private readonly EventHandler<ViewportChangedEventArgs> onViewport;
    private readonly FailureLog failure = new FailureLog("Border lines");

    private IBorderDrawing drawing;
    private bool drawingGlobe;
    private BorderLook look;
    private BorderSelection lines;

    /// <summary>Null when the map would not take the panels (logged).</summary>
    public static BorderLines TryAttach(MapBase map, ObservableCollection<object> background = null)
    {
        try
        {
            return new BorderLines(map, background);
        }
        catch (Exception ex)
        {
            Rig.Log.Warn($"Could not add the border lines to the map: {ex.Message}");
            return null;
        }
    }

    private BorderLines(MapBase map, ObservableCollection<object> background)
    {
        this.map = map;
        this.background = background;
        panel = new MapPanel { IsHitTestVisible = false };
        MapPanel.SetParentMap(panel, map);
        MapSlots.Insert(map, panel, MapSlots.Borders, background);
        waterPanel = new MapPanel { IsHitTestVisible = false };
        MapPanel.SetParentMap(waterPanel, map);
        MapSlots.Insert(map, waterPanel, MapSlots.Waters, background);
        // Runs inside the map's own viewport update: never let anything out.
        onViewport = (_, __) =>
        {
            try
            {
                drawing?.ViewportChanged();
            }
            catch (Exception ex)
            {
                failure.Report(ex);
            }
        };
        map.ViewportChanged += onViewport;
    }

    public void Detach()
    {
        try
        {
            map.ViewportChanged -= onViewport;
            MapSlots.Remove(map, panel, background);
            MapPanel.SetParentMap(panel, null);
            MapSlots.Remove(map, waterPanel, background);
            MapPanel.SetParentMap(waterPanel, null);
        }
        catch (Exception ex)
        {
            Rig.Log.Debug($"Border detach: {ex.Message}");
        }
    }

    /// <param name="visible">The Borders layer is on.</param>
    /// <param name="selection">The lines of the mission's date; null while they are not there yet.</param>
    /// <param name="lineLook">The look for this map's theme.</param>
    /// <param name="waters">Territorial-water tiles, null for none.</param>
    public void Update(bool visible, BorderSelection selection, BorderLook lineLook, SeaLimitCache waters)
    {
        Visibility shown = visible && selection != null ? Visibility.Visible : Visibility.Collapsed;
        if (panel.Visibility != shown)
            panel.Visibility = shown;
        if (waterPanel.Visibility != shown)
            waterPanel.Visibility = shown;
        if (shown != Visibility.Visible)
            return;
        try
        {
            bool globe = MapView.IsGlobe(map);
            if (drawing == null || globe != drawingGlobe || !lineLook.Equals(look) || selection != lines)
                Restart(globe, lineLook, selection);
            drawing.Update(waters);
        }
        catch (Exception ex)
        {
            failure.Report(ex);
        }
    }

    private void Restart(bool globe, BorderLook lineLook, BorderSelection selection)
    {
        drawing?.Clear();
        drawingGlobe = globe;
        look = lineLook;
        lines = selection;
        drawing = globe ? new GlobeDrawing(map, panel, waterPanel, look, lines) : new FlatDrawing(map, panel, waterPanel, look, lines);
    }
}
