using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Path = System.IO.Path;
using MapControl;
using Noesis;
using SeaPowerBorders.Borders;
using SeaPowerBorders.Map;

namespace SeaPowerBorders;

/// <summary>Stand-in for the mod's log.</summary>
internal static class ModLog
{
    public static void Warn(string m) => Console.WriteLine("WARN " + m);
}

/// <summary>
/// Offline check of the globe drawing, against the game's own <c>OrthographicProjection</c> (no game needed):
/// <code>dotnet run --project tools/globe-check -c Release -- --check
/// dotnet run --project tools/globe-check -c Release -- --view 70.3,20,60[,heading] [--size 1920x1080] [--date 1985-06-26] --out globe.tsv
/// python -I tools/globe-check/draw.py globe.tsv globe.png</code>
/// --check runs the accuracy, horizon and timing checks; --view writes the segments one view draws, for draw.py. The
/// lines are those of the date (default 1985-06-26), picked and projected by the mod's own GlobeLines.
/// </summary>
internal static class Program
{
    private sealed class Polylines : ILineSink
    {
        public readonly List<List<(float x, float y)>> Lines = new List<List<(float x, float y)>>();
        public int Points { get; private set; }

        public void Begin(float x, float y) => Lines.Add(new List<(float x, float y)> { (x, y) });

        public void LineTo(float x, float y)
        {
            Lines[Lines.Count - 1].Add((x, y));
            Points++;
        }
    }

    /// <summary>One view of the globe as the map control would have it: the projection turned to a centre, the view transform.</summary>
    private sealed class View
    {
        public readonly OrthographicProjection Projection;
        public readonly ViewTransform Transform = new ViewTransform();
        public readonly int Width, Height;
        public readonly double PixelsPerDegree;
        public readonly Projector Project;

        public View(double lat, double lon, double pixelsPerDegree, double heading, int width, int height)
        {
            Width = width;
            Height = height;
            PixelsPerDegree = pixelsPerDegree;
            Projection = new OrthographicProjection { Center = new Location(lat, lon) };
            Point centre = Projection.LocationToMap(new Location(lat, lon));
            Transform.SetTransform(centre, new Point(width / 2f, height / 2f), pixelsPerDegree / Earth.MetresPerDegree, heading);
            Project = (double la, double lo, out float x, out float y) =>
            {
                bool visible = Projection.TryLocationToMap(la, lo, out Point map);
                Point view = Transform.MapToView(map);
                x = view.X;
                y = view.Y;
                return visible;
            };
        }

        public double GlobeRadiusPixels => Transform.Scale * Earth.RadiusMetres;

        public GeoArea Area()
        {
            BoundingBox box = Projection.GetVisibleGeoBounds(Transform, new Size(Width, Height));
            return box == null ? throw new InvalidOperationException("no visible bounds") : new GeoArea(box.West, box.South, box.East, box.North);
        }
    }

    private static int Main(string[] args)
    {
        string bordersDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SeaPowerBorders", "mod", "borders"));
        string viewText = null, output = null;
        DateTime date = new DateTime(1985, 6, 26);
        bool check = false;
        int width = 1920, height = 1080;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--check") check = true;
            else if (args[i] == "--borders") bordersDir = args[++i];
            else if (args[i] == "--view") viewText = args[++i];
            else if (args[i] == "--out") output = args[++i];
            else if (args[i] == "--date") date = DateTime.ParseExact(args[++i], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            else if (args[i] == "--size")
            {
                string[] s = args[++i].Split('x');
                width = int.Parse(s[0]);
                height = int.Parse(s[1]);
            }
        }
        BorderData data = BorderData.Load(Path.Combine(bordersDir, "borders.bin"));
        BorderEras eras = BorderEras.Load(bordersDir);
        var drawn = new HashSet<BorderLine>(data.Lines.Where(l => eras.Drawn(l, date, BorderEra.MissionDate)));
        Console.WriteLine($"borders.bin: {data.Lines.Count} lines ({drawn.Count} for {date:yyyy-MM-dd}), {data.Pieces.Count} pieces, levels {string.Join(", ", data.Tolerances)}");
        var lines = new Lines(data, drawn);
        int failures = 0;
        if (check)
            failures += Check(lines);
        if (viewText != null)
            Draw(lines, viewText, width, height, output);
        return failures == 0 ? 0 : 1;
    }

    /// <summary>The border lines of the date, and the pieces GlobeLines picks for a level (dashed ones included).</summary>
    private sealed class Lines
    {
        public readonly BorderData Data;
        private readonly HashSet<BorderLine> drawn;

        public Lines(BorderData data, HashSet<BorderLine> drawn)
        {
            Data = data;
            this.drawn = drawn;
        }

        public GlobeLines For(int level)
        {
            var pieces = new GlobeLines();
            pieces.Select(Data, drawn, level, disputed: true);
            return pieces;
        }
    }

    // One view drawn the way GlobeDrawing does it: the pieces of the view's level, dashed then solid, each style
    // under the point budget; the timing includes the pick. Both styles end up in one set of polylines here.
    private static (int points, int pieces, double milliseconds, Polylines lines) Rebuild(Lines data, View view)
    {
        var clock = Stopwatch.StartNew();
        GlobeLines pieces = data.For(data.Data.LevelFor(view.PixelsPerDegree));
        GeoArea area = view.Area();
        double step = ProjectedLines.StepDegrees(view.GlobeRadiusPixels);
        var dashed = new Polylines();
        var solid = new Polylines();
        GlobeLines.Fill(pieces.Dashed, area, view.Project, step, dashed, OverBudget);
        GlobeLines.Fill(pieces.Solid, area, view.Project, step, solid, OverBudget);
        clock.Stop();
        dashed.Lines.AddRange(solid.Lines);
        int met = pieces.Dashed.Count(p => area.Meets(p.Bounds)) + pieces.Solid.Count(p => area.Meets(p.Bounds));
        return (dashed.Points + solid.Points, met, clock.Elapsed.TotalMilliseconds, dashed);
    }

    private static void OverBudget() => Console.WriteLine($"  over the point budget ({GlobeLines.MaxPoints}): the rest is left out");

    private static void Draw(Lines data, string viewText, int width, int height, string output)
    {
        string[] v = viewText.Split(',');
        double lat = double.Parse(v[0], CultureInfo.InvariantCulture), lon = double.Parse(v[1], CultureInfo.InvariantCulture);
        double ppd = double.Parse(v[2], CultureInfo.InvariantCulture);
        double heading = v.Length > 3 ? double.Parse(v[3], CultureInfo.InvariantCulture) : 0;
        var view = new View(lat, lon, ppd, heading, width, height);
        var (points, pieces, ms, lines) = Rebuild(data, view);
        Console.WriteLine($"view {lat},{lon} {ppd} px/deg: level {data.Data.LevelFor(ppd)}, {pieces} pieces, {lines.Lines.Count} figures, {points} points, {ms:0.0} ms");
        // What the renderer is handed: a single NaN or absurd coordinate can make it drop the whole path.
        int bad = 0;
        float extreme = 0;
        foreach (List<(float x, float y)> line in lines.Lines)
        {
            foreach ((float x, float y) in line)
            {
                if (!float.IsFinite(x) || !float.IsFinite(y))
                    bad++;
                else
                    extreme = Math.Max(extreme, Math.Max(Math.Abs(x), Math.Abs(y)));
            }
        }
        Console.WriteLine($"  coordinates: {bad} not finite, largest |value| {extreme:0} px (globe radius {view.GlobeRadiusPixels:0} px, view {width}x{height})");
        if (output == null)
            return;
        using var w = new StreamWriter(output);
        w.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{width}\t{height}\t{view.GlobeRadiusPixels:0.###}"));
        foreach (List<(float x, float y)> line in lines.Lines)
            w.WriteLine(string.Join("\t", line.ConvertAll(p => $"{p.x.ToString("0.##", CultureInfo.InvariantCulture)},{p.y.ToString("0.##", CultureInfo.InvariantCulture)}")));
    }

    private static int Check(Lines data)
    {
        int failures = 0;
        void Report(bool ok, string text)
        {
            Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {text}");
            if (!ok)
                failures++;
        }

        Console.WriteLine("Projection: the game's OrthographicProjection against the textbook formula");
        var rnd = new Random(7);
        double worst = 0;
        int mismatchedHorizon = 0;
        var view = new View(52, 8, 20, 25, 1920, 1080);
        for (int i = 0; i < 5000; i++)
        {
            double la = rnd.NextDouble() * 180 - 90, lo = rnd.NextDouble() * 360 - 180;
            bool visible = view.Project(la, lo, out float x, out float y);
            double phi = la * Math.PI / 180, dl = (lo - 8) * Math.PI / 180, phi0 = 52 * Math.PI / 180;
            double cosC = Math.Sin(phi0) * Math.Sin(phi) + Math.Cos(phi0) * Math.Cos(phi) * Math.Cos(dl);
            if ((cosC >= 0) != visible)
                mismatchedHorizon++;
            if (!visible)
                continue;
            double ex = Math.Cos(phi) * Math.Sin(dl), ny = Math.Cos(phi0) * Math.Sin(phi) - Math.Sin(phi0) * Math.Cos(phi) * Math.Cos(dl);
            double r = view.GlobeRadiusPixels, a = 25 * Math.PI / 180;
            // north up, y down, then the heading turns the picture clockwise about the view centre
            double px = ex * r, py = -ny * r;
            double rx = px * Math.Cos(a) - py * Math.Sin(a) + 960, ry = px * Math.Sin(a) + py * Math.Cos(a) + 540;
            worst = Math.Max(worst, Math.Max(Math.Abs(rx - x), Math.Abs(ry - y)));
        }
        Report(mismatchedHorizon == 0, $"horizon test agrees with the formula ({mismatchedHorizon} of 5000 differ)");
        Report(worst < 0.05, $"screen positions within {worst:0.0000} px of the formula (heading 25°)");

        Console.WriteLine("Chords: a straight lon/lat stretch against the curve it follows on the globe");
        foreach (double ppd in new[] { 6.0, 40.0, 300.0, 3000.0 })
        {
            var v = new View(48, -109, ppd, 0, 1920, 1080);
            var sink = new Polylines();
            double step = ProjectedLines.StepDegrees(v.GlobeRadiusPixels);
            ProjectedLines.Add(new float[] { -123f, 49f, -95f, 49f }, v.Project, step, sink);
            double deviation = 0;
            for (double lo = -123; lo <= -95; lo += 0.002)
            {
                if (!v.Project(49, lo, out float x, out float y))
                    continue;
                double best = double.MaxValue;
                foreach (List<(float x, float y)> line in sink.Lines)
                {
                    for (int k = 1; k < line.Count; k++)
                        best = Math.Min(best, Distance(x, y, line[k - 1], line[k]));
                }
                deviation = Math.Max(deviation, best);
            }
            Report(deviation < 0.6, $"49°N, {ppd} px/deg: {sink.Points} points, step {step:0.###}°, worst distance to the curve {deviation:0.00} px");
        }

        Console.WriteLine("Horizon: a line that runs over the limb");
        {
            var v = new View(0, 0, 6, 0, 1920, 1080);
            var sink = new Polylines();
            ProjectedLines.Add(new float[] { 0f, -20f, 120f, -20f, 120f, 40f, 30f, 40f }, v.Project, ProjectedLines.StepDegrees(v.GlobeRadiusPixels), sink);
            double r = v.GlobeRadiusPixels, worstEdge = 0;
            int ends = 0;
            foreach (List<(float x, float y)> line in sink.Lines)
            {
                foreach (var end in new[] { line[0], line[line.Count - 1] })
                {
                    double d = Math.Sqrt((end.x - 960) * (end.x - 960) + (end.y - 540) * (end.y - 540));
                    if (d > r * 0.95)    // an end near the rim is where the line was cut
                    {
                        worstEdge = Math.Max(worstEdge, Math.Abs(d - r));
                        ends++;
                    }
                }
            }
            Report(sink.Lines.Count >= 1 && ends >= 1, $"{sink.Lines.Count} figure(s), {ends} cut end(s) at the rim");
            Report(worstEdge < 0.7, $"cut ends within {worstEdge:0.00} px of the limb circle (radius {r:0.0} px)");
        }

        Console.WriteLine("Time: the plugin's rebuild (cull, subdivide, project) per view; the borders as drawn on every view change");
        foreach (var (name, la, lo, ppd) in new[]
                 {
                     ("whole globe", 20.0, 20.0, 5.5), ("Europe", 52.0, 15.0, 30.0), ("Arctic Norway", 70.3, 20.0, 120.0),
                     ("Gulf of Finland", 60.0, 26.0, 600.0), ("harbour", 59.9, 30.3, 6000.0), ("the pole", 89.0, 0.0, 20.0),
                 })
        {
            var v = new View(la, lo, ppd, 0, 3840, 2160);
            var (points, pieces, ms, lines) = Rebuild(data, v);
            double best = ms;
            for (int k = 0; k < 4; k++)
                best = Math.Min(best, Rebuild(data, v).milliseconds);
            Report(best < 12, $"{name,-16} {ppd,6} px/deg: level {data.Data.LevelFor(ppd)}, {pieces} pieces, {lines.Lines.Count} figures, {points} points, {best:0.0} ms (4K view)");
        }
        return failures;
    }

    private static double Distance(double px, double py, (float x, float y) a, (float x, float y) b)
    {
        double dx = b.x - a.x, dy = b.y - a.y, len2 = dx * dx + dy * dy;
        double t = len2 == 0 ? 0 : Math.Max(0, Math.Min(1, ((px - a.x) * dx + (py - a.y) * dy) / len2));
        double cx = a.x + t * dx, cy = a.y + t * dy;
        return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }
}
