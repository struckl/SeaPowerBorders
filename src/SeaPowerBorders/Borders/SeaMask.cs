using System;
using System.Collections.Generic;
using System.IO;

namespace SeaPowerBorders.Borders;

/// <summary>
/// The game's 3′ world grid (<c>terrain/data/lowres.raw</c>: 7200 × 3600 int16 metres from the north-west corner,
/// the grid behind the zoomed-out map) as sea or not: sea is water connected to the oceans, flooded from points in
/// the oceans and in the seas a 3′ grid may cut off (the Bosporus and the Danish straits are narrower than a cell).
/// Lakes and dry land below sea level - the Caspian, the Great Lakes, the Dead Sea, the Qattara Depression - stay
/// out, so they get no territorial waters. Plain C#, read once on a worker thread.
/// </summary>
internal sealed class SeaMask
{
    public const int Cols = 7200, Rows = 3600;
    public const double Cell = 0.05;

    // Water points in each ocean and in the seas that may be cut off from it at 3′. Seeds that turn out to be land
    // are skipped (and logged by the caller).
    private static readonly (string name, double lat, double lon)[] Seeds =
    {
        ("North Atlantic", 40, -40), ("South Atlantic", -30, -15), ("North Pacific", 30, -150), ("South Pacific", -30, -120),
        ("Indian Ocean", -20, 80), ("Arctic Ocean", 85, 0), ("Southern Ocean", -60, 0), ("Mediterranean", 35, 18),
        ("Sea of Marmara", 40.75, 28.2), ("Black Sea", 43, 34), ("Sea of Azov", 46, 36.5), ("Baltic Sea", 57, 19),
        ("Gulf of Bothnia", 62, 20), ("Gulf of Finland", 59.8, 26), ("Gulf of Riga", 57.6, 23.5), ("White Sea", 65.5, 38),
        ("Red Sea", 20, 38.5), ("Persian Gulf", 27, 51), ("Sea of Japan", 40, 135), ("Sea of Okhotsk", 55, 150),
        ("Yellow Sea", 35, 123), ("Hudson Bay", 60, -85), ("Gulf of Mexico", 25, -90), ("Caribbean", 15, -75),
        ("Gulf of St. Lawrence", 48, -62),
    };

    private readonly ulong[] water = new ulong[(Cols * Rows + 63) / 64];
    private readonly ulong[] sea = new ulong[(Cols * Rows + 63) / 64];

    /// <summary>Seeds that were on land in this grid.</summary>
    public readonly List<string> LandSeeds = new List<string>();

    public static SeaMask Load(string path)
    {
        var mask = new SeaMask();
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
        {
            if (stream.Length != (long)Cols * Rows * 2)
                throw new InvalidDataException($"{Path.GetFileName(path)}: {stream.Length} bytes, expected {(long)Cols * Rows * 2}");
            var row = new byte[Cols * 2];
            for (int y = 0; y < Rows; y++)
            {
                int read = 0;
                while (read < row.Length)
                {
                    int n = stream.Read(row, read, row.Length - read);
                    if (n <= 0)
                        throw new EndOfStreamException(path);
                    read += n;
                }
                for (int x = 0; x < Cols; x++)
                {
                    if ((short)(row[2 * x] | (row[2 * x + 1] << 8)) < 0)
                        Set(mask.water, y * Cols + x);
                }
            }
        }
        foreach ((string name, double lat, double lon) in Seeds)
        {
            int x = (int)Math.Floor((lon + 180.0) / Cell), y = (int)Math.Floor((90.0 - lat) / Cell);
            if (!Get(mask.water, Index(x, y)))
                mask.LandSeeds.Add(name);
            else
                mask.Flood(x, y);
        }
        return mask;
    }

    /// <summary>Whether a world grid cell is sea; x wraps round the world, y is clamped.</summary>
    public bool IsSea(int x, int y) => Get(sea, Index(x, y));

    private static int Index(int x, int y)
    {
        x %= Cols;
        if (x < 0) x += Cols;
        if (y < 0) y = 0;
        else if (y >= Rows) y = Rows - 1;
        return y * Cols + x;
    }

    private bool Open(int x, int y)
    {
        int i = Index(x, y);
        return Get(water, i) && !Get(sea, i);
    }

    // Span flood fill: each popped seed fills its whole run of water in the row (wrapping round the world) and
    // pushes one seed per run of open water above and below it.
    private void Flood(int x0, int y0)
    {
        var stack = new Stack<long>();
        stack.Push(((long)y0 << 32) | (uint)x0);
        while (stack.Count > 0)
        {
            long s = stack.Pop();
            int x = (int)(s & 0xFFFFFFFF), y = (int)(s >> 32);
            if (!Open(x, y))
                continue;
            int left = x, guard = 0;
            while (guard++ < Cols && Open(left - 1, y))
                left--;
            bool upOpen = false, downOpen = false;
            for (int cx = left, n = 0; n < Cols && Open(cx, y); cx++, n++)
            {
                Set(sea, Index(cx, y));
                if (y > 0)
                {
                    bool open = Open(cx, y - 1);
                    if (open && !upOpen)
                        stack.Push(((long)(y - 1) << 32) | (uint)Mod(cx));
                    upOpen = open;
                }
                if (y < Rows - 1)
                {
                    bool open = Open(cx, y + 1);
                    if (open && !downOpen)
                        stack.Push(((long)(y + 1) << 32) | (uint)Mod(cx));
                    downOpen = open;
                }
            }
        }
    }

    private static int Mod(int x) => ((x % Cols) + Cols) % Cols;

    private static bool Get(ulong[] bits, int i) => (bits[i >> 6] & (1UL << (i & 63))) != 0;

    private static void Set(ulong[] bits, int i) => bits[i >> 6] |= 1UL << (i & 63);
}
