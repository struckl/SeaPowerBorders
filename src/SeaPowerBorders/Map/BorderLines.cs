using System;
using System.Collections.Generic;
using MapControl;
using Noesis;
using SeaPowerBorders.Borders;
using GeoPosition = SeaPower.GeoPosition;

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
/// <para><b>On the globe</b> (the mission map of the 3D-globe build, an orthographic projection) a lon/lat rectangle is
/// no rectangle, so there the lines are projected point by point instead (<see cref="ProjectedLines"/>): one path per
/// line style in screen pixels, rebuilt from the pieces that meet the visible area whenever the view changes.</para>
/// </summary>
internal sealed class BorderLines
{
    private const double MetresPerDegree = 111319.49079327358;    // the map's equirectangular projection
    private const double MinWaterOffset = 5.0;                     // pixels between coast and line before it shows

    public readonly MapBase Map;
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

    // Globe mode: screen-space paths, and what they were last drawn for.
    private const int MaxGlobePoints = 250000;                      // per rebuild; a safety net, normal views use a fraction
    private const float MinStepPixels = 0.4f;
    private const double GlobeRadiusMetres = 6378137.0;
    private bool globe;
    private Path globeSolid, globeDashed, globeWater;
    private readonly List<BorderPiece> solidPieces = new List<BorderPiece>(), dashedPieces = new List<BorderPiece>();
    private readonly Dictionary<(int x, int y), SeaLimitTile> globeWaterTiles = new Dictionary<(int x, int y), SeaLimitTile>();
    private BorderData lastData;
    private HashSet<BorderLine> lastDrawn;
    private bool piecesStale = true, globeDirty = true, loggedBudget;
    private ViewKey viewKey;
    private readonly Projector projector;

    /// <summary>The view as far as it matters for the globe's paths: changes whenever a projected point moves.</summary>
    private readonly struct ViewKey : IEquatable<ViewKey>
    {
        private readonly double lat, lon, scale, rotation, originX, originY, width, height;

        public ViewKey(double lat, double lon, double scale, double rotation, double originX, double originY, double width, double height)
        {
            this.lat = lat; this.lon = lon; this.scale = scale; this.rotation = rotation;
            this.originX = originX; this.originY = originY; this.width = width; this.height = height;
        }

        public bool Equals(ViewKey o) => lat == o.lat && lon == o.lon && scale == o.scale && rotation == o.rotation &&
                                         originX == o.originX && originY == o.originY && width == o.width && height == o.height;

        public override bool Equals(object obj) => obj is ViewKey k && Equals(k);

        public override int GetHashCode() => HashCode.Combine(lat, lon, scale, rotation);
    }

    /// <summary>Appends the projected points to a stream geometry; points within a fraction of a pixel of the last one are dropped.</summary>
    private sealed class GeometrySink : ILineSink
    {
        private readonly StreamGeometryContext context;
        private Point pending, last;
        private bool hasPending;
        public int Points;

        public GeometrySink(StreamGeometryContext context) => this.context = context;

        public void Begin(float x, float y)
        {
            pending = last = new Point(x, y);
            hasPending = true;
        }

        public void LineTo(float x, float y)
        {
            if (!(Math.Abs(x) < 1e7f && Math.Abs(y) < 1e7f))
                return;
            if (hasPending)
            {
                context.BeginFigure(pending, false, false);
                hasPending = false;
            }
            else if (Math.Abs(x - last.X) + Math.Abs(y - last.Y) < MinStepPixels)
            {
                return;
            }
            last = new Point(x, y);
            context.LineTo(last, true, true);
            Points++;
        }
    }

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
        waterPanel = new MapPanel { IsHitTestVisible = false };
        MapPanel.SetParentMap(waterPanel, map);
        MapSlots.Insert(map, waterPanel, MapSlots.Waters);
        projector = Project;
        // Flat maps: the stroke scale; the globe: the paths are projected again for the new view.
        onViewport = (_, __) =>
        {
            ApplyScale();
            RefreshGlobe();
        };
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
            Map.Children.Remove(waterPanel);
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
            bool onGlobe = Map.MapProjection is OrthographicProjection;
            if (onGlobe != globe)
                SwitchMode(onGlobe);
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
                piecesStale = globeDirty = true;
                StyleGlobe();
            }
            if (globe)
            {
                UpdateGlobe(data, drawn, waters);
                return;
            }
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
        if (globeWaterTiles.Count > 0)
        {
            globeWaterTiles.Clear();
            globeDirty = true;
        }
        if (waterTiles.Count == 0)
            return;
        waterPanel.Children.Clear();
        waterTiles.Clear();
    }

    // ---- the globe ----------------------------------------------------------------------------------------

    // The map changed its projection (or this is the first look at it): drop what the other mode put into the panels.
    private void SwitchMode(bool toGlobe)
    {
        panel.Children.Clear();
        scales.Clear();
        waterPanel.Children.Clear();
        waterTiles.Clear();
        globeWaterTiles.Clear();
        level = waterLevel = waterNm = -1;
        scale = float.NaN;
        globe = toGlobe;
        piecesStale = globeDirty = true;
        if (!globe)
            return;
        globeDashed ??= NewGlobePath();
        globeSolid ??= NewGlobePath();
        globeWater ??= NewGlobePath();
        panel.Children.Add(globeDashed);
        panel.Children.Add(globeSolid);
        waterPanel.Children.Add(globeWater);
        StyleGlobe();
        ModLog.Debug("Border lines: drawing for the globe.");
    }

    private static Path NewGlobePath() => new Path { IsHitTestVisible = false, StrokeLineJoin = PenLineJoin.Round };

    private void StyleGlobe()
    {
        if (globeSolid == null)
            return;
        globeSolid.Stroke = globeDashed.Stroke = brush;
        globeSolid.StrokeThickness = globeDashed.StrokeThickness = Settings.LineWidth.Value;
        globeDashed.StrokeDashArray = "6 3";
        globeWater.Stroke = waterBrush;
        globeWater.StrokeThickness = Settings.LineWidth.Value * 0.8f;
        globeWater.StrokeDashArray = "3 3";
    }

    private void UpdateGlobe(BorderData data, HashSet<BorderLine> drawn, SeaLimitCache waters)
    {
        lastData = data;
        lastDrawn = drawn;
        if (Map.ActualWidth < 2 || Map.ActualHeight < 2)
            return;
        double pixelsPerDegree = Map.ViewTransform.Scale * MetresPerDegree;    // at the centre of the view
        if (pixelsPerDegree <= 0)
            return;
        int wanted = data.LevelFor(pixelsPerDegree);
        if (wanted != level)
        {
            level = wanted;
            piecesStale = true;
        }
        if (piecesStale)
        {
            PreparePieces(data, drawn);
            piecesStale = false;
            globeDirty = true;
        }
        if (TryArea(out GeoArea area))
            PollGlobeWaters(waters, pixelsPerDegree, area);
        RefreshGlobe();
    }

    // The pieces of this level and date, by line style (dashed ones only with "Disputed lines" on).
    private void PreparePieces(BorderData data, HashSet<BorderLine> drawn)
    {
        solidPieces.Clear();
        dashedPieces.Clear();
        bool disputed = Settings.DisputedLines.Value;
        foreach (BorderPiece piece in data.Pieces)
        {
            if (piece.Level != level || !drawn.Contains(piece.Line))
                continue;
            if (!piece.Line.Dashed)
                solidPieces.Add(piece);
            else if (disputed)
                dashedPieces.Add(piece);
        }
        ModLog.Debug($"Borders (globe): level {level} ({data.Tolerances[level]}°), {solidPieces.Count + dashedPieces.Count} pieces");
    }

    private bool TryArea(out GeoArea area)
    {
        BoundingBox view = Map.MapProjection.GetVisibleGeoBounds(Map.ViewTransform, new Size((float)Map.ActualWidth, (float)Map.ActualHeight));
        area = view == null ? default : new GeoArea(view.South, view.North, view.West, view.East);
        return view != null;
    }

    private ViewKey CurrentKey()
    {
        ViewTransform view = Map.ViewTransform;
        Location centre = Map.MapProjection.Center;
        Point origin = view.MapToView(new Point(0f, 0f));
        return new ViewKey(centre.Latitude, centre.Longitude, view.Scale, view.Rotation, origin.X, origin.Y, Map.ActualWidth, Map.ActualHeight);
    }

    private bool Project(double lat, double lon, out float x, out float y)
    {
        bool visible = Map.TryLocationToView(new GeoPosition(lat, lon), out Point point);
        x = point.X;
        y = point.Y;
        return visible;
    }

    // The coastal line: the 10° tiles around the view, as the cache delivers them (it works on a worker thread).
    private void PollGlobeWaters(SeaLimitCache waters, double pixelsPerDegree, in GeoArea area)
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
        int x0 = 0, x1 = 35;
        if (!area.AllLongitudes)
        {
            x0 = (int)Math.Floor((area.West + 180.0) / BorderData.Tile);
            x1 = Math.Min((int)Math.Floor((area.East + 180.0) / BorderData.Tile), x0 + 35);
        }
        int y0 = Math.Max(0, (int)Math.Floor((area.South + 90.0) / BorderData.Tile));
        int y1 = Math.Min(17, (int)Math.Floor((area.North + 90.0) / BorderData.Tile));
        for (int tx = x0; tx <= x1; tx++)
        {
            int x = ((tx % 36) + 36) % 36;
            for (int y = y0; y <= y1; y++)
            {
                if (!SeaLimits.Covered(y) || globeWaterTiles.ContainsKey((x, y)))
                    continue;
                SeaLimitTile tile = waters.Get(x, y, nm);
                if (tile != null)
                {
                    globeWaterTiles[(x, y)] = tile;
                    globeDirty = true;
                }
            }
        }
    }

    // Projects the pieces that meet the visible area into the three paths. Runs on every change of the view.
    private void RefreshGlobe()
    {
        try
        {
            if (!globe || lastData == null || lastDrawn == null || piecesStale || Map.ActualWidth < 2 || Map.ActualHeight < 2)
                return;
            ViewKey key = CurrentKey();
            if (!globeDirty && key.Equals(viewKey))
                return;
            viewKey = key;
            globeDirty = false;
            if (!TryArea(out GeoArea area))
            {
                globeSolid.Data = globeDashed.Data = globeWater.Data = null;
                return;
            }
            double step = ProjectedLines.StepDegrees(Map.ViewTransform.Scale * GlobeRadiusMetres);
            int points = FillBorders(globeDashed, dashedPieces, area, step) + FillBorders(globeSolid, solidPieces, area, step) +
                         FillWaters(area, step);
            if (Settings.VerboseLog.Value && Environment.TickCount - nextDiagnostics >= 0)
            {
                nextDiagnostics = Environment.TickCount + 4000;
                ModLog.Debug($"Border lines (globe): {points} points, step {step:0.###}°, view {area.South:0.#}..{area.North:0.#}N {area.West:0.#}..{area.East:0.#}E");
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

    private int FillBorders(Path path, List<BorderPiece> pieces, in GeoArea area, double step)
    {
        var geometry = new StreamGeometry();
        int count;
        using (StreamGeometryContext context = geometry.Open())
        {
            var sink = new GeometrySink(context);
            foreach (BorderPiece piece in pieces)
            {
                if (!area.Meets(piece.Bounds))
                    continue;
                ProjectedLines.Add(piece.Points, projector, step, sink);
                if (sink.Points > MaxGlobePoints)
                {
                    LogBudget();
                    break;
                }
            }
            count = sink.Points;
        }
        path.Data = geometry;
        return count;
    }

    private int FillWaters(in GeoArea area, double step)
    {
        var geometry = new StreamGeometry();
        int count;
        using (StreamGeometryContext context = geometry.Open())
        {
            var sink = new GeometrySink(context);
            foreach (SeaLimitTile tile in globeWaterTiles.Values)
            {
                if (waterLevel < 0 || waterLevel >= tile.Levels.Length)
                    continue;
                foreach (SeaLimitPiece piece in tile.Levels[waterLevel])
                {
                    if (!area.Meets(piece.Bounds))
                        continue;
                    ProjectedLines.Add(piece.Points, projector, step, sink);
                }
                if (sink.Points > MaxGlobePoints)
                {
                    LogBudget();
                    break;
                }
            }
            count = sink.Points;
        }
        globeWater.Data = geometry;
        return count;
    }

    private void LogBudget()
    {
        if (loggedBudget)
            return;
        loggedBudget = true;
        ModLog.Warn($"Border lines: more than {MaxGlobePoints} points in view, the rest is left out of this frame.");
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
