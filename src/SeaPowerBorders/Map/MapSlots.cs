using System;
using System.Collections.ObjectModel;
using MapControl;
using Noesis;

namespace SeaPowerBorders.Map;

/// <summary>
/// Where the mod's panels go in a map control: right above its graticule (below the scale, the drag surface and the
/// units), territorial waters below the border lines below the place labels - whichever is laid in first.
/// </summary>
internal static class MapSlots
{
    public const string Waters = "sewerlabs.waters";
    public const string Borders = "sewerlabs.borders";
    public const string Cities = "sewerlabs.cities";
    private static readonly string[] Order = { Waters, Borders, Cities };

    /// <summary>
    /// Lays a panel into the map. In the mission editor's map (<paramref name="background"/>, the map view model's
    /// background elements) it goes in there instead, after the tiles and before the units - the game builds that map
    /// the same way, and Topo's overlay sits there too; panels laid in first stay below the ones laid in later.
    /// </summary>
    public static void Insert(MapBase map, MapPanel panel, string slot, ObservableCollection<object> background = null)
    {
        panel.Tag = slot;
        if (background != null)
        {
            int at = background.Count;
            for (int i = 0; i < background.Count; i++)
            {
                if (background[i] is FrameworkElement e && e.Tag is string other && Array.IndexOf(Order, other) is int r && r > Array.IndexOf(Order, slot))
                {
                    at = i;
                    break;
                }
            }
            background.Insert(at, panel);
            return;
        }
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

    public static void Remove(MapBase map, MapPanel panel, ObservableCollection<object> background = null)
    {
        if (background != null)
            background.Remove(panel);
        else
            map.Children.Remove(panel);
    }
}
