#if !FLATMAP
using System;
using System.Collections.Generic;
using MapControl;
using Noesis;
using SeaPowerBorders.Borders;
using GeoPosition = SeaPower.GeoPosition;

namespace SeaPowerBorders.Map;

/// <summary>
/// The border lines on the 3D-globe build. The mission map there is a globe (an orthographic projection), and a
/// lon/lat rectangle is no rectangle on it: the lines are projected point by point instead (<see cref="ProjectedLines"/>),
/// one path per line style in screen pixels, rebuilt from the pieces that meet the visible area whenever the view
/// changes. The editor's flat map (equirectangular) keeps the tile paths of <c>BorderLines.cs</c>.
/// <para>Only in the globe build (<c>FLATMAP</c> undefined); the flat-map build for Sea Power 0.8.7 has no such types.</para>
/// </summary>
internal sealed partial class BorderLines
{
    // Globe mode: screen-space paths, and what they were last drawn for.
    private const int MaxGlobePoints = 250000;                      // per rebuild; a safety net, normal views use a fraction
    private const float MinStepPixels = 0.4f;
    private const double GlobeRadiusMetres = 6378137.0;
    private bool globe;
    private PathSet solidSet, dashedSet, waterSet;
    private readonly List<BorderPiece> solidPieces = new List<BorderPiece>(), dashedPieces = new List<BorderPiece>();
    private readonly Dictionary<(int x, int y), SeaLimitTile> globeWaterTiles = new Dictionary<(int x, int y), SeaLimitTile>();
    private BorderData lastData;
    private HashSet<BorderLine> lastDrawn;
    private bool piecesStale = true, globeDirty = true, loggedBudget;
    private ViewKey viewKey;
    // The delegate for ProjectedLines, made on first use here (not in the constructor of BorderLines.cs, which the flat-map
    // build shares and which knows nothing of the globe).
    private Projector projectorCache;
    private Projector projector => projectorCache ??= Project;

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

    /// <summary>
    /// The paths of one line style in a panel. Noesis does not draw a path with thousands of points (round joins and dashes
    /// multiply them) - the lines of a wide view simply vanished - so the lines are cut into paths of a few hundred points
    /// each. The paths are made once and reused on every rebuild.
    /// </summary>
    private sealed class PathSet
    {
        private readonly Panel host;
        private readonly List<Path> paths = new List<Path>();
        private Brush stroke;
        private float thickness = 1f;
        private string dashes;
        private int used;

        public PathSet(Panel host) => this.host = host;

        public void Style(Brush stroke, float thickness, string dashes)
        {
            this.stroke = stroke;
            this.thickness = thickness;
            this.dashes = dashes;
            foreach (Path path in paths)
                Apply(path);
        }

        private void Apply(Path path)
        {
            path.Stroke = stroke;
            path.StrokeThickness = thickness;
            if (dashes != null)
                path.StrokeDashArray = dashes;
        }

        /// <summary>Forget the paths (the panel was cleared by the caller).</summary>
        public void Forget()
        {
            paths.Clear();
            used = 0;
        }

        public void Reset() => used = 0;

        public Path Next()
        {
            if (used == paths.Count)
            {
                var path = new Path { IsHitTestVisible = false, StrokeLineJoin = PenLineJoin.Round };
                Apply(path);
                host.Children.Add(path);
                paths.Add(path);
            }
            return paths[used++];
        }

        /// <summary>Empties the paths that were not used this time.</summary>
        public void Trim()
        {
            for (int i = used; i < paths.Count; i++)
                paths[i].Data = null;
        }
    }

    /// <summary>
    /// Appends the projected points to the paths of a <see cref="PathSet"/>, starting a new path between two lines once the
    /// current one has a few hundred points; points within a fraction of a pixel of the last one are dropped.
    /// </summary>
    private sealed class ChunkSink : ILineSink
    {
        private const int ChunkPoints = 600;

        private readonly PathSet set;
        private StreamGeometry geometry;
        private StreamGeometryContext context;
        private Path path;
        private Point pending, last;
        private bool hasPending;
        private int inChunk;
        public int Points;

        public ChunkSink(PathSet set)
        {
            this.set = set;
            set.Reset();
        }

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
                if (context == null || inChunk >= ChunkPoints)
                    Roll();
                context.BeginFigure(pending, false, false);
                hasPending = false;
            }
            else if (Math.Abs(x - last.X) + Math.Abs(y - last.Y) < MinStepPixels)
            {
                return;
            }
            last = new Point(x, y);
            context.LineTo(last, true, true);
            inChunk++;
            Points++;
        }

        private void Roll()
        {
            Close();
            path = set.Next();
            geometry = new StreamGeometry();
            context = geometry.Open();
            inChunk = 0;
        }

        private void Close()
        {
            if (context == null)
                return;
            context.Dispose();
            context = null;
            path.Data = geometry;
        }

        /// <summary>Hands the last geometry over and empties the paths that stay unused.</summary>
        public void Finish()
        {
            Close();
            set.Trim();
        }
    }

    // ---- what BorderLines.cs calls (partial methods: not there in the flat-map build) -------------------------

    partial void OnGlobeViewport() => RefreshGlobe();

    partial void CheckProjection()
    {
        bool onGlobe = Map.MapProjection is OrthographicProjection;
        if (onGlobe != globe)
            SwitchMode(onGlobe);
    }

    partial void StylesChanged()
    {
        piecesStale = globeDirty = true;
        StyleGlobe();
    }

    partial void GlobeStep(BorderData data, HashSet<BorderLine> drawn, SeaLimitCache waters, ref bool handled)
    {
        if (!globe)
            return;
        UpdateGlobe(data, drawn, waters);
        handled = true;
    }

    partial void ClearGlobeWaters()
    {
        if (globeWaterTiles.Count > 0)
        {
            globeWaterTiles.Clear();
            globeDirty = true;
        }
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
        solidSet?.Forget();
        dashedSet?.Forget();
        waterSet?.Forget();
        if (!globe)
        {
            panel.Opacity = Settings.LineOpacity.Value;     // the flat drawing takes its opacity from the panel
            return;
        }
        solidSet ??= new PathSet(panel);
        dashedSet ??= new PathSet(panel);
        waterSet ??= new PathSet(waterPanel);
        StyleGlobe();
        ModLog.Debug("Border lines: drawing for the globe.");
    }

    private void StyleGlobe()
    {
        if (solidSet == null)
            return;
        // The line opacity goes into the brush: a panel opacity below 1 makes Noesis draw the whole panel into an offscreen
        // picture first, which is wasteful for a full-screen map.
        panel.Opacity = 1f;
        byte alpha = (byte)Math.Round(255.0 * Settings.LineOpacity.Value);
        Brush lines = brush == null ? null : new SolidColorBrush(Color.FromArgb(alpha, brush.Color.R, brush.Color.G, brush.Color.B));
        solidSet.Style(lines, Settings.LineWidth.Value, null);
        dashedSet.Style(lines, Settings.LineWidth.Value, "6 3");
        waterSet.Style(waterBrush, Settings.LineWidth.Value * 0.8f, "3 3");
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
                new ChunkSink(solidSet).Finish();
                new ChunkSink(dashedSet).Finish();
                new ChunkSink(waterSet).Finish();
                return;
            }
            double step = ProjectedLines.StepDegrees(Map.ViewTransform.Scale * GlobeRadiusMetres);
            int points = FillBorders(dashedSet, dashedPieces, area, step) + FillBorders(solidSet, solidPieces, area, step) +
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

    private int FillBorders(PathSet set, List<BorderPiece> pieces, in GeoArea area, double step)
    {
        var sink = new ChunkSink(set);
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
        sink.Finish();
        return sink.Points;
    }

    private int FillWaters(in GeoArea area, double step)
    {
        var sink = new ChunkSink(waterSet);
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
        sink.Finish();
        return sink.Points;
    }

    private void LogBudget()
    {
        if (loggedBudget)
            return;
        loggedBudget = true;
        ModLog.Warn($"Border lines: more than {MaxGlobePoints} points in view, the rest is left out of this frame.");
    }
}
#endif
