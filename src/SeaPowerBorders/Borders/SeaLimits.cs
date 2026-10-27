using System;
using System.Collections.Generic;

namespace SeaPowerBorders.Borders;

/// <summary>A stretch of the line inside one <see cref="SeaLimits.CellDegrees"/> cell: its cell and the points.</summary>
internal sealed class SeaLimitPiece
{
    /// <summary>Cell numbers from 180°W and 90°S.</summary>
    public int Cx, Cy;

    /// <summary>Longitude, latitude, longitude, latitude, ...</summary>
    public float[] Points;

    private float[] bounds;

    /// <summary>West, south, east, north of the points (worked out once, for the globe's culling).</summary>
    public float[] Bounds => bounds ??= Map.ProjectedLines.Bounds(Points);
}

/// <summary>The line a set distance off the coast in one 10° tile, at a few levels of detail.</summary>
internal sealed class SeaLimitTile
{
    public int TileX, TileY;

    /// <summary>Per level of <see cref="SeaLimits.Tolerances"/>: the line cut into cells.</summary>
    public List<SeaLimitPiece>[] Levels;

    public int Points;
}

/// <summary>
/// Territorial waters: the line a set distance (12 nm) off the coast the game's map draws, for one 10° tile at a
/// time. The distance to the nearest land is taken on a grid four times finer than the world grid (land where the
/// stock map draws it: a grid point is sea where its bilinear share of <see cref="SeaMask"/> sea is over half), with
/// an exact Euclidean distance transform (Felzenszwalb–Huttenlocher; north–south and east–west spacing in nautical
/// miles, the latter for each row's latitude); then marching squares at the distance. The tile looks past its edges
/// as far as the distance, and the grid is the same everywhere, so the lines of neighbouring tiles meet.
/// <para>Measured from the low-water coast as the game has it, not from the straight baselines states draw across
/// bays and fjords. Plain C#, safe on a worker thread.</para>
/// </summary>
internal static class SeaLimits
{
    private const int Sub = 4;
    private const double Step = SeaMask.Cell / Sub;                      // 0.0125°: 0.75 nm north–south

    /// <summary>
    /// The line is cut into cells this size, each drawn as its own small path: a path as big as a 10° tile (several
    /// thousand pixels zoomed in) went missing from the map at some positions, small ones don't have to be rendered
    /// as one huge piece.
    /// </summary>
    public const double CellDegrees = 2.5;
    private const int PerTile = (int)(BorderData.Tile / Step + 0.5);    // 800
    private const double Far = 1e12;                                     // "no land" in nm²

    /// <summary>Douglas–Peucker tolerances in degrees; the map shows the coarsest within half a pixel.</summary>
    public static readonly float[] Tolerances = { 0.002f, 0.006f, 0.02f };

    public static int LevelFor(double pixelsPerDegree)
    {
        for (int level = Tolerances.Length - 1; level > 0; level--)
        {
            if (Tolerances[level] * pixelsPerDegree <= 0.5)
                return level;
        }
        return 0;
    }

    /// <summary>Whether a tile can have a line at all (polar tiles and the Antarctic have none).</summary>
    public static bool Covered(int tileY) => tileY >= 3 && tileY <= 16;    // 60°S to 80°N

    public static SeaLimitTile Tile(SeaMask mask, int tileX, int tileY, double nm)
    {
        var result = new SeaLimitTile { TileX = tileX, TileY = tileY, Levels = new List<SeaLimitPiece>[Tolerances.Length] };
        for (int l = 0; l < Tolerances.Length; l++)
            result.Levels[l] = new List<SeaLimitPiece>();
        if (!Covered(tileY) || nm <= 0)
            return result;

        double south = tileY * BorderData.Tile - 90.0, north = south + BorderData.Tile;
        double polewards = Math.Min(85.0, Math.Max(Math.Abs(south), Math.Abs(north)) + nm / 60.0);
        int marginY = (int)Math.Ceiling(nm / 60.0 / Step) + 2;
        int marginX = (int)Math.Ceiling(nm / 60.0 / Math.Cos(polewards * Math.PI / 180.0) / Step) + 2;
        int w = PerTile + 2 * marginX + 1, h = PerTile + 2 * marginY + 1;
        // Global grid point (i, j): lon = -180 + (i + 0.5) * Step, lat = 90 - (j + 0.5) * Step.
        int i0 = tileX * PerTile - marginX;
        int j0 = (int)Math.Round((90.0 - north) / Step) - marginY;

        var g = new double[w * h];
        bool anySea = false, anyLand = false;
        for (int y = 0; y < h; y++)
        {
            double v = (j0 + y + 0.5) / Sub - 0.5;
            int cy = (int)Math.Floor(v);
            double ty = v - cy;
            for (int x = 0; x < w; x++)
            {
                double u = (i0 + x + 0.5) / Sub - 0.5;
                int cx = (int)Math.Floor(u);
                double tx = u - cx;
                double top = Sea(mask, cx, cy) + (Sea(mask, cx + 1, cy) - Sea(mask, cx, cy)) * tx;
                double bottom = Sea(mask, cx, cy + 1) + (Sea(mask, cx + 1, cy + 1) - Sea(mask, cx, cy + 1)) * tx;
                bool sea = top + (bottom - top) * ty > 0.5;
                g[y * w + x] = sea ? Far : 0.0;
                if (sea) anySea = true;
                else anyLand = true;
            }
        }
        if (!anySea || !anyLand)
            return result;

        // Squared distance to the nearest land point: columns first (even spacing), then rows (spacing by latitude).
        int n = Math.Max(w, h);
        var f = new double[n];
        var d = new double[n];
        var vIdx = new int[n];
        var z = new double[n + 1];
        double dy = Step * 60.0;
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
                f[y] = g[y * w + x];
            Edt(f, h, dy * dy, d, vIdx, z);
            for (int y = 0; y < h; y++)
                g[y * w + x] = d[y];
        }
        for (int y = 0; y < h; y++)
        {
            double lat = 90.0 - (j0 + y + 0.5) * Step;
            double dx = Step * 60.0 * Math.Cos(lat * Math.PI / 180.0);
            Array.Copy(g, y * w, f, 0, w);
            Edt(f, w, dx * dx, d, vIdx, z);
            for (int x = 0; x < w; x++)
                g[y * w + x] = Math.Sqrt(d[x]);
        }

        // The coast lies half-way between a land point and the next sea point: measure from there.
        double level = nm + dy / 2.0;
        List<List<(double lon, double lat)>> lines = Contour(g, w, h, level, marginX, marginY, i0, j0);
        foreach (List<(double lon, double lat)> line in lines)
        {
            for (int l = 0; l < Tolerances.Length; l++)
            {
                List<(double lon, double lat)> simple = Simplify(line, Tolerances[l]);
                if (simple.Count < 2)
                    continue;
                result.Levels[l].AddRange(CutAtCells(simple));
                if (l == 0)
                    result.Points += simple.Count;
            }
        }
        return result;
    }

    // Cuts a polyline into pieces that each lie in one cell: every segment is split where it crosses a cell edge,
    // and a new piece starts whenever the cell of the next stretch (by its middle) is another one. Neighbouring
    // pieces share the point where they meet, also when a vertex lies right on an edge.
    private static IEnumerable<SeaLimitPiece> CutAtCells(List<(double lon, double lat)> points)
    {
        var current = new List<(double lon, double lat)>();
        int cx = 0, cy = 0;
        var ts = new List<double>();
        for (int i = 1; i < points.Count; i++)
        {
            (double x0, double y0) = points[i - 1];
            (double x1, double y1) = points[i];
            ts.Clear();
            ts.Add(0.0);
            Crossings(x0 + 180.0, x1 + 180.0, ts);
            Crossings(y0 + 90.0, y1 + 90.0, ts);
            ts.Add(1.0);
            ts.Sort();
            for (int k = 1; k < ts.Count; k++)
            {
                (double lon, double lat) a = (x0 + (x1 - x0) * ts[k - 1], y0 + (y1 - y0) * ts[k - 1]);
                (double lon, double lat) b = (x0 + (x1 - x0) * ts[k], y0 + (y1 - y0) * ts[k]);
                int sx = (int)Math.Floor(((a.lon + b.lon) / 2.0 + 180.0) / CellDegrees);
                int sy = (int)Math.Floor(((a.lat + b.lat) / 2.0 + 90.0) / CellDegrees);
                if (current.Count > 0 && (sx != cx || sy != cy))
                {
                    yield return Piece(current, cx, cy);
                    current.Clear();
                }
                if (current.Count == 0)
                {
                    cx = sx;
                    cy = sy;
                    current.Add(a);
                }
                current.Add(b);
            }
        }
        if (current.Count >= 2)
            yield return Piece(current, cx, cy);
    }

    private static void Crossings(double a, double b, List<double> ts)
    {
        if (a == b)
            return;
        int from = (int)Math.Floor(Math.Min(a, b) / CellDegrees) + 1, to = (int)Math.Floor(Math.Max(a, b) / CellDegrees);
        for (int k = from; k <= to; k++)
        {
            double t = (k * CellDegrees - a) / (b - a);
            if (t > 0.0 && t < 1.0)
                ts.Add(t);
        }
    }

    private static SeaLimitPiece Piece(List<(double lon, double lat)> points, int cx, int cy)
    {
        var pts = new float[points.Count * 2];
        for (int k = 0; k < points.Count; k++)
        {
            pts[2 * k] = (float)points[k].lon;
            pts[2 * k + 1] = (float)points[k].lat;
        }
        return new SeaLimitPiece { Cx = cx, Cy = cy, Points = pts };
    }

    private static double Sea(SeaMask mask, int cx, int cy) => mask.IsSea(cx, cy) ? 1.0 : 0.0;

    // Felzenszwalb–Huttenlocher 1D squared distance transform with spacing² s2 between samples.
    private static void Edt(double[] f, int n, double s2, double[] d, int[] v, double[] z)
    {
        int k = 0;
        v[0] = 0;
        z[0] = double.NegativeInfinity;
        z[1] = double.PositiveInfinity;
        for (int q = 1; q < n; q++)
        {
            // z[0] is -infinity, so this stops at the first parabola at the latest.
            double s = Intersect(f, s2, q, v[k]);
            while (s <= z[k])
                s = Intersect(f, s2, q, v[--k]);
            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = double.PositiveInfinity;
        }
        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q)
                k++;
            double r = q - v[k];
            d[q] = s2 * r * r + f[v[k]];
        }
    }

    private static double Intersect(double[] f, double s2, int q, int p) =>
        (f[q] + s2 * q * q - (f[p] + s2 * p * p)) / (2.0 * s2 * (q - p));

    // Marching squares at `level` over the squares whose top-left point lies in the tile (not in its margins); the
    // segments are chained through the grid edges they cross, so a line comes out as one polyline.
    private static List<List<(double lon, double lat)>> Contour(double[] g, int w, int h, double level, int mx, int my, int i0, int j0)
    {
        var next = new Dictionary<long, long>();
        var prev = new Dictionary<long, long>();
        for (int y = my; y < my + PerTile; y++)
        {
            for (int x = mx; x < mx + PerTile; x++)
            {
                double a = g[y * w + x], b = g[y * w + x + 1], c = g[(y + 1) * w + x + 1], e = g[(y + 1) * w + x];
                int code = (a < level ? 8 : 0) | (b < level ? 4 : 0) | (c < level ? 2 : 0) | (e < level ? 1 : 0);
                if (code == 0 || code == 15)
                    continue;
                long top = EdgeH(x, y, w), right = EdgeV(x + 1, y, w), bottom = EdgeH(x, y + 1, w), left = EdgeV(x, y, w);
                switch (code)
                {
                    case 1: Link(left, bottom); break;
                    case 2: Link(bottom, right); break;
                    case 3: Link(left, right); break;
                    case 4: Link(right, top); break;
                    case 6: Link(bottom, top); break;
                    case 7: Link(left, top); break;
                    case 8: Link(top, left); break;
                    case 9: Link(top, bottom); break;
                    case 11: Link(top, right); break;
                    case 12: Link(right, left); break;
                    case 13: Link(right, bottom); break;
                    case 14: Link(bottom, left); break;
                    case 5:
                    case 10:
                        // A saddle: the centre decides which corners belong together.
                        bool centre = (a + b + c + e) / 4.0 < level;
                        if ((code == 5) == centre)
                        {
                            Link(code == 5 ? left : top, code == 5 ? top : left);
                            Link(code == 5 ? right : bottom, code == 5 ? bottom : right);
                        }
                        else
                        {
                            Link(code == 5 ? left : top, code == 5 ? bottom : right);
                            Link(code == 5 ? right : bottom, code == 5 ? top : left);
                        }
                        break;
                }
            }
        }

        // Chain the segments: start where a chain has no predecessor (it runs out of the tile), then the closed loops.
        var lines = new List<List<(double lon, double lat)>>();
        var done = new HashSet<long>();
        foreach (long start in new List<long>(next.Keys))
        {
            if (prev.ContainsKey(start))
                continue;
            lines.Add(Walk(start));
        }
        foreach (long start in new List<long>(next.Keys))
        {
            if (!done.Contains(start))
                lines.Add(Walk(start));
        }
        return lines;

        void Link(long from, long to)
        {
            next[from] = to;
            prev[to] = from;
        }

        List<(double lon, double lat)> Walk(long start)
        {
            var line = new List<(double lon, double lat)> { Point(start) };
            long at = start;
            done.Add(at);
            while (next.TryGetValue(at, out long to))
            {
                line.Add(Point(to));
                if (!done.Add(to))
                    break;                              // back at the start of a loop
                at = to;
            }
            return line;
        }

        // Where the line crosses a grid edge, by linear interpolation of the distance along it.
        (double lon, double lat) Point(long edge)
        {
            long cell = edge >> 1;
            int x = (int)(cell % w), y = (int)(cell / w);
            bool vertical = (edge & 1) == 1;
            double p = g[y * w + x], q = vertical ? g[(y + 1) * w + x] : g[y * w + x + 1];
            double t = Math.Abs(q - p) < 1e-12 ? 0.5 : (level - p) / (q - p);
            double gx = i0 + x + (vertical ? 0 : t), gy = j0 + y + (vertical ? t : 0);
            return (-180.0 + (gx + 0.5) * Step, 90.0 - (gy + 0.5) * Step);
        }
    }

    // Grid edges: horizontal from (x, y) to (x + 1, y), vertical from (x, y) to (x, y + 1).
    private static long EdgeH(int x, int y, int w) => ((long)y * w + x) << 1;

    private static long EdgeV(int x, int y, int w) => (((long)y * w + x) << 1) | 1;

    // Douglas–Peucker, ends kept (lines of neighbouring tiles still meet).
    private static List<(double lon, double lat)> Simplify(List<(double lon, double lat)> points, double tolerance)
    {
        if (points.Count < 3)
            return points;
        var keep = new bool[points.Count];
        keep[0] = keep[points.Count - 1] = true;
        var stack = new Stack<(int a, int b)>();
        stack.Push((0, points.Count - 1));
        while (stack.Count > 0)
        {
            (int a, int b) = stack.Pop();
            double ax = points[a].lon, ay = points[a].lat, dx = points[b].lon - ax, dy = points[b].lat - ay;
            double length = Math.Sqrt(dx * dx + dy * dy);
            double worst = -1;
            int index = -1;
            for (int i = a + 1; i < b; i++)
            {
                double px = points[i].lon - ax, py = points[i].lat - ay;
                double dist = length == 0 ? Math.Sqrt(px * px + py * py) : Math.Abs(dy * px - dx * py) / length;
                if (dist > worst)
                {
                    worst = dist;
                    index = i;
                }
            }
            if (worst > tolerance)
            {
                keep[index] = true;
                stack.Push((a, index));
                stack.Push((index, b));
            }
        }
        var result = new List<(double lon, double lat)>();
        for (int i = 0; i < points.Count; i++)
        {
            if (keep[i])
                result.Add(points[i]);
        }
        return result;
    }
}
