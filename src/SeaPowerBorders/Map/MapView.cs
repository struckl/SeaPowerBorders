using MapControl;
using Noesis;

namespace SeaPowerBorders.Map;

/// <summary>
/// What the drawing needs to know of a map control's projection. The mission map is a globe (an
/// orthographic projection); the mission editor's map is equirectangular.
/// </summary>
internal static class MapView
{
    public static bool HasSize(MapBase map, double least) => map.ActualWidth >= least && map.ActualHeight >= least;

    public static bool IsGlobe(MapBase map) => map.MapProjection is OrthographicProjection;

    /// <summary>The lon/lat box of what is in view; null when the map has no size or shows nothing.</summary>
    public static BoundingBox VisibleBox(MapBase map) => IsGlobe(map)
        ? map.MapProjection.GetVisibleGeoBounds(map.ViewTransform, new Size((float)map.ActualWidth, (float)map.ActualHeight))
        : FlatBox(map);

    /// <summary>The point the globe is turned to; false on a flat map.</summary>
    public static bool TryGlobeCentre(MapBase map, out double lon, out double lat)
    {
        lon = lat = 0;
        if (!IsGlobe(map))
            return false;
        Location centre = map.MapProjection.Center;
        lon = centre.Longitude;
        lat = centre.Latitude;
        return true;
    }

    /// <summary>Where a place shows in the map control, in pixels; false on the globe's far side.</summary>
    public static bool ToView(MapBase map, double lat, double lon, out float x, out float y)
    {
        bool visible;
        Point view;
        if (IsGlobe(map))
        {
            visible = map.TryLocationToView(new SeaPower.GeoPosition(lat, lon), out view);
        }
        else
        {
            view = map.LocationToView(new Location(lat, lon));
            visible = true;
        }
        x = view.X;
        y = view.Y;
        return visible;
    }

    private static BoundingBox FlatBox(MapBase map) =>
        map.ViewRectToBoundingBox(new Rect(0, 0, map.ActualWidth, map.ActualHeight));
}
