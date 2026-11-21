using System;
using System.Collections.Generic;
using MapControl;
using Noesis;
using SeaPowerBorders.Borders;

namespace SeaPowerBorders.Map;

/// <summary>
/// The lines on the globe (the mission map, an orthographic projection). A lon/lat rectangle is no
/// rectangle there, so the lines are projected point by point instead (<see cref="ProjectedLines"/>, the pieces
/// chosen by <see cref="GlobeLines"/>): one set of paths per line style in screen pixels, rebuilt whenever the view
/// changes.
/// </summary>
internal sealed class GlobeDrawing : IBorderDrawing
{
    private readonly MapBase map;
    private readonly MapPanel panel, waterPanel;
    private readonly BorderLook look;
    private readonly BorderSelection lines;
    private readonly PathSet solidSet, dashedSet, waterSet;
    private readonly GlobeLines pieces = new GlobeLines();
    private readonly CoastalTiles<SeaLimitTile> coast;
    private readonly Projector project;
    private int level = -1;
    private bool dirty = true, loggedBudget;
    private ViewKey viewKey;
    private Throttle diagnostics;

    public GlobeDrawing(MapBase map, MapPanel panel, MapPanel waterPanel, BorderLook look, BorderSelection lines)
    {
        this.map = map;
        this.panel = panel;
        this.waterPanel = waterPanel;
        this.look = look;
        this.lines = lines;
        // A panel opacity below 1 makes Noesis draw the whole panel into an offscreen picture first, which is wasteful
        // for a full-screen map: the opacity goes into the brush.
        panel.Opacity = 1f;
        SolidColorBrush brush = look.LineBrush();
        solidSet = new PathSet(panel, brush, look.Width, null);
        dashedSet = new PathSet(panel, brush, look.Width, BorderLook.DashedPattern);
        waterSet = new PathSet(waterPanel, look.CoastalBrush(), look.CoastalWidth, BorderLook.CoastalPattern);
        coast = new CoastalTiles<SeaLimitTile>(() => dirty = true);
        project = (double lat, double lon, out float x, out float y) => MapView.ToView(map, lat, lon, out x, out y);
        Rig.Log.Debug("Border lines: drawing for the globe.");
    }

    public void Update(SeaLimitCache waters)
    {
        if (!MapView.HasSize(map, 2))
            return;
        double pixelsPerDegree = map.ViewTransform.Scale * Earth.MetresPerDegree;    // at the centre of the view
        if (pixelsPerDegree <= 0)
            return;
        int wanted = lines.Data.LevelFor(pixelsPerDegree);
        if (wanted != level)
        {
            level = wanted;
            pieces.Select(lines.Data, lines.Drawn, level, look.Disputed);
            dirty = true;
            Rig.Log.Debug($"Borders (globe): level {level} ({lines.Data.Tolerances[level]}°), {pieces.Solid.Count + pieces.Dashed.Count} pieces");
        }
        bool hasArea = TryArea(out GeoArea area);
        if (coast.Fetch(waters, look.CoastalNm, pixelsPerDegree, hasArea ? (area.West, area.South, area.East, area.North) : null,
                        hasArea && area.AllLongitudes, tile => tile))
            dirty = true;
        ViewportChanged();
    }

    // Projects the pieces that meet the visible area into the three path sets. Runs on every change of the view.
    public void ViewportChanged()
    {
        if (level < 0 || !MapView.HasSize(map, 2))
            return;
        ViewKey key = CurrentKey();
        if (!dirty && key.Equals(viewKey))
            return;
        viewKey = key;
        dirty = false;
        var solid = new ChunkSink(solidSet);
        var dashed = new ChunkSink(dashedSet);
        var water = new ChunkSink(waterSet);
        if (TryArea(out GeoArea area))
        {
            double step = ProjectedLines.StepDegrees(map.ViewTransform.Scale * Earth.RadiusMetres);
            GlobeLines.Fill(pieces.Dashed, area, project, step, dashed, LogBudget);
            GlobeLines.Fill(pieces.Solid, area, project, step, solid, LogBudget);
            GlobeLines.Fill(CoastalPieces(), area, project, step, water, LogBudget);
            if (diagnostics.Ready(4000))
                Rig.Log.Debug($"Border lines (globe): {solid.Points + dashed.Points + water.Points} points, step {step:0.###}°, view {area.South:0.#}..{area.North:0.#}N {area.West:0.#}..{area.East:0.#}E");
        }
        solid.Finish();
        dashed.Finish();
        water.Finish();
    }

    public void Clear()
    {
        panel.Children.Clear();
        waterPanel.Children.Clear();
    }

    private IEnumerable<GeoPiece> CoastalPieces()
    {
        foreach (SeaLimitTile tile in coast.Drawn)
        {
            if (coast.Level < 0 || coast.Level >= tile.Levels.Length)
                continue;
            foreach (SeaLimitPiece piece in tile.Levels[coast.Level])
                yield return piece;
        }
    }

    private bool TryArea(out GeoArea area)
    {
        BoundingBox view = MapView.VisibleBox(map);
        area = view == null ? default : new GeoArea(view.West, view.South, view.East, view.North);
        return view != null;
    }

    private ViewKey CurrentKey()
    {
        ViewTransform view = map.ViewTransform;
        Location centre = map.MapProjection.Center;
        Point origin = view.MapToView(new Point(0f, 0f));
        return new ViewKey(centre.Latitude, centre.Longitude, view.Scale, view.Rotation, origin.X, origin.Y, map.ActualWidth, map.ActualHeight);
    }

    private void LogBudget()
    {
        if (loggedBudget)
            return;
        loggedBudget = true;
        Rig.Log.Warn($"Border lines: more than {GlobeLines.MaxPoints} points in view, the rest is left out of this frame.");
    }

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
        private readonly Brush stroke;
        private readonly float thickness;
        private readonly string dashes;
        private int used;

        public PathSet(Panel host, Brush stroke, float thickness, string dashes)
        {
            this.host = host;
            this.stroke = stroke;
            this.thickness = thickness;
            this.dashes = dashes;
        }

        public void Reset() => used = 0;

        public Path Next()
        {
            if (used == paths.Count)
            {
                var path = new Path { IsHitTestVisible = false, StrokeLineJoin = PenLineJoin.Round, Stroke = stroke, StrokeThickness = thickness };
                if (dashes != null)
                    path.StrokeDashArray = dashes;
                host.Children.Add(path);
                paths.Add(path);
            }
            return paths[used++];
        }

        public void EmptyUnused()
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
        private const float MinStepPixels = 0.4f;
        private const float FarOffPixels = 1e7f;     // a point this far out makes Noesis drop the whole path

        private readonly PathSet set;
        private StreamGeometry geometry;
        private StreamGeometryContext context;
        private Path path;
        private Point pending, last;
        private bool hasPending;
        private int inChunk;

        public ChunkSink(PathSet set)
        {
            this.set = set;
            set.Reset();
        }

        public int Points { get; private set; }

        public void Begin(float x, float y)
        {
            pending = last = new Point(x, y);
            hasPending = true;
        }

        public void LineTo(float x, float y)
        {
            if (!(Math.Abs(x) < FarOffPixels && Math.Abs(y) < FarOffPixels))
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

        public void Finish()
        {
            Close();
            set.EmptyUnused();
        }
    }
}
