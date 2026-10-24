using Noesis;
using SeaPowerBorders.Places;

namespace SeaPowerBorders.Map;

/// <summary>The symbols in front of a label: city dot, capital ring, anchor for a naval base, aircraft for an airfield.</summary>
internal static class Icons
{
    // 12 × 12 drawings: an anchor (stroked) and an aircraft seen from above, nose up (filled).
    private const string AnchorData = "M6,0.6 A1.3,1.3 0 1 1 5.99,0.6 M6,2.9 L6,11.2 M3.3,4.6 L8.7,4.6 M1.2,7.4 C1.6,10.2 3.6,11.2 6,11.2 C8.4,11.2 10.4,10.2 10.8,7.4";
    private const string PlaneData = "M6,0.4 C6.6,0.4 6.9,1.4 6.9,2.4 L6.9,4.6 L11.6,7.2 L11.6,8.4 L6.9,6.9 L6.7,9.6 L8.2,10.8 L8.2,11.6 L6,10.9 L3.8,11.6 L3.8,10.8 L5.3,9.6 L5.1,6.9 L0.4,8.4 L0.4,7.2 L5.1,4.6 L5.1,2.4 C5.1,1.4 5.4,0.4 6,0.4 Z";

    private static Geometry anchor, plane;

    public sealed class Palette
    {
        public SolidColorBrush Text, Halo, West, East, Other, Civil;

        public static Palette For(bool dark) => dark
            ? new Palette
            {
                Text = Brush(255, 232, 232, 232), Halo = Brush(225, 12, 16, 20),
                West = Brush(255, 111, 168, 255), East = Brush(255, 255, 107, 96), Other = Brush(255, 170, 170, 170),
                Civil = Brush(255, 150, 150, 150),
            }
            : new Palette
            {
                Text = Brush(255, 34, 40, 49), Halo = Brush(225, 242, 238, 228),
                West = Brush(255, 31, 95, 208), East = Brush(255, 200, 38, 30), Other = Brush(255, 100, 100, 100),
                Civil = Brush(255, 110, 110, 110),
            };

        public SolidColorBrush SideBrush(Bloc side) => side == Bloc.West ? West : side == Bloc.East ? East : Other;
    }

    /// <summary>The symbol for a place and its width (it is drawn centred on the place).</summary>
    public static FrameworkElement For(Place p, Palette c, bool naval, float scale, out float size)
    {
        switch (p.Kind)
        {
            case PlaceKind.AirBase:
                size = 11f * scale;
                return Shape(Plane, size, fill: c.SideBrush(p.AirSide), stroke: c.Halo, thickness: 0.8f);
            case PlaceKind.Airport:
                size = 9f * scale;
                return Shape(Plane, size, fill: c.Civil, stroke: c.Halo, thickness: 0.8f);
        }
        if (naval)
        {
            size = 12f * scale;
            var grid = new Grid { Width = size, Height = size };
            // A light outline under the anchor, so it reads on any colour.
            grid.Children.Add(Shape(Anchor, size, fill: null, stroke: c.Halo, thickness: 3.2f));
            grid.Children.Add(Shape(Anchor, size, fill: null, stroke: c.SideBrush(p.NavalSide), thickness: 1.5f));
            return grid;
        }
        if (p.Capital)
        {
            size = 9f * scale;
            var grid = new Grid { Width = size, Height = size };
            grid.Children.Add(new Ellipse { Width = size, Height = size, Stroke = c.Text, StrokeThickness = 1.5f, Fill = c.Halo });
            grid.Children.Add(new Ellipse { Width = size * 0.42f, Height = size * 0.42f, Fill = c.Text });
            return grid;
        }
        size = (p.Rank <= 1 ? 6f : p.Rank == 2 ? 5f : 4f) * scale;
        return new Ellipse { Width = size, Height = size, Fill = c.Text, Stroke = c.Halo, StrokeThickness = 1f };
    }

    private static Geometry Anchor => anchor ??= Parse(AnchorData);
    private static Geometry Plane => plane ??= Parse(PlaneData);

    // Geometry from path markup, through the XAML parser (shared by every symbol: geometries are not copied).
    private static Geometry Parse(string data)
    {
        var path = (Path)GUI.ParseXaml($"<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Data='{data}'/>");
        return path.Data;
    }

    private static Path Shape(Geometry geometry, float size, Brush fill, Brush stroke, float thickness) => new Path
    {
        Data = geometry,
        Width = size,
        Height = size,
        Stretch = Stretch.Uniform,
        Fill = fill,
        Stroke = stroke,
        StrokeThickness = thickness,
        StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
    };

    private static SolidColorBrush Brush(byte a, byte r, byte g, byte b) => new SolidColorBrush(Color.FromArgb(a, r, g, b));
}
