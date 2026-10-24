using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SeaPowerBorders.Borders;

/// <summary>
/// Territorial-water tiles for the session, worked out on one background thread as the map asks for them: the sea
/// mask is read on the first request, then each tile once per distance. The map asks again each frame and draws a
/// tile once it is here; messages wait for the main thread (<see cref="DrainLog"/>).
/// </summary>
internal sealed class SeaLimitCache
{
    private readonly string lowresPath;
    private readonly Func<bool> verbose;
    private readonly BlockingCollection<(int x, int y, int nm)> queue = new BlockingCollection<(int x, int y, int nm)>();
    private readonly ConcurrentDictionary<(int x, int y, int nm), SeaLimitTile> done = new ConcurrentDictionary<(int x, int y, int nm), SeaLimitTile>();
    private readonly ConcurrentQueue<string> log = new ConcurrentQueue<string>();
    private readonly HashSet<(int x, int y, int nm)> asked = new HashSet<(int x, int y, int nm)>();
    private Thread worker;
    private volatile bool failed;

    public SeaLimitCache(string lowresPath, Func<bool> verbose)
    {
        this.lowresPath = lowresPath;
        this.verbose = verbose;
    }

    public bool Failed => failed;

    /// <summary>The tile if it is ready; otherwise asks for it (once) and returns null.</summary>
    public SeaLimitTile Get(int x, int y, int nm)
    {
        var key = (x, y, nm);
        if (done.TryGetValue(key, out SeaLimitTile tile))
            return tile;
        if (failed || !asked.Add(key))
            return null;
        if (worker == null)
        {
            worker = new Thread(Work) { IsBackground = true, Name = "SeaPowerBorders territorial waters", Priority = ThreadPriority.BelowNormal };
            worker.Start();
        }
        queue.Add(key);
        return null;
    }

    /// <summary>Writes what the worker had to say to the mod's log (main thread).</summary>
    public void DrainLog()
    {
        while (log.TryDequeue(out string message))
            ModLog.Info(message);
    }

    private void Work()
    {
        SeaMask mask;
        try
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            mask = SeaMask.Load(lowresPath);
            log.Enqueue($"Sea mask read in {timer.ElapsedMilliseconds} ms" +
                        (mask.LandSeeds.Count > 0 ? $" (seeds on land: {string.Join(", ", mask.LandSeeds)})" : "") + ".");
        }
        catch (Exception ex)
        {
            failed = true;
            log.Enqueue($"Could not read the world grid - no territorial waters: {ex.Message}");
            return;
        }
        foreach ((int x, int y, int nm) key in queue.GetConsumingEnumerable())
        {
            try
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                SeaLimitTile tile = SeaLimits.Tile(mask, key.x, key.y, key.nm);
                done[key] = tile;
                if (tile.Points > 0 && verbose())
                    log.Enqueue($"[dbg] Territorial waters {key.nm} nm, tile {key.x},{key.y}: {tile.Points} points, {timer.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                done[key] = new SeaLimitTile { TileX = key.x, TileY = key.y, Levels = Array.ConvertAll(SeaLimits.Tolerances, _ => new List<float[]>()) };
                log.Enqueue($"Territorial waters failed for tile {key.x},{key.y}: {ex}");
            }
        }
    }
}
