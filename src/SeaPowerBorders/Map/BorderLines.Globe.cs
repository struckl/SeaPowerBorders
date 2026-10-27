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
}
#endif
