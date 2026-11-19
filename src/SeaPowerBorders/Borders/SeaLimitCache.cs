using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SeaPowerBorders.Borders;

/// <summary>
/// Territorial-water tiles for the session, worked out on one background thread as the map asks for them: the sea
/// mask is read on the first request, then each tile once per distance. The map asks again each frame and draws a
/// tile once it is here; messages wait for the main thread (<see cref="DrainLog"/>), where the mod's log decides
/// whether a debug line is written.
/// </summary>
internal sealed class SeaLimitCache
{
    private readonly string lowresPath;
    private readonly BlockingCollection<(int x, int y, int nm)> queue = new BlockingCollection<(int x, int y, int nm)>();
    private readonly ConcurrentDictionary<(int x, int y, int nm), SeaLimitTile> done = new ConcurrentDictionary<(int x, int y, int nm), SeaLimitTile>();
    private readonly ConcurrentQueue<(bool debug, string text)> log = new ConcurrentQueue<(bool debug, string text)>();
    private readonly HashSet<(int x, int y, int nm)> asked = new HashSet<(int x, int y, int nm)>();
    private Thread worker;
    private volatile bool failed;

    public SeaLimitCache(string lowresPath) => this.lowresPath = lowresPath;

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
        while (log.TryDequeue(out (bool debug, string text) message))
        {
            if (message.debug)
                ModLog.Debug(message.text);
            else
                ModLog.Info(message.text);
        }
    }

    private void Work()
    {
        SeaMask mask;
        try
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            mask = SeaMask.Load(lowresPath);
            log.Enqueue((false, $"Sea mask read in {timer.ElapsedMilliseconds} ms" +
                        (mask.LandSeeds.Count > 0 ? $" (seeds on land: {string.Join(", ", mask.LandSeeds)})" : "") + "."));
        }
        catch (Exception ex)
        {
            failed = true;
            log.Enqueue((false, $"Could not read the world grid - no territorial waters: {ex.Message}"));
            return;
        }
        foreach ((int x, int y, int nm) key in queue.GetConsumingEnumerable())
        {
            try
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                SeaLimitTile tile = SeaLimits.Tile(mask, key.x, key.y, key.nm);
                done[key] = tile;
                if (tile.Points > 0)
                    log.Enqueue((true, $"Territorial waters {key.nm} nm, tile {key.x},{key.y}: {tile.Points} points, {timer.ElapsedMilliseconds} ms"));
            }
            catch (Exception ex)
            {
                done[key] = SeaLimitTile.Empty();
                log.Enqueue((false, $"Territorial waters failed for tile {key.x},{key.y}: {ex}"));
            }
        }
    }
}
