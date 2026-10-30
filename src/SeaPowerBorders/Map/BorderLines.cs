using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
/// <para>The coastal distance line lives in a second panel below the borders, the same way, one small path per
/// <see cref="SeaLimits.CellDegrees"/> cell as the tiles come in from <see cref="SeaLimitCache"/> - only around the
/// view and once zoomed in far enough that the line stands clear of the coast.</para>
/// <para>The 3D-globe build projects the lines point by point instead, see <c>BorderLines.Globe.cs</c> (this file is
/// the drawing of the flat map: Sea Power 0.8.7 and the editor's map).</para>
/// </summary>
internal sealed partial class BorderLines
{
    private const double MetresPerDegree = 111319.49079327358;    // the map's equirectangular projection
    private const double MinWaterOffset = 5.0;                     // pixels between coast and line before it shows

    public readonly MapBase Map;
    private readonly ObservableCollection<object> background;      // the editor's map: panels go in here, not in Children
    private readonly MapPanel panel;
    private readonly List<ScaleTransform> scales = new List<ScaleTransform>();
    private readonly EventHandler<ViewportChangedEventArgs> onViewport;
    private readonly MapPanel waterPanel;
    private readonly Dictionary<(int x, int y), List<ScaleTransform>> waterTiles = new Dictionary<(int x, int y), List<ScaleTransform>>();

    private int linesVersion = -1, settingsVersion = -1, level = -1, waterLevel = -1, waterNm = -1;
    private bool dark;
    private float scale = float.NaN;
    private SolidColorBrush brush, waterBrush;
    private bool loggedFailure;
    private int nextDiagnostics;

    /// <summary>Lays a border panel into the map, or returns null if the map isn't the panel we expect.</summary>
    public static BorderLines TryAttach(MapBase map, ObservableCollection<object> background = null)
    {
        try
        {
            return new BorderLines(map, background);
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Could not add the border lines to the map: {ex.Message}");
            return null;
        }
    }

    private BorderLines(MapBase map, ObservableCollection<object> background)
    {
        Map = map;
        this.background = background;
        panel = new MapPanel { IsHitTestVisible = false };
        MapPanel.SetParentMap(panel, map);
        MapSlots.Insert(map, panel, MapSlots.Borders, background);
        waterPanel = new MapPanel { IsHitTestVisible = false };
        MapPanel.SetParentMap(waterPanel, map);
        MapSlots.Insert(map, waterPanel, MapSlots.Waters, background);
        onViewport = (_, __) =>
        {
            ApplyScale();
            OnGlobeViewport();
        };
        map.ViewportChanged += onViewport;
    }

    // Hooks of the 3D-globe build (BorderLines.Globe.cs); without an implementation the calls are left out.
    partial void OnGlobeViewport();
    partial void CheckProjection();
    partial void StylesChanged();
    partial void GlobeStep(BorderData data, HashSet<BorderLine> drawn, SeaLimitCache waters, ref bool handled);
    partial void ClearGlobeWaters();

    public bool Alive => VisualTreeHelper.GetParent(Map) != null;

    public void Detach()
    {
        try
        {
            Map.ViewportChanged -= onViewport;
            MapSlots.Remove(Map, panel, background);
            MapPanel.SetParentMap(panel, null);
            MapSlots.Remove(Map, waterPanel, background);
            MapPanel.SetParentMap(waterPanel, null);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"Border detach: {ex.Message}");
        }
    }

    /// <param name="visible">The Borders layer is on.</param>
    /// <param name="data">All border lines; <paramref name="drawn"/> those of the mission's date, and
    /// <paramref name="version"/> changes when they do.</param>
    /// <param name="waters">Territorial-water tiles, null for none.</param>
    public void Update(bool visible, BorderData data, HashSet<BorderLine> drawn, int version, bool darkTheme, SeaLimitCache waters)
    {
        if (!visible || data == null || drawn == null)
        {
            if (panel.Visibility != Visibility.Collapsed)
                panel.Visibility = Visibility.Collapsed;
            if (waterPanel.Visibility != Visibility.Collapsed)
                waterPanel.Visibility = Visibility.Collapsed;
            return;
        }
        if (panel.Visibility != Visibility.Visible)
            panel.Visibility = Visibility.Visible;
        if (waterPanel.Visibility != Visibility.Visible)
            waterPanel.Visibility = Visibility.Visible;
        try
        {
            CheckProjection();
            if (version != linesVersion || darkTheme != dark || Settings.Version != settingsVersion)
            {
                linesVersion = version;
                dark = darkTheme;
                settingsVersion = Settings.Version;
                // Dashed lines in the same colour: a lighter tint got lost on the terrain (the Korean line by day).
                brush = new SolidColorBrush(dark ? Color.FromArgb(255, 205, 200, 215) : Color.FromArgb(255, 58, 50, 72));
                // The coastal line gets its opacity in its brush, not as a panel opacity: that would have Noesis draw
                // all its paths into an offscreen picture first.
                byte alpha = (byte)Math.Round(255.0 * Settings.LineOpacity.Value);
                waterBrush = new SolidColorBrush(dark ? Color.FromArgb(alpha, 140, 180, 215) : Color.FromArgb(alpha, 45, 90, 140));
                panel.Opacity = Settings.LineOpacity.Value;
                level = -1;
                ClearWaters();
                StylesChanged();
            }
            bool handled = false;
            GlobeStep(data, drawn, waters, ref handled);
            if (handled)
                return;
            ApplyScale();
            double pixelsPerDegree = Map.ViewTransform.Scale * MetresPerDegree;
            if (pixelsPerDegree <= 0)
                return;
            int wanted = data.LevelFor(pixelsPerDegree);
            if (wanted != level)
            {
                Build(data, drawn, wanted);
                level = wanted;
            }
            UpdateWaters(waters, pixelsPerDegree);
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

    // The tiles around the view that have come in: one dashed path each, at the zoom's level of detail.
    private void UpdateWaters(SeaLimitCache waters, double pixelsPerDegree)
    {
        int nm = Settings.CoastalLine.Value;
        if (waters == null || waters.Failed || nm <= 0 || nm / 60.0 * pixelsPerDegree < MinWaterOffset)
        {
            ClearWaters();
            return;
        }
        int wanted = SeaLimits.LevelFor(pixelsPerDegree);
        if (wanted != waterLevel || nm != waterNm)
        {
            ClearWaters();
            waterLevel = wanted;
            waterNm = nm;
        }
        if (Map.ActualWidth <= 0 || Map.ActualHeight <= 0)
            return;
        BoundingBox view = Map.ViewRectToBoundingBox(new Rect(0, 0, Map.ActualWidth, Map.ActualHeight));
        if (view == null)
            return;
        // The view and a quarter of it around, so a short pan finds its tiles drawn.
        double padX = (view.East - view.West) / 4, padY = (view.North - view.South) / 4;
        int x0 = (int)Math.Floor((view.West - padX + 180.0) / BorderData.Tile), x1 = (int)Math.Floor((view.East + padX + 180.0) / BorderData.Tile);
        int y0 = Math.Max(0, (int)Math.Floor((view.South - padY + 90.0) / BorderData.Tile));
        int y1 = Math.Min(17, (int)Math.Floor((view.North + padY + 90.0) / BorderData.Tile));
        if (x1 - x0 > 35)
            x1 = x0 + 35;
        for (int tx = x0; tx <= x1; tx++)
        {
            int x = ((tx % 36) + 36) % 36;
            for (int y = y0; y <= y1; y++)
            {
                if (!SeaLimits.Covered(y) || waterTiles.ContainsKey((x, y)))
                    continue;
                SeaLimitTile tile = waters.Get(x, y, nm);
                if (tile != null)
                    waterTiles[(x, y)] = AddWaterPaths(tile, wanted);
            }
        }
        if (Settings.VerboseLog.Value && Environment.TickCount - nextDiagnostics >= 0)
        {
            nextDiagnostics = Environment.TickCount + 4000;
            LogWaterDiagnostics(view);
        }
    }

    // What the map did with the cell paths nearest the view's centre: size, whether Noesis counts them visible, and
    // where their top-left corner is on the map control (to find out why a line goes missing at some positions).
    private void LogWaterDiagnostics(BoundingBox view)
    {
        try
        {
            double cx = (view.West + view.East) / 2.0, cy = (view.South + view.North) / 2.0;
            var near = new List<(double d, Path path)>();
            foreach (UIElement child in waterPanel.Children)
            {
                if (child is not Path path)
                    continue;
                BoundingBox box = MapPanel.GetBoundingBox(path);
                double d = Math.Abs((box.West + box.East) / 2.0 - cx) + Math.Abs((box.South + box.North) / 2.0 - cy);
                near.Add((d, path));
            }
            near.Sort((a, b) => a.d.CompareTo(b.d));
            var text = new System.Text.StringBuilder($"Coastal line: {near.Count} cell paths, scale {scale:0.0} px/deg, map {Map.ActualWidth:0}x{Map.ActualHeight:0}");
            for (int i = 0; i < Math.Min(5, near.Count); i++)
            {
                Path path = near[i].path;
                BoundingBox box = MapPanel.GetBoundingBox(path);
                Point at = path.TranslatePoint(new Point(0, 0), Map);
                text.Append($" | cell W{box.West:0.##} N{box.North:0.##}: size {path.ActualWidth:0}x{path.ActualHeight:0}, at {at.X:0},{at.Y:0}, visible {path.IsVisible}");
            }
            ModLog.Debug(text.ToString());
        }
        catch (Exception ex)
        {
            ModLog.Debug($"Coastal line diagnostics: {ex.Message}");
        }
    }

    // One small path per cell of the tile, each placed by its own bounding box. Null when the tile has no line at
    // this level (it is still remembered as done).
    private List<ScaleTransform> AddWaterPaths(SeaLimitTile tile, int detail)
    {
        List<SeaLimitPiece> pieces = tile.Levels[detail];
        if (pieces.Count == 0)
            return null;
        ApplyScale();
        float s = float.IsNaN(scale) ? (float)(Map.ViewTransform.Scale * MetresPerDegree) : scale;
        const float cell = (float)SeaLimits.CellDegrees;
        var cells = new Dictionary<(int x, int y), List<SeaLimitPiece>>();
        foreach (SeaLimitPiece piece in pieces)
        {
            if (!cells.TryGetValue((piece.Cx, piece.Cy), out List<SeaLimitPiece> list))
                cells[(piece.Cx, piece.Cy)] = list = new List<SeaLimitPiece>();
            list.Add(piece);
        }
        var transforms = new List<ScaleTransform>();
        var line = new List<Point>();
        foreach (KeyValuePair<(int x, int y), List<SeaLimitPiece>> kv in cells)
        {
            float west = kv.Key.x * cell - 180f, south = kv.Key.y * cell - 90f, north = south + cell;
            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                foreach (SeaLimitPiece piece in kv.Value)
                {
                    float[] p = piece.Points;
                    context.BeginFigure(new Point(p[0] - west, north - p[1]), false, false);
                    line.Clear();
                    for (int k = 2; k < p.Length; k += 2)
                        line.Add(new Point(p[k] - west, north - p[k + 1]));
                    context.PolyLineTo(line, true, true);
                }
            }
            var transform = new ScaleTransform(s, s);
            geometry.Transform = transform;
            transforms.Add(transform);
            var path = new Path
            {
                Data = geometry,
                Stroke = waterBrush,
                StrokeThickness = Settings.LineWidth.Value * 0.8f,
                StrokeDashArray = "3 3",
                StrokeLineJoin = PenLineJoin.Round,
                IsHitTestVisible = false,
            };
            MapPanel.SetBoundingBox(path, new BoundingBox(south, west, north, west + cell));
            waterPanel.Children.Add(path);
        }
        return transforms;
    }

    private void ClearWaters()
    {
        ClearGlobeWaters();
        if (waterTiles.Count == 0)
            return;
        waterPanel.Children.Clear();
        waterTiles.Clear();
    }

    // Runs inside the map's own viewport update: never let anything out.
    private void ApplyScale()
    {
        try
        {
            if (scales.Count == 0 && waterTiles.Count == 0)
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
            foreach (List<ScaleTransform> transforms in waterTiles.Values)
            {
                if (transforms == null)
                    continue;
                foreach (ScaleTransform t in transforms)
                {
                    t.ScaleX = s;
                    t.ScaleY = s;
                }
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
