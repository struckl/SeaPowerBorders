using System;
using System.Collections.Generic;
using MapControl;
using Noesis;
using SeaPowerBorders.Borders;

namespace SeaPowerBorders.Map;

/// <summary>
/// The border lines on one map control. They live in a <see cref="MapPanel"/> laid into the map below the place
/// labels: one Noesis <see cref="Path"/> per 10° tile and line style, which the map places by the tile's bounding box
/// on every pan and zoom (and wraps past ±180°). The geometry is in degrees from the tile's north-west corner, scaled
/// to the zoom by a transform on the geometry rather than on the path, so the stroke keeps its width on screen.
/// <para>The scale follows the map's <c>ViewportChanged</c> event (before the map lays out its children, so lines
/// never lag a frame behind); the level of detail follows the zoom.</para>
/// </summary>
internal sealed class BorderLines
{
    private const double MetresPerDegree = 111319.49079327358;    // the map's equirectangular projection

    public readonly MapBase Map;
    private readonly MapPanel panel;
    private readonly List<ScaleTransform> scales = new List<ScaleTransform>();
    private readonly EventHandler<ViewportChangedEventArgs> onViewport;

    private int linesVersion = -1, settingsVersion = -1, level = -1;
    private bool dark;
    private float scale = float.NaN;
    private SolidColorBrush brush;
    private bool loggedFailure;

    /// <summary>Lays a border panel into the map, or returns null if the map isn't the panel we expect.</summary>
    public static BorderLines TryAttach(MapBase map)
    {
        try
        {
            return new BorderLines(map);
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Could not add the border lines to the map: {ex.Message}");
            return null;
        }
    }

    private BorderLines(MapBase map)
    {
        Map = map;
        panel = new MapPanel { IsHitTestVisible = false };
        MapPanel.SetParentMap(panel, map);
        MapSlots.Insert(map, panel, MapSlots.Borders);
        onViewport = (_, __) => ApplyScale();
        map.ViewportChanged += onViewport;
    }

    public bool Alive => VisualTreeHelper.GetParent(Map) != null;

    public void Detach()
    {
        try
        {
            Map.ViewportChanged -= onViewport;
            Map.Children.Remove(panel);
            MapPanel.SetParentMap(panel, null);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"Border detach: {ex.Message}");
        }
    }

    /// <param name="visible">The Borders layer is on.</param>
    /// <param name="data">All border lines; <paramref name="drawn"/> those of the mission's date, and
    /// <paramref name="version"/> changes when they do.</param>
    public void Update(bool visible, BorderData data, HashSet<BorderLine> drawn, int version, bool darkTheme)
    {
        if (!visible || data == null || drawn == null)
        {
            if (panel.Visibility != Visibility.Collapsed)
                panel.Visibility = Visibility.Collapsed;
            return;
        }
        if (panel.Visibility != Visibility.Visible)
            panel.Visibility = Visibility.Visible;
        try
        {
            if (version != linesVersion || darkTheme != dark || Settings.Version != settingsVersion)
            {
                linesVersion = version;
                dark = darkTheme;
                settingsVersion = Settings.Version;
                // Dashed lines in the same colour: a lighter tint got lost on the terrain (the Korean line by day).
                brush = new SolidColorBrush(dark ? Color.FromArgb(255, 205, 200, 215) : Color.FromArgb(255, 58, 50, 72));
                panel.Opacity = Settings.LineOpacity.Value;
                level = -1;
            }
            double pixelsPerDegree = Map.ViewTransform.Scale * MetresPerDegree;
            if (pixelsPerDegree <= 0)
                return;
            int wanted = data.LevelFor(pixelsPerDegree);
            if (wanted != level)
            {
                Build(data, drawn, wanted);
                level = wanted;
            }
        }
        catch (Exception ex)
        {
            if (!loggedFailure)
            {
                loggedFailure = true;
                ModLog.Error($"Border lines failed: {ex}");
            }
        }
    }

    // One path per tile and style for this level of detail.
    private void Build(BorderData data, HashSet<BorderLine> drawn, int detail)
    {
        panel.Children.Clear();
        scales.Clear();
        scale = (float)(Map.ViewTransform.Scale * MetresPerDegree);
        bool disputed = Settings.DisputedLines.Value;
        var tiles = new Dictionary<(int x, int y, bool dashed), List<BorderPiece>>();
        foreach (BorderPiece piece in data.Pieces)
        {
            if (piece.Level != detail || !drawn.Contains(piece.Line) || (piece.Line.Dashed && !disputed))
                continue;
            var key = (piece.TileX, piece.TileY, piece.Line.Dashed);
            if (!tiles.TryGetValue(key, out List<BorderPiece> list))
                tiles[key] = list = new List<BorderPiece>();
            list.Add(piece);
        }
        int points = 0;
        var line = new List<Point>();
        foreach (KeyValuePair<(int x, int y, bool dashed), List<BorderPiece>> tile in tiles)
        {
            float west = tile.Key.x * BorderData.Tile - 180f, south = tile.Key.y * BorderData.Tile - 90f;
            float north = south + BorderData.Tile;
            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                foreach (BorderPiece piece in tile.Value)
                {
                    float[] p = piece.Points;
                    context.BeginFigure(new Point(p[0] - west, north - p[1]), false, false);
                    line.Clear();
                    for (int k = 2; k < p.Length; k += 2)
                        line.Add(new Point(p[k] - west, north - p[k + 1]));
                    context.PolyLineTo(line, true, true);
                    points += p.Length / 2;
                }
            }
            var transform = new ScaleTransform(scale, scale);
            geometry.Transform = transform;
            scales.Add(transform);
            var path = new Path
            {
                Data = geometry,
                Stroke = brush,
                StrokeThickness = Settings.LineWidth.Value,
                StrokeLineJoin = PenLineJoin.Round,
                IsHitTestVisible = false,
            };
            if (tile.Key.dashed)
                path.StrokeDashArray = "6 3";
            MapPanel.SetBoundingBox(path, new BoundingBox(south, west, north, west + BorderData.Tile));
            panel.Children.Add(path);
        }
        ModLog.Debug($"Borders: level {detail} ({data.Tolerances[detail]}°), {tiles.Count} tiles, {points} points");
    }

    // Runs inside the map's own viewport update: never let anything out.
    private void ApplyScale()
    {
        try
        {
            if (scales.Count == 0)
                return;
            float s = (float)(Map.ViewTransform.Scale * MetresPerDegree);
            if (s == scale || s <= 0)
                return;
            scale = s;
            foreach (ScaleTransform t in scales)
            {
                t.ScaleX = s;
                t.ScaleY = s;
            }
        }
        catch (Exception ex)
        {
            if (!loggedFailure)
            {
                loggedFailure = true;
                ModLog.Error($"Border lines failed: {ex}");
            }
        }
    }
}
