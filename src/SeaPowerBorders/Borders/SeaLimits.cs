using System;
using System.Collections.Generic;

namespace SeaPowerBorders.Borders;

/// <summary>A stretch of the line inside one <see cref="SeaLimits.CellDegrees"/> cell.</summary>
internal sealed class SeaLimitPiece : GeoPiece
{
    /// <summary>Cell numbers from 180 W and 90 S.</summary>
    public int Cx, Cy;
}

/// <summary>The line a set distance off the coast in one tile, at a few levels of detail.</summary>
internal sealed class SeaLimitTile
{
    /// <summary>Per level of <see cref="SeaLimits.Tolerances"/>: the line cut into cells.</summary>
    public List<SeaLimitPiece>[] Levels;

    public int Points;

    public static SeaLimitTile Empty() =>
        new SeaLimitTile { Levels = Array.ConvertAll(SeaLimits.Tolerances, _ => new List<SeaLimitPiece>()) };
}

/// <summary>
/// Territorial waters: the line a set distance (12 nm) off the coast the game's map draws, for one tile at a
/// time. The distance to the nearest land is taken on a grid four times finer than the world grid (land where the
/// stock map draws it: a grid point is sea where its bilinear share of <see cref="SeaMask"/> sea is over half), with
/// an exact Euclidean distance transform (Felzenszwalb - Huttenlocher; north - south and east - west spacing in nautical
/// miles, the latter for each row's latitude); then marching squares at the distance. The tile looks past its edges
/// as far as the distance, and the grid is the same everywhere, so the lines of neighbouring tiles meet.
/// <para>Measured from the low-water coast as the game has it, not from the straight baselines states draw across
/// bays and fjords. Plain C#, safe on a worker thread.</para>
/// </summary>
internal static class SeaLimits
{
    private const int Sub = 4;
    private const double Step = SeaMask.Cell / Sub;                      // 0.0125 deg: 0.75 nm north - south
    private const int PerTile = (int)(Tiles.Size / Step + 0.5);          // 800
    private const double Far = 1e12;                                     // "no land" in nm squared

    // Polar tiles and the Antarctic have no line.
    private const double SouthLimit = -60.0, NorthLimit = 80.0;

    /// <summary>
    /// The line is cut into cells this size, each drawn as its own small path: a path as big as a tile (several
    /// thousand pixels zoomed in) went missing from the map at some positions, small ones don't have to be rendered
    /// as one huge piece.
    /// </summary>
    public const double CellDegrees = 2.5;

    /// <summary>Douglas - Peucker tolerances in degrees; the map shows the coarsest within half a pixel.</summary>
    public static readonly float[] Tolerances = { 0.002f, 0.006f, 0.02f };

    public static int LevelFor(double pixelsPerDegree) => Tiles.LevelFor(Tolerances, pixelsPerDegree);

    /// <summary>Whether a tile can have a line at all.</summary>
    public static bool Covered(int tileY) =>
        tileY >= (int)((SouthLimit + 90.0) / Tiles.Size) && tileY < (int)((NorthLimit + 90.0) / Tiles.Size);

    public static SeaLimitTile Tile(SeaMask mask, int tileX, int tileY, double nm)
    {
        SeaLimitTile result = SeaLimitTile.Empty();
        if (!Covered(tileY) || nm <= 0)
            return result;
        DistanceGrid grid = DistanceGrid.Make(mask, tileX, tileY, nm);
        if (grid == null)
            return result;
        // The coast lies half-way between a land point and the next sea point: measure from there.
        foreach (List<(double lon, double lat)> line in new Contour(grid, nm + grid.RowStepNm / 2.0).Lines())
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

    /// <summary>
    /// The distance in nm from every point of a tile's grid (and its margins) to the nearest land point. Global grid
    /// point (i, j): lon = -180 + (i + 0.5) * Step, lat = 90 - (j + 0.5) * Step.
    /// </summary>
    private sealed class DistanceGrid
    {
        public double[] G;
        public int W, H, MarginX, MarginY, I0, J0;
        public double RowStepNm => Step * 60.0;

        // Null when the tile and its margins are all sea or all land: no line there.
        public static DistanceGrid Make(SeaMask mask, int tileX, int tileY, double nm)
        {
            double south = tileY * Tiles.Size - 90.0, north = south + Tiles.Size;
            double polewards = Math.Min(85.0, Math.Max(Math.Abs(south), Math.Abs(north)) + nm / 60.0);
            var grid = new DistanceGrid
            {
                MarginY = (int)Math.Ceiling(nm / 60.0 / Step) + 2,
                MarginX = (int)Math.Ceiling(nm / 60.0 / Math.Cos(polewards * Math.PI / 180.0) / Step) + 2,
            };
            grid.W = PerTile + 2 * grid.MarginX + 1;
            grid.H = PerTile + 2 * grid.MarginY + 1;
            grid.I0 = tileX * PerTile - grid.MarginX;
            grid.J0 = (int)Math.Round((90.0 - north) / Step) - grid.MarginY;
            if (!grid.FillLandSea(mask))
                return null;
            grid.Transform();
            return grid;
        }

        // Land 0, sea "far"; true when there is some of both.
        private bool FillLandSea(SeaMask mask)
        {
            G = new double[W * H];
            bool anySea = false, anyLand = false;
            for (int y = 0; y < H; y++)
            {
                double v = (J0 + y + 0.5) / Sub - 0.5;
                int cy = (int)Math.Floor(v);
                double ty = v - cy;
                for (int x = 0; x < W; x++)
                {
                    double u = (I0 + x + 0.5) / Sub - 0.5;
                    int cx = (int)Math.Floor(u);
                    double tx = u - cx;
                    double top = Sea(mask, cx, cy) + (Sea(mask, cx + 1, cy) - Sea(mask, cx, cy)) * tx;
                    double bottom = Sea(mask, cx, cy + 1) + (Sea(mask, cx + 1, cy + 1) - Sea(mask, cx, cy + 1)) * tx;
                    bool sea = top + (bottom - top) * ty > 0.5;
                    G[y * W + x] = sea ? Far : 0.0;
                    anySea |= sea;
                    anyLand |= !sea;
                }
            }
            return anySea && anyLand;
        }

        private static double Sea(SeaMask mask, int cx, int cy) => mask.IsSea(cx, cy) ? 1.0 : 0.0;

        // Squared distance to the nearest land point: columns first (even spacing), then rows (spacing by latitude).
        private void Transform()
        {
            int n = Math.Max(W, H);
            var f = new double[n];
            var d = new double[n];
            var vIdx = new int[n];
            var z = new double[n + 1];
            double dy = RowStepNm;
            for (int x = 0; x < W; x++)
            {
                for (int y = 0; y < H; y++)
                    f[y] = G[y * W + x];
                Edt(f, H, dy * dy, d, vIdx, z);
                for (int y = 0; y < H; y++)
                    G[y * W + x] = d[y];
            }
            for (int y = 0; y < H; y++)
            {
                double lat = 90.0 - (J0 + y + 0.5) * Step;
                double dx = Step * 60.0 * Math.Cos(lat * Math.PI / 180.0);
                Array.Copy(G, y * W, f, 0, W);
                Edt(f, W, dx * dx, d, vIdx, z);
                for (int x = 0; x < W; x++)
                    G[y * W + x] = Math.Sqrt(d[x]);
            }
        }

        // Felzenszwalb - Huttenlocher 1D squared distance transform with spacing squared s2 between samples.
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
    }

    /// <summary>
    /// Marching squares at one distance over the squares whose top-left point lies in the tile (not in its margins);
    /// the segments are chained through the grid edges they cross, so a line comes out as one polyline.
    /// </summary>
    private sealed class Contour
    {
        private const int Top = 0, Right = 1, Bottom = 2, Left = 3;

        // Per square code (corners below the level: 8 top-left, 4 top-right, 2 bottom-right, 1 bottom-left), the
        // sides each segment runs from and to, keeping the land on the same side of every segment.
        private static readonly int[][] Segments =
        {
            null, new[] { Left, Bottom }, new[] { Bottom, Right }, new[] { Left, Right }, new[] { Right, Top }, null,
            new[] { Bottom, Top }, new[] { Left, Top }, new[] { Top, Left }, new[] { Top, Bottom }, null,
            new[] { Top, Right }, new[] { Right, Left }, new[] { Right, Bottom }, new[] { Bottom, Left }, null,
        };

        // The two saddles (5 and 10), by whether the centre of the square is below the level.
        private static readonly int[] Saddle5Below = { Left, Top, Right, Bottom }, Saddle5Above = { Left, Bottom, Right, Top };
        private static readonly int[] Saddle10Below = { Top, Right, Bottom, Left }, Saddle10Above = { Top, Left, Bottom, Right };

        private readonly DistanceGrid grid;
        private readonly double level;
        private readonly Dictionary<long, long> next = new Dictionary<long, long>();
        private readonly HashSet<long> hasPrev = new HashSet<long>();
        private readonly HashSet<long> done = new HashSet<long>();

        public Contour(DistanceGrid grid, double level)
        {
            this.grid = grid;
            this.level = level;
        }

        public List<List<(double lon, double lat)>> Lines()
        {
            for (int y = grid.MarginY; y < grid.MarginY + PerTile; y++)
            {
                for (int x = grid.MarginX; x < grid.MarginX + PerTile; x++)
                    Square(x, y);
            }
            // Start where a chain has no predecessor (it runs out of the tile), then the closed loops.
            var lines = new List<List<(double lon, double lat)>>();
            foreach (long start in new List<long>(next.Keys))
            {
                if (!hasPrev.Contains(start))
                    lines.Add(Walk(start));
            }
            foreach (long start in new List<long>(next.Keys))
            {
                if (!done.Contains(start))
                    lines.Add(Walk(start));
            }
            return lines;
        }

        private void Square(int x, int y)
        {
            double[] g = grid.G;
            int w = grid.W;
            double a = g[y * w + x], b = g[y * w + x + 1], c = g[(y + 1) * w + x + 1], e = g[(y + 1) * w + x];
            int code = (a < level ? 8 : 0) | (b < level ? 4 : 0) | (c < level ? 2 : 0) | (e < level ? 1 : 0);
            int[] sides = code switch
            {
                5 => (a + b + c + e) / 4.0 < level ? Saddle5Below : Saddle5Above,
                10 => (a + b + c + e) / 4.0 < level ? Saddle10Below : Saddle10Above,
                _ => Segments[code],
            };
            if (sides == null)
                return;
            for (int k = 0; k < sides.Length; k += 2)
            {
                long from = Edge(x, y, sides[k]), to = Edge(x, y, sides[k + 1]);
                next[from] = to;
                hasPrev.Add(to);
            }
        }

        // Grid edges: horizontal from (x, y) to (x + 1, y), vertical from (x, y) to (x, y + 1).
        private long Edge(int x, int y, int side) => side switch
        {
            Top => ((long)y * grid.W + x) << 1,
            Right => (((long)y * grid.W + x + 1) << 1) | 1,
            Bottom => ((long)(y + 1) * grid.W + x) << 1,
            _ => (((long)y * grid.W + x) << 1) | 1,
        };

        private List<(double lon, double lat)> Walk(long start)
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
        private (double lon, double lat) Point(long edge)
        {
            long cell = edge >> 1;
            int w = grid.W, x = (int)(cell % w), y = (int)(cell / w);
            bool vertical = (edge & 1) == 1;
            double p = grid.G[y * w + x], q = vertical ? grid.G[(y + 1) * w + x] : grid.G[y * w + x + 1];
            double t = Math.Abs(q - p) < 1e-12 ? 0.5 : (level - p) / (q - p);
            double gx = grid.I0 + x + (vertical ? 0 : t), gy = grid.J0 + y + (vertical ? t : 0);
            return (-180.0 + (gx + 0.5) * Step, 90.0 - (gy + 0.5) * Step);
        }
    }

    // Cuts a polyline into pieces that each lie in one cell: every segment is split where it crosses a cell edge,
    // and a new piece starts whenever the cell of the next stretch (by its middle) is another one. Neighbouring
    // pieces share the point where they meet, also when a vertex lies right on an edge. (build.py's cut does the
    // same for the border lines and tiles; it runs offline, this one in the game on the coast it reads there.)
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

    // Douglas - Peucker, ends kept (lines of neighbouring tiles still meet). The same algorithm as build.py's
    // simplify: that one runs offline on Natural Earth's borders, this one in the game on the coast it reads there.
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
            int index = Farthest(points, a, b, out double worst);
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

    // The point between a and b farthest from the chord a-b, and its distance (-1 when there is none).
    private static int Farthest(List<(double lon, double lat)> points, int a, int b, out double worst)
    {
        double ax = points[a].lon, ay = points[a].lat, dx = points[b].lon - ax, dy = points[b].lat - ay;
        double length = Math.Sqrt(dx * dx + dy * dy);
        worst = -1;
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
        return index;
    }
}
