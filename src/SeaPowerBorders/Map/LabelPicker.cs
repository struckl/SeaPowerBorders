using System;
using System.Collections.Generic;
using SeaPowerBorders.Places;

namespace SeaPowerBorders.Map;

/// <summary>How many places show at a given zoom.</summary>
internal enum Density { Fewer, Normal, More }

/// <summary>
/// Which places get a label at a zoom: those whose rank fits the view, most important first, each only where its
/// label doesn't overlap one already placed. Plain C# (no Noesis), so the offline check draws the same choice.
/// </summary>
internal static class LabelPicker
{
    /// <summary>View height in degrees up to which a rank shows (rank 0 always).</summary>
    private static readonly double[] RankHeights = { double.MaxValue, 70, 25, 9 };
    public static readonly float[] FontSizes = { 13f, 12f, 11f, 10f };
    private const int MaxLabels = 450;
    private const float Cell = 64f;

    /// <summary>
    /// Most important first: rank, then capitals, then the bigger cities, then naval bases (a base next to a big
    /// city – Kronstadt by Leningrad – shows once there is room for both).
    /// </summary>
    public static void Order(List<Place> places, bool naval)
    {
        places.Sort((a, b) =>
        {
            int c = a.Rank.CompareTo(b.Rank);
            if (c == 0) c = b.Capital.CompareTo(a.Capital);
            if (c == 0) c = b.Population.CompareTo(a.Population);
            if (c == 0) c = (b.Naval && naval).CompareTo(a.Naval && naval);
            return c;
        });
    }

    public static int MaxRank(double viewHeight, Density density)
    {
        float factor = density == Density.Fewer ? 0.6f : density == Density.More ? 1.7f : 1f;
        int max = 0;
        for (int r = 1; r < RankHeights.Length; r++)
        {
            if (viewHeight <= RankHeights[r] * factor)
                max = r;
        }
        return max;
    }

    /// <summary>The view plus half its size on every side: places there are picked, so a pan needs no new pick
    /// until it has gone a quarter of the view.</summary>
    public static (double west, double south, double east, double north) Around(double west, double south, double east, double north)
    {
        double w = (east - west) * 0.5, h = (north - south) * 0.5;
        return (west - w, south - h, east + w, north + h);
    }

    /// <param name="ordered">Candidates in the order of <see cref="Order"/>.</param>
    /// <param name="area">Only places in here (see <see cref="Around"/>).</param>
    /// <param name="project">A place's position in view units.</param>
    /// <returns>The places to label, each with whether its name goes left of the symbol (where the right side is
    /// taken).</returns>
    public static Dictionary<Place, bool> Pick(List<Place> ordered, double viewHeight, Density density, bool naval, float scale,
                                               (double west, double south, double east, double north) area,
                                               Func<Place, (float x, float y)> project)
    {
        int maxRank = MaxRank(viewHeight, density);
        var occupied = new Dictionary<long, List<(float x, float y, float w, float h)>>();
        var picked = new Dictionary<Place, bool>();
        foreach (Place p in ordered)
        {
            if (p.Rank > maxRank || picked.Count >= MaxLabels || !Inside(p, area))
                continue;
            (float x, float y) = project(p);
            bool anchor = p.Naval && naval;
            float icon = (anchor ? 12f : p.Capital ? 9f : 6f) * scale;
            float font = FontSizes[Math.Min(p.Rank, FontSizes.Length - 1)] * scale;
            bool bold = p.Capital || p.Rank == 0;
            float textWidth = p.Label.Length * font * (bold ? 0.6f : 0.55f);
            float width = icon + 4f + textWidth + 2f;
            var right = (x: x - icon / 2f - 2f, y: y - font * 0.7f - 1f, w: width, h: font * 1.4f + 2f);
            var left = (x: x + icon / 2f + 2f - width, y: right.y, w: width, h: right.h);
            bool onLeft = false;
            if (Overlaps(occupied, right))
            {
                if (Overlaps(occupied, left))
                    continue;
                onLeft = true;
            }
            Occupy(occupied, onLeft ? left : right);
            picked[p] = onLeft;
        }
        return picked;
    }

    private static bool Inside(Place p, (double west, double south, double east, double north) a)
    {
        if (p.Lat < a.south || p.Lat > a.north)
            return false;
        if (a.east - a.west >= 360.0)
            return true;
        // The map runs on past ±180°: try the place a turn east or west too.
        for (double lon = p.Lon - 360.0; lon <= p.Lon + 360.0; lon += 360.0)
        {
            if (lon >= a.west && lon <= a.east)
                return true;
        }
        return false;
    }

    private static bool Overlaps(Dictionary<long, List<(float x, float y, float w, float h)>> occupied, (float x, float y, float w, float h) box)
    {
        foreach (long key in Cells(box))
        {
            if (!occupied.TryGetValue(key, out var rects))
                continue;
            foreach (var r in rects)
            {
                if (box.x < r.x + r.w && r.x < box.x + box.w && box.y < r.y + r.h && r.y < box.y + box.h)
                    return true;
            }
        }
        return false;
    }

    private static void Occupy(Dictionary<long, List<(float x, float y, float w, float h)>> occupied, (float x, float y, float w, float h) box)
    {
        foreach (long key in Cells(box))
        {
            if (!occupied.TryGetValue(key, out var rects))
                occupied[key] = rects = new List<(float, float, float, float)>();
            rects.Add(box);
        }
    }

    private static IEnumerable<long> Cells((float x, float y, float w, float h) box)
    {
        int x0 = (int)Math.Floor(box.x / Cell), x1 = (int)Math.Floor((box.x + box.w) / Cell);
        int y0 = (int)Math.Floor(box.y / Cell), y1 = (int)Math.Floor((box.y + box.h) / Cell);
        for (int x = x0; x <= x1; x++)
        {
            for (int y = y0; y <= y1; y++)
                yield return ((long)x << 32) ^ (uint)y;
        }
    }
}
