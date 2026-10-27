using System;
using System.Collections.Generic;
using MapControl;
using Noesis;
using SeaPowerBorders.Places;

namespace SeaPowerBorders.Map;

/// <summary>
/// The place labels on one map control. They live in a <see cref="MapPanel"/> laid into the map itself, right above
/// the graticule: the map places every child at its latitude and longitude on each pan and zoom, below the unit
/// symbols and the map's own drag surface, so nothing gets in the way of clicks.
/// <para>Which labels show is worked out when the zoom changes (not on a pan – the labels keep their distances
/// then): those whose rank fits the zoom, most important first, each only where it doesn't overlap one already
/// placed.</para>
/// </summary>
internal sealed class CityLabels
{
    private const float RelayoutSeconds = 0.2f;

    public readonly MapBase Map;
    private readonly MapPanel panel;
    private readonly Dictionary<Place, (FrameworkElement element, bool left)> elements =
        new Dictionary<Place, (FrameworkElement element, bool left)>();
    private readonly List<Place> order = new List<Place>();

    private List<Place> places;
    private int placesVersion = -1, settingsVersion = -1;
    private bool dark;
    private Icons.Palette palette;
    private double laidOutHeight = -1, laidOutX, laidOutY;
    private float nextLayout;
    private bool loggedFailure;

    /// <summary>Lays a label panel into the map, or returns null if the map isn't the panel we expect.</summary>
    public static CityLabels TryAttach(MapBase map)
    {
        try
        {
            return new CityLabels(map);
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Could not add the place labels to the map: {ex.Message}");
            return null;
        }
    }

    private CityLabels(MapBase map)
    {
        Map = map;
        panel = new MapPanel { IsHitTestVisible = false };
        MapPanel.SetParentMap(panel, map);
        MapSlots.Insert(map, panel, MapSlots.Cities);
    }

    /// <summary>False once the map has left the visual tree (window closed, mission over).</summary>
    public bool Alive => VisualTreeHelper.GetParent(Map) != null;

    public void Detach()
    {
        try
        {
            Map.Children.Remove(panel);
            MapPanel.SetParentMap(panel, null);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"Label detach: {ex.Message}");
        }
    }

    /// <param name="visible">The Cities layer is on.</param>
    /// <param name="all">The mission's places; <paramref name="version"/> changes when they do.</param>
    public void Update(bool visible, List<Place> all, int version, bool darkTheme, float now)
    {
        if (!visible || all == null)
        {
            if (panel.Visibility != Visibility.Collapsed)
                panel.Visibility = Visibility.Collapsed;
            return;
        }
        if (panel.Visibility != Visibility.Visible)
            panel.Visibility = Visibility.Visible;

        if (version != placesVersion || darkTheme != dark || Settings.Version != settingsVersion)
        {
            // New places, the other theme or other settings: every label is made again.
            panel.Children.Clear();
            elements.Clear();
            places = all;
            placesVersion = version;
            settingsVersion = Settings.Version;
            dark = darkTheme;
            palette = Icons.Palette.For(dark);
            laidOutHeight = -1;
            Order();
        }

        if (!TryView(out BoundingBox view))
            return;
        double height = Math.Min(180.0, view.North - view.South);
        if (height <= 0)
            return;
        // A new pick when the zoom changed, or a pan has gone a quarter of the view (the pick covers half more).
        bool zoomed = laidOutHeight <= 0 || Math.Abs(Math.Log(height / laidOutHeight)) >= 0.04;
        double cx = (view.West + view.East) / 2, cy = (view.South + view.North) / 2;
        bool panned = Math.Abs(cx - laidOutX) > (view.East - view.West) * 0.25 || Math.Abs(cy - laidOutY) > height * 0.25;
        if (Globe)
        {
            // The visible lon/lat box stops changing once the pole or the whole globe is in view: follow the centre
            // the globe is turned to instead, a tenth of the view's height at a time.
            Location centre = Map.MapProjection.Center;
            cx = centre.Longitude;
            cy = centre.Latitude;
            double turn = cx - laidOutX;
            turn -= 360.0 * Math.Round(turn / 360.0);
            panned = Math.Abs(turn) * Math.Max(0.2, Math.Cos(cy * Math.PI / 180.0)) > height * 0.1 || Math.Abs(cy - laidOutY) > height * 0.1;
        }
        if (!zoomed && !panned)
            return;
        if (now < nextLayout)
            return;
        nextLayout = now + RelayoutSeconds;
        try
        {
            Layout(height, LabelPicker.Around(view.West, view.South, view.East, view.North));
            laidOutHeight = height;
            laidOutX = cx;
            laidOutY = cy;
        }
        catch (Exception ex)
        {
            if (!loggedFailure)
            {
                loggedFailure = true;
                ModLog.Error($"Place labels failed: {ex}");
            }
        }
    }

    private bool TryView(out BoundingBox view)
    {
        view = null;
        if (Map.ActualWidth <= 0 || Map.ActualHeight <= 0)
            return false;
        view = Globe
            ? Map.MapProjection.GetVisibleGeoBounds(Map.ViewTransform, new Size((float)Map.ActualWidth, (float)Map.ActualHeight))
            : Map.ViewRectToBoundingBox(new Rect(0, 0, Map.ActualWidth, Map.ActualHeight));
        return view != null;
    }

    /// <summary>The map is the globe (an orthographic projection): places on its far side have no position.</summary>
    private bool Globe => Map.MapProjection is OrthographicProjection;

    private void Order()
    {
        order.Clear();
        foreach (Place p in places)
        {
            if (Shown(p))
                order.Add(p);
        }
        LabelPicker.Order(order, Settings.NavalBases.Value);
    }

    private static bool Shown(Place p)
    {
        switch (p.Kind)
        {
            case PlaceKind.AirBase:
                return Settings.AirBases.Value;
            case PlaceKind.Airport:
                return Settings.Airports.Value;
            case PlaceKind.NavalBase:
                return Settings.NavalBases.Value;
            default:
                return true;
        }
    }

    private void Layout(double height, (double west, double south, double east, double north) area)
    {
        float scale = Settings.LabelSize.Value;
        bool globe = Globe;
        Dictionary<Place, bool> visible = LabelPicker.Pick(order, height, Settings.Density.Value, Settings.NavalBases.Value, scale, area, p =>
        {
            if (globe)
            {
                bool near = Map.TryLocationToView(new SeaPower.GeoPosition(p.Lat, p.Lon), out Point g);
                return near ? (g.X, g.Y) : (float.NaN, 0f);
            }
            Point v = Map.LocationToView(new Location(p.Lat, p.Lon));
            return (v.X, v.Y);
        });

        foreach (KeyValuePair<Place, bool> kv in visible)
        {
            Place p = kv.Key;
            bool left = kv.Value;
            if (elements.TryGetValue(p, out var known) && known.left != left)
            {
                panel.Children.Remove(known.element);
                elements.Remove(p);
            }
            if (!elements.TryGetValue(p, out known))
            {
                FrameworkElement e = Create(p, scale, left);
                elements[p] = (e, left);
                panel.Children.Add(e);
            }
            else if (known.element.Visibility != Visibility.Visible)
            {
                known.element.Visibility = Visibility.Visible;
            }
        }
        foreach (KeyValuePair<Place, (FrameworkElement element, bool left)> kv in elements)
        {
            if (!visible.ContainsKey(kv.Key) && kv.Value.element.Visibility != Visibility.Collapsed)
                kv.Value.element.Visibility = Visibility.Collapsed;
        }
        ModLog.Debug($"Places: {visible.Count} shown at {height:0.0}° view height, {elements.Count} made");
    }

    // Symbol on the place, name to its right - or to its left, then the map puts the panel's right edge there.
    private FrameworkElement Create(Place p, float scale, bool left)
    {
        bool naval = p.Naval && Settings.NavalBases.Value;
        FrameworkElement icon = Icons.For(p, palette, naval, scale, out float size);
        icon.VerticalAlignment = VerticalAlignment.Center;
        bool bold = p.Capital || p.Rank == 0;
        var text = new TextBlock
        {
            Text = p.Label,
            FontSize = LabelPicker.FontSizes[Math.Min(p.Rank, LabelPicker.FontSizes.Length - 1)] * scale,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            Foreground = palette.Text,
            Stroke = palette.Halo,
            StrokeThickness = 3f,
            Margin = left ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = left ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            // The map puts that edge on the place: shift by half the symbol, so the symbol is centred on it.
            Margin = left ? new Thickness(0, 0, -size / 2f, 0) : new Thickness(-size / 2f, 0, 0, 0),
            IsHitTestVisible = false,
        };
        if (left)
            row.Children.Add(text);
        row.Children.Add(icon);
        if (!left)
            row.Children.Add(text);
        row.SetValue(MapPanel.LatitudeProperty, p.Lat);
        row.SetValue(MapPanel.LongitudeProperty, p.Lon);
        return row;
    }
}
