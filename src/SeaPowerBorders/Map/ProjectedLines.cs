using System;

namespace SeaPowerBorders.Map;

/// <summary>The game's map projections work on a sphere of the WGS84 equator's radius.</summary>
internal static class Earth
{
    public const double RadiusMetres = 6378137.0;

    /// <summary>Metres per degree of arc: the flat map's equirectangular scale.</summary>
    public const double MetresPerDegree = RadiusMetres * Math.PI / 180.0;
}

/// <summary>Where a place on the map shows on screen, in pixels; false when it lies beyond the globe's horizon.</summary>
internal delegate bool Projector(double lat, double lon, out float x, out float y);

/// <summary>Receives the screen-space lines: a figure is begun, then extended point by point.</summary>
internal interface ILineSink
{
    void Begin(float x, float y);
    void LineTo(float x, float y);

    /// <summary>Points taken so far (the globe's point budget counts these).</summary>
    int Points { get; }
}

/// <summary>
/// Longitude/latitude polylines drawn through a projector, for a map whose projection is not a plain grid (the
/// globe). The stock map panel can only place a lon/lat rectangle, which the globe's meridians skew; so the lines are
/// projected point by point instead, on every change of the view.
/// <para>A straight stretch between two data points is a straight line in lon/lat, so it is cut into steps short
/// enough that its chords stay within a fraction of a pixel of the curve it follows on the globe; where a line
/// crosses the horizon it is cut there (found by bisection), so nothing is drawn across the far side.</para>
/// Plain C#: the offline check runs it against the game's own projection.
/// </summary>
internal static class ProjectedLines
{
    private const int MaxSteps = 96;
    private const double ToRadians = Math.PI / 180.0;
    private const double TolerancePixels = 0.35;

    /// <summary>The step, in degrees of arc, at which a chord of the curve on a globe of this radius (in pixels) deviates by a third of a pixel.</summary>
    public static double StepDegrees(double globeRadiusPixels)
    {
        double radians = Math.Sqrt(8.0 * TolerancePixels / Math.Max(globeRadiusPixels, 1.0));
        return Math.Min(Math.Max(radians / ToRadians, 0.002), 4.0);
    }

    /// <summary>Hands one polyline (longitude, latitude, longitude, latitude, ...) to the sink.</summary>
    public static void Add(float[] points, Projector project, double stepDegrees, ILineSink sink)
    {
        int count = points.Length / 2;
        if (count < 2)
            return;
        double vertexLat = points[1], vertexLon = points[0];
        double curLat = vertexLat, curLon = vertexLon;
        bool curVisible = project(curLat, curLon, out float x, out float y);
        bool open = false;
        if (curVisible)
        {
            sink.Begin(x, y);
            open = true;
        }
        for (int i = 1; i < count; i++)
        {
            double lon = points[2 * i], lat = points[2 * i + 1];
            double dLat = lat - vertexLat, dLon = lon - vertexLon;
            double east = dLon * Math.Cos(0.5 * (lat + vertexLat) * ToRadians);
            int steps = Math.Max(1, Math.Min(MaxSteps, (int)Math.Ceiling(Math.Sqrt(dLat * dLat + east * east) / stepDegrees)));
            for (int s = 1; s <= steps; s++)
            {
                double la = s == steps ? lat : vertexLat + dLat * s / steps;
                double lo = s == steps ? lon : vertexLon + dLon * s / steps;
                bool visible = project(la, lo, out float nx, out float ny);
                if (visible)
                {
                    if (!open)
                    {
                        if (!curVisible && Horizon(project, la, lo, curLat, curLon, out float hx, out float hy))
                        {
                            sink.Begin(hx, hy);
                        }
                        else
                        {
                            sink.Begin(nx, ny);
                        }
                        open = true;
                    }
                    sink.LineTo(nx, ny);
                }
                else if (open)
                {
                    if (Horizon(project, curLat, curLon, la, lo, out float hx, out float hy))
                    {
                        sink.LineTo(hx, hy);
                    }
                    open = false;
                }
                curLat = la;
                curLon = lo;
                curVisible = visible;
            }
            vertexLat = lat;
            vertexLon = lon;
        }
    }

    // The last visible point on the way from a visible point to a hidden one (bisection along the straight lon/lat stretch).
    private static bool Horizon(Projector project, double visLat, double visLon, double hidLat, double hidLon, out float x, out float y)
    {
        bool found = project(visLat, visLon, out x, out y);
        double aLat = visLat, aLon = visLon, bLat = hidLat, bLon = hidLon;
        for (int k = 0; k < 10; k++)
        {
            double mLat = 0.5 * (aLat + bLat), mLon = 0.5 * (aLon + bLon);
            if (project(mLat, mLon, out float mx, out float my))
            {
                aLat = mLat;
                aLon = mLon;
                x = mx;
                y = my;
            }
            else
            {
                bLat = mLat;
                bLon = mLon;
            }
        }
        return found;
    }

}

/// <summary>
/// The part of the globe in view as a lon/lat box, padded so a line that is just off screen (or bulges past the box's
/// corners, as the edges of a view on a globe do) is still found. Longitudes may run past +/-180°.
/// </summary>
internal readonly struct GeoArea
{
    public readonly double West, South, East, North;
    public readonly bool AllLongitudes;

    public GeoArea(double west, double south, double east, double north)
    {
        double padLat = Math.Max(0.5, (north - south) * 0.1);
        South = Math.Max(-90.0, south - padLat);
        North = Math.Min(90.0, north + padLat);
        double cos = Math.Max(0.05, Math.Cos(Math.Max(Math.Abs(South), Math.Abs(North)) * Math.PI / 180.0));
        double padLon = Math.Min(180.0, Math.Max(0.5, (east - west) * 0.1) / cos);
        West = west - padLon;
        East = east + padLon;
        AllLongitudes = East - West >= 359.0;
    }

    /// <summary>Whether a bounding box (west, south, east, north, longitudes within +/-180°) meets the area.</summary>
    public bool Meets(float[] box)
    {
        if (box[3] < South || box[1] > North)
            return false;
        if (AllLongitudes)
            return true;
        for (int turn = -1; turn <= 1; turn++)
        {
            if (box[2] >= West + 360.0 * turn && box[0] <= East + 360.0 * turn)
                return true;
        }
        return false;
    }
}
