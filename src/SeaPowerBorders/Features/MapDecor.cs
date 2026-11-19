using System.Collections.Generic;
using System.Collections.ObjectModel;
using MapControl;
using SeaPowerBorders.Map;

namespace SeaPowerBorders.Features;

/// <summary>The mod's layers on one map control: its border lines and its labels, either of which may have failed.</summary>
internal sealed class MapDecor
{
    public readonly MapBase Map;
    public readonly BorderLines Borders;
    public readonly CityLabels Labels;

    private MapDecor(MapBase map, BorderLines borders, CityLabels labels)
    {
        Map = map;
        Borders = borders;
        Labels = labels;
    }

    /// <summary>Lays both layers into the map (into <paramref name="background"/> for the editor's map); null if neither went in.</summary>
    public static MapDecor TryMake(MapBase map, ObservableCollection<object> background = null)
    {
        var decor = new MapDecor(map, BorderLines.TryAttach(map, background), CityLabels.TryAttach(map, background));
        return decor.Borders == null && decor.Labels == null ? null : decor;
    }

    /// <summary>False once the map has left the visual tree (window closed, mission over).</summary>
    public bool Alive => Noesis.VisualTreeHelper.GetParent(Map) != null;

    public void Detach()
    {
        Borders?.Detach();
        Labels?.Detach();
    }
}

/// <summary>Every map control the mod has laid its layers into, and those it could not.</summary>
internal sealed class DecoratedMaps
{
    private readonly List<MapDecor> maps = new List<MapDecor>();
    private readonly HashSet<MapBase> undecoratable = new HashSet<MapBase>();

    public IReadOnlyList<MapDecor> All => maps;

    public bool Knows(MapBase map)
    {
        if (undecoratable.Contains(map))
            return true;
        foreach (MapDecor known in maps)
        {
            if (known.Map == map)
                return true;
        }
        return false;
    }

    public void Add(MapDecor decor) => maps.Add(decor);

    public void AddUndecoratable(MapBase map) => undecoratable.Add(map);

    public void Remove(MapDecor decor)
    {
        decor.Detach();
        maps.Remove(decor);
    }

    /// <summary>Lets go of the maps that have closed.</summary>
    public void Prune()
    {
        for (int i = maps.Count - 1; i >= 0; i--)
        {
            if (!maps[i].Alive)
                Remove(maps[i]);
        }
    }

    public void Clear()
    {
        foreach (MapDecor map in maps)
            map.Detach();
        maps.Clear();
        undecoratable.Clear();
    }
}
