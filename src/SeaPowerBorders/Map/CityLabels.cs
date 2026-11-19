using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MapControl;
using Noesis;
using SeaPowerBorders.Places;

namespace SeaPowerBorders.Map;

/// <summary>
/// The place labels on one map control. They live in a <see cref="MapPanel"/> laid into the map itself, right above
/// the graticule: the map places every child at its latitude and longitude on each pan and zoom, below the unit
/// symbols and the map's own drag surface, so nothing gets in the way of clicks.
/// <para>Which labels show is worked out when the zoom changes (not on a pan - the labels keep their distances
/// then): those whose rank fits the zoom, most important first, each only where it doesn't overlap one already
/// placed.</para>
/// </summary>
internal sealed class CityLabels
{
    private const int RelayoutMilliseconds = 200;
    private const double ZoomChange = 0.04;          // of the view height, as a log ratio: a new pick
    private const double GlobeTurnShare = 0.1;       // of the view height the globe's centre may move before a new pick
    private const float HaloWidth = 3f;
    private const float TextGap = 3f;

    private readonly MapBase map;
    private readonly ObservableCollection<object> background;      // the editor's map: the panel goes in here, not in Children
    private readonly MapPanel panel;
    private readonly Dictionary<Place, (FrameworkElement element, bool left)> elements =
        new Dictionary<Place, (FrameworkElement element, bool left)>();
    private readonly List<Place> order = new List<Place>();
    private readonly FailureLog failure = new FailureLog("Place labels");

    private IReadOnlyList<Place> places;
    private LabelLook look;
    private Icons.Palette palette;
    private double laidOutHeight = -1, laidOutX, laidOutY;
    private Throttle relayout;

    /// <summary>Null when the map would not take the panel (logged).</summary>
    public static CityLabels TryAttach(MapBase map, ObservableCollection<object> background = null)
    {
        try
        {
            return new CityLabels(map, background);
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Could not add the place labels to the map: {ex.Message}");
            return null;
        }
    }

    private CityLabels(MapBase map, ObservableCollection<object> background)
    {
        this.map = map;
        this.background = background;
        panel = new MapPanel { IsHitTestVisible = false };
        MapPanel.SetParentMap(panel, map);
        MapSlots.Insert(map, panel, MapSlots.Cities, background);
    }

    public void Detach()
    {
        try
        {
            MapSlots.Remove(map, panel, background);
            MapPanel.SetParentMap(panel, null);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"Label detach: {ex.Message}");
        }
    }

    /// <param name="visible">The Cities layer is on.</param>
    /// <param name="labelled">The places to label on this map; a new list when they change, null while there are none yet.</param>
    /// <param name="labelLook">The look for this map's theme.</param>
    public void Update(bool visible, IReadOnlyList<Place> labelled, LabelLook labelLook)
    {
        Visibility shown = visible && labelled != null ? Visibility.Visible : Visibility.Collapsed;
        if (panel.Visibility != shown)
            panel.Visibility = shown;
        if (shown != Visibility.Visible)
            return;
        if (labelled != places || !labelLook.Equals(look))
            Restart(labelled, labelLook);
        BoundingBox view = MapView.HasSize(map, double.Epsilon) ? MapView.VisibleBox(map) : null;
        if (view == null)
            return;
        double height = Math.Min(180.0, view.North - view.South);
        if (height <= 0 || !Moved(view, height, out double cx, out double cy) || !relayout.Ready(RelayoutMilliseconds))
            return;
        try
        {
            Layout(height, LabelPicker.Around(view.West, view.South, view.East, view.North));
            laidOutHeight = height;
            laidOutX = cx;
            laidOutY = cy;
        }
        catch (Exception ex)
        {
            failure.Report(ex);
        }
    }

    private void Restart(IReadOnlyList<Place> labelled, LabelLook labelLook)
    {
        panel.Children.Clear();
        elements.Clear();
        places = labelled;
        look = labelLook;
        palette = Icons.Palette.For(look.Dark);
        laidOutHeight = -1;
        order.Clear();
        foreach (Place p in places)
        {
            if (look.Shows(p))
                order.Add(p);
        }
        LabelPicker.Order(order, look);
    }

    // A new pick when the zoom changed, or a pan has gone far enough that the last pick no longer covers the view.
    private bool Moved(BoundingBox view, double height, out double cx, out double cy)
    {
        bool zoomed = laidOutHeight <= 0 || Math.Abs(Math.Log(height / laidOutHeight)) >= ZoomChange;
        if (MapView.TryGlobeCentre(map, out cx, out cy))
        {
            // The visible lon/lat box stops changing once the pole or the whole globe is in view: follow the centre
            // the globe is turned to instead.
            double turn = cx - laidOutX;
            turn -= 360.0 * Math.Round(turn / 360.0);
            double across = Math.Abs(turn) * Math.Max(0.2, Math.Cos(cy * Math.PI / 180.0));
            return zoomed || across > height * GlobeTurnShare || Math.Abs(cy - laidOutY) > height * GlobeTurnShare;
        }
        cx = (view.West + view.East) / 2;
        cy = (view.South + view.North) / 2;
        return zoomed || Math.Abs(cx - laidOutX) > (view.East - view.West) * LabelPicker.RepickShare ||
               Math.Abs(cy - laidOutY) > height * LabelPicker.RepickShare;
    }

    private void Layout(double height, (double west, double south, double east, double north) area)
    {
        Dictionary<Place, bool> visible = LabelPicker.Pick(order, height, look, area,
            p => MapView.ToView(map, p.Lat, p.Lon, out float x, out float y) ? (x, y) : (float.NaN, 0f));
        foreach (KeyValuePair<Place, bool> kv in visible)
            Show(kv.Key, kv.Value);
        foreach (KeyValuePair<Place, (FrameworkElement element, bool left)> kv in elements)
        {
            if (!visible.ContainsKey(kv.Key) && kv.Value.element.Visibility != Visibility.Collapsed)
                kv.Value.element.Visibility = Visibility.Collapsed;
        }
        ModLog.Debug($"Places: {visible.Count} shown at {height:0.0} deg view height, {elements.Count} made");
    }

    // The label of a place, its name on the given side; made again when the side changed.
    private void Show(Place p, bool left)
    {
        if (elements.TryGetValue(p, out var known) && known.left != left)
        {
            panel.Children.Remove(known.element);
            elements.Remove(p);
        }
        if (!elements.TryGetValue(p, out known))
        {
            FrameworkElement e = Create(p, left);
            elements[p] = (e, left);
            panel.Children.Add(e);
        }
        else if (known.element.Visibility != Visibility.Visible)
        {
            known.element.Visibility = Visibility.Visible;
        }
    }

    // Symbol on the place, name to its right - or to its left, then the map puts the panel's right edge there.
    private FrameworkElement Create(Place p, bool left)
    {
        FrameworkElement icon = Icons.For(p, palette, look.Anchor(p), look.Size, out float size);
        icon.VerticalAlignment = VerticalAlignment.Center;
        var text = new TextBlock
        {
            Text = p.Label,
            FontSize = LabelPicker.FontSize(p, look.Size),
            FontWeight = p.Bold ? FontWeight.Bold : FontWeight.Normal,
            Foreground = palette.Text,
            Stroke = palette.Halo,
            StrokeThickness = HaloWidth,
            Margin = left ? new Thickness(0, 0, TextGap, 0) : new Thickness(TextGap, 0, 0, 0),
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
