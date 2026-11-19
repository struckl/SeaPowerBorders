using System;
using System.Collections.Generic;
using SeaPowerBorders.Borders;

namespace SeaPowerBorders.Map;

/// <summary>
/// The coastal distance line's tiles one drawing has taken from the <see cref="SeaLimitCache"/>, each turned into
/// whatever the drawing keeps for it (<typeparamref name="T"/>). The line shows only once zoomed in far enough that
/// it stands clear of the coast; a new level of detail or distance starts over.
/// </summary>
internal sealed class CoastalTiles<T>
{
    private const double MinOffsetPixels = 5.0;

    private readonly Dictionary<(int x, int y), T> tiles = new Dictionary<(int x, int y), T>();
    private readonly Action cleared;
    private int nm = -1;

    /// <param name="cleared">Called when tiles were dropped, so the drawing empties what it drew of them.</param>
    public CoastalTiles(Action cleared) => this.cleared = cleared;

    public int Level { get; private set; } = -1;

    public IEnumerable<T> Drawn => tiles.Values;

    public bool Any => tiles.Count > 0;

    /// <summary>
    /// Takes the tiles of the box (lon/lat, at most a turn of columns; null when the view has none) that have come in
    /// and were not taken yet; asks the cache for the others. True when a tile was added.
    /// </summary>
    public bool Fetch(SeaLimitCache waters, int distanceNm, double pixelsPerDegree, (double west, double south, double east, double north)? box,
                      bool allLongitudes, Func<SeaLimitTile, T> take)
    {
        if (!Shown(waters, distanceNm, pixelsPerDegree))
        {
            Clear();
            return false;
        }
        int wanted = SeaLimits.LevelFor(pixelsPerDegree);
        if (wanted != Level || distanceNm != nm)
        {
            Clear();
            Level = wanted;
            nm = distanceNm;
        }
        if (box == null)
            return false;
        bool added = false;
        (double west, double south, double east, double north) = box.Value;
        foreach ((int tx, int ty) in Tiles.In(west, south, east, north, allLongitudes))
        {
            int x = Tiles.Wrap(tx);
            if (!SeaLimits.Covered(ty) || tiles.ContainsKey((x, ty)))
                continue;
            SeaLimitTile tile = waters.Get(x, ty, nm);
            if (tile == null)
                continue;
            tiles[(x, ty)] = take(tile);
            added = true;
        }
        return added;
    }

    public void Clear()
    {
        if (tiles.Count == 0)
            return;
        tiles.Clear();
        cleared();
    }

    private static bool Shown(SeaLimitCache waters, int distanceNm, double pixelsPerDegree) =>
        waters != null && !waters.Failed && distanceNm > 0 && distanceNm / 60.0 * pixelsPerDegree >= MinOffsetPixels;
}
