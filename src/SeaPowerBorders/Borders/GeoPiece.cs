using System;
using System.Collections.Generic;

namespace SeaPowerBorders.Borders;

/// <summary>A stretch of a line on the map: longitude, latitude, longitude, latitude, ...</summary>
internal abstract class GeoPiece
{
    public float[] Points;

    private float[] bounds;

    /// <summary>West, south, east, north of the points (worked out once, for the globe's culling).</summary>
    public float[] Bounds => bounds ??= BoundsOf(Points);

    private static float[] BoundsOf(float[] points)
    {
        float west = float.MaxValue, south = float.MaxValue, east = float.MinValue, north = float.MinValue;
        for (int i = 0; i + 1 < points.Length; i += 2)
        {
            west = Math.Min(west, points[i]);
            east = Math.Max(east, points[i]);
            south = Math.Min(south, points[i + 1]);
            north = Math.Max(north, points[i + 1]);
        }
        return new[] { west, south, east, north };
    }
}

/// <summary>
/// The 10 degree tiles both line sets are cut into (border lines in <c>borders.bin</c>, the coastal line per tile), and
/// the level of detail a zoom gets. Tile x counts from 180 W, tile y from 90 S.
/// </summary>
internal static class Tiles
{
    public const float Size = 10f;
    public const int Across = (int)(360f / Size), Down = (int)(180f / Size);

    /// <summary>Tile x of a column that may lie a turn east or west (the map runs on past +/-180).</summary>
    public static int Wrap(int tx) => ((tx % Across) + Across) % Across;

    /// <summary>
    /// The tiles a lon/lat box meets, as (tile x before wrapping, tile y); at most one turn of columns. With
    /// <paramref name="allLongitudes"/> every column once.
    /// </summary>
    public static IEnumerable<(int tx, int ty)> In(double west, double south, double east, double north, bool allLongitudes = false)
    {
        int x0 = 0, x1 = Across - 1;
        if (!allLongitudes)
        {
            x0 = (int)Math.Floor((west + 180.0) / Size);
            x1 = Math.Min((int)Math.Floor((east + 180.0) / Size), x0 + Across - 1);
        }
        int y0 = Math.Max(0, (int)Math.Floor((south + 90.0) / Size));
        int y1 = Math.Min(Down - 1, (int)Math.Floor((north + 90.0) / Size));
        for (int tx = x0; tx <= x1; tx++)
        {
            for (int ty = y0; ty <= y1; ty++)
                yield return (tx, ty);
        }
    }

    /// <summary>The coarsest level whose simplification (Douglas-Peucker tolerance in degrees) stays within half a pixel at this zoom.</summary>
    public static int LevelFor(float[] tolerances, double pixelsPerDegree)
    {
        for (int level = tolerances.Length - 1; level > 0; level--)
        {
            if (tolerances[level] * pixelsPerDegree <= 0.5)
                return level;
        }
        return 0;
    }
}
