using System;
using System.Collections.Generic;

namespace SeaPowerBorders.Borders;

/// <summary>The line a set distance off the coast in one 10° tile, at a few levels of detail.</summary>
internal sealed class SeaLimitTile
{
    public int TileX, TileY;

    /// <summary>Per level of <see cref="SeaLimits.Tolerances"/>: polylines as longitude, latitude pairs.</summary>
    public List<float[]>[] Levels;

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

    /// <summary>How far a tile's line may reach past the tile's edges (its grid is half a step off them), degrees.</summary>
    public const double Pad = Step;
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
        var result = new SeaLimitTile { TileX = tileX, TileY = tileY, Levels = new List<float[]>[Tolerances.Length] };
        for (int l = 0; l < Tolerances.Length; l++)
            result.Levels[l] = new List<float[]>();
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
                var pts = new float[simple.Count * 2];
                for (int k = 0; k < simple.Count; k++)
                {
                    pts[2 * k] = (float)simple[k].lon;
                    pts[2 * k + 1] = (float)simple[k].lat;
                }
                result.Levels[l].Add(pts);
                if (l == 0)
                    result.Points += simple.Count;
            }
        }
        return result;
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
