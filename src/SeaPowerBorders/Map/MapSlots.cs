using System;
using MapControl;
using Noesis;

namespace SeaPowerBorders.Map;

/// <summary>
/// Where the mod's panels go in a map control: right above its graticule (below the scale, the drag surface and the
/// units), the border lines below the place labels - whichever is laid in first.
/// </summary>
internal static class MapSlots
{
    public const string Borders = "sewerlabs.borders";
    public const string Cities = "sewerlabs.cities";
    private static readonly string[] Order = { Borders, Cities };

    public static void Insert(MapBase map, MapPanel panel, string slot)
    {
        panel.Tag = slot;
        int rank = Array.IndexOf(Order, slot);
        int index = Math.Min(1, map.Children.Count);
        for (int i = 0; i < map.Children.Count; i++)
        {
            if (map.Children[i] is MapGraticule)
            {
                index = i + 1;
                break;
            }
        }
        // Past our panels that go below this one.
        while (index < map.Children.Count && map.Children[index] is FrameworkElement e && e.Tag is string other
               && Array.IndexOf(Order, other) is int r && r >= 0 && r < rank)
            index++;
        map.Children.Insert(index, panel);
    }
}
