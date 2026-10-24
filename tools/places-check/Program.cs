using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SeaPowerBorders.Borders;
using SeaPowerBorders.Map;
using SeaPowerBorders.Places;

namespace SeaPowerBorders;

/// <summary>Stand-in for the mod's log.</summary>
internal static class ModLog
{
    public static void Info(string m) => Console.WriteLine(m);
    public static void Warn(string m) => Console.WriteLine("WARN " + m);
}

/// <summary>
/// Builds the places of one mission date the way the mod does and prints what it got.
/// <code>dotnet run --project tools/places-check -- [--date 1985-06-26] [--game "E:\Games\steamapps\common\Sea Power"] [--find Leningrad]
///     [--view 62,5,20 --size 1920x1080 --out labels.tsv] [--era Today]</code>
/// --view lat,lon,height draws a map view of that height in degrees: the labels and border lines the mod would show
/// there go to --out (tab-separated, positions in pixels), for tools/places-check/draw.py. --era Today draws
/// today's borders instead of the date's.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        DateTime date = new DateTime(1985, 6, 26);
        string game = @"E:\Games\steamapps\common\Sea Power", find = null, view = null, output = "labels.tsv";
        BorderEra era = BorderEra.MissionDate;
        int width = 1920, height = 1080;
        double waters = 0;
        for (int i = 0; i + 1 < args.Length; i += 2)
        {
            if (args[i] == "--date") date = DateTime.ParseExact(args[i + 1], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            else if (args[i] == "--game") game = args[i + 1];
            else if (args[i] == "--find") find = args[i + 1];
            else if (args[i] == "--view") view = args[i + 1];
            else if (args[i] == "--out") output = args[i + 1];
            else if (args[i] == "--era") era = (BorderEra)Enum.Parse(typeof(BorderEra), args[i + 1], true);
            else if (args[i] == "--waters") waters = double.Parse(args[i + 1], CultureInfo.InvariantCulture);
            else if (args[i] == "--size")
            {
                string[] s = args[i + 1].Split('x');
                width = int.Parse(s[0]);
                height = int.Parse(s[1]);
            }
        }
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        string here = AppContext.BaseDirectory;
        string data = Path.GetFullPath(Path.Combine(here, "..", "..", "..", "..", "..", "src", "SeaPowerBorders", "mod", "places"));
        var gameList = GameLists.Load(Path.Combine(game, "Sea Power_Data", "StreamingAssets", "original", "campaigns"));
        var places = Atlas.Build(gameList, ModLists.LoadWorld(data), ModLists.LoadBases(data), ModLists.LoadEras(data), date);

        Console.WriteLine($"{date:yyyy-MM-dd}: {gameList.Count} from the game -> {places.Count} places");
        foreach (var g in places.GroupBy(p => p.Kind))
            Console.WriteLine($"  {g.Key}: {g.Count()}");
        Console.WriteLine($"  naval: {places.Count(p => p.Naval)} (West {places.Count(p => p.Naval && p.NavalSide == Bloc.West)}, East {places.Count(p => p.Naval && p.NavalSide == Bloc.East)})");
        Console.WriteLine($"  capitals: {places.Count(p => p.Capital)}");
        for (int r = 0; r <= 3; r++)
            Console.WriteLine($"  rank {r}: {places.Count(p => p.Rank == r)}");
        Console.WriteLine("Renamed: " + string.Join(", ", places.Where(p => p.Label != p.Name).Select(p => $"{p.Name}->{p.Label}")));
        Console.WriteLine("Rank 0: " + string.Join(", ", places.Where(p => p.Rank == 0).OrderBy(p => p.Label).Select(p => p.Label + (p.Capital ? "*" : "") + (p.Naval ? "⚓" : ""))));
        if (find != null)
        {
            foreach (Place p in places.Where(p => p.Label.IndexOf(find, StringComparison.OrdinalIgnoreCase) >= 0 || p.Name.IndexOf(find, StringComparison.OrdinalIgnoreCase) >= 0))
                Console.WriteLine($"  {p.Label} [{p.Name}/{p.AltName}] {p.Kind} {p.Country} pop {p.Population} cap {p.Capital} naval {p.Naval} {p.NavalSide} rank {p.Rank} game {p.FromGame} ({p.Lat:0.000}, {p.Lon:0.000})");
        }
        string bordersDir = Path.GetFullPath(Path.Combine(data, "..", "borders"));
        BorderData borders = BorderData.Load(Path.Combine(bordersDir, "borders.bin"));
        BorderEras borderEras = BorderEras.Load(bordersDir);
        var drawn = borders.Lines.Where(l => borderEras.Drawn(l, date, era)).ToHashSet();
        Console.WriteLine($"Borders: {drawn.Count} of {borders.Lines.Count} lines for {date:yyyy-MM-dd} ({era}); not drawn: " +
                          string.Join(", ", borders.Lines.Where(l => !drawn.Contains(l) && l.Class != BorderClass.Added).Select(l => l.Name).Distinct()));
        Console.WriteLine("Added: " + string.Join(", ", borders.Lines.Where(l => drawn.Contains(l) && l.Class == BorderClass.Added).Select(l => l.Name).Distinct()));
        SeaMask sea = null;
        if (waters > 0)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            sea = SeaMask.Load(Path.Combine(game, "Sea Power_Data", "StreamingAssets", "original", "terrain", "data", "lowres.raw"));
            Console.WriteLine($"Sea mask: {timer.ElapsedMilliseconds} ms" + (sea.LandSeeds.Count > 0 ? "; seeds on land: " + string.Join(", ", sea.LandSeeds) : ""));
        }
        if (view != null)
            Draw(places, borders, drawn, sea, waters, view, width, height, output);
        return 0;
    }

    // The map is lon/lat, equirectangular: x by longitude, y by latitude, the same degrees per pixel both ways.
    private static void Draw(List<Place> places, BorderData borders, HashSet<BorderLine> drawn, SeaMask sea, double waters,
                             string view, int width, int height, string output)
    {
        string[] v = view.Split(',');
        double lat = double.Parse(v[0], CultureInfo.InvariantCulture), lon = double.Parse(v[1], CultureInfo.InvariantCulture);
        double span = double.Parse(v[2], CultureInfo.InvariantCulture), dpp = span / height;
        double west = lon - dpp * width / 2, north = lat + span / 2;
        var ordered = places.ToList();
        LabelPicker.Order(ordered, naval: true);
        var area = LabelPicker.Around(west, north - span, west + dpp * width, north);
        Dictionary<Place, bool> picked = LabelPicker.Pick(ordered, span, Density.Normal, naval: true, scale: 1f, area,
            p => ((float)((p.Lon - west) / dpp), (float)((north - p.Lat) / dpp)));
        using var w = new StreamWriter(output);
        w.WriteLine($"#view	{west.ToString(CultureInfo.InvariantCulture)}	{north.ToString(CultureInfo.InvariantCulture)}	{dpp.ToString(CultureInfo.InvariantCulture)}	{width}	{height}");
        // Territorial waters first, then border lines (all below the labels): one row per piece, "#line", the style,
        // then x,y pairs.
        if (sea != null)
        {
            int wl = SeaLimits.LevelFor(1.0 / dpp), pieces = 0, points = 0;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            int tx0 = (int)Math.Floor((west + 180) / BorderData.Tile), tx1 = (int)Math.Floor((west + dpp * width + 180) / BorderData.Tile);
            int ty0 = Math.Max(0, (int)Math.Floor((north - span + 90) / BorderData.Tile)), ty1 = Math.Min(17, (int)Math.Floor((north + 90) / BorderData.Tile));
            int tiles = 0;
            for (int tx = tx0; tx <= tx1; tx++)
            {
                for (int ty = ty0; ty <= ty1; ty++)
                {
                    int wrapped = ((tx % 36) + 36) % 36;
                    SeaLimitTile tile = SeaLimits.Tile(sea, wrapped, ty, waters);
                    tiles++;
                    points += tile.Points;
                    double shift = (tx - wrapped) * BorderData.Tile;
                    foreach (float[] line in tile.Levels[wl])
                    {
                        var xy = new List<string>();
                        for (int k = 0; k < line.Length; k += 2)
                            xy.Add(((line[k] + shift - west) / dpp).ToString("0.0", CultureInfo.InvariantCulture) + "," + ((north - line[k + 1]) / dpp).ToString("0.0", CultureInfo.InvariantCulture));
                        w.WriteLine("#line	waters	" + string.Join(" ", xy));
                        pieces++;
                    }
                }
            }
            Console.WriteLine($"Waters {waters} nm: {tiles} tiles in {timer.ElapsedMilliseconds} ms, {points} points at full detail, {pieces} lines at level {wl}");
        }
        int level = borders.LevelFor(1.0 / dpp), lineCount = 0;
        foreach (BorderPiece piece in borders.Pieces)
        {
            if (piece.Level != level || !drawn.Contains(piece.Line))
                continue;
            var xy = new List<string>();
            // The map runs on past ±180°: like MapPanel, each tile goes within half a turn of the view's centre.
            double offset = piece.TileX * BorderData.Tile - 180 + BorderData.Tile / 2 - (west + dpp * width / 2);
            double shift = offset > 180 ? -360 * Math.Ceiling((offset - 180) / 360) : offset < -180 ? 360 * Math.Ceiling((-offset - 180) / 360) : 0;
            for (int k = 0; k < piece.Points.Length; k += 2)
            {
                double plon = piece.Points[k] + shift;
                xy.Add(((plon - west) / dpp).ToString("0.0", CultureInfo.InvariantCulture) + "," + ((north - piece.Points[k + 1]) / dpp).ToString("0.0", CultureInfo.InvariantCulture));
            }
            w.WriteLine("#line	" + (piece.Line.Dashed ? "dashed" : "solid") + "	" + string.Join(" ", xy));
            lineCount++;
        }
        foreach (Place p in ordered.Where(picked.ContainsKey))
        {
            float x = (float)((p.Lon - west) / dpp), y = (float)((north - p.Lat) / dpp);
            if (x < -50 || y < -20 || x > width + 50 || y > height + 20)
                continue;
            string kind = p.Kind == PlaceKind.AirBase ? "air" : p.Kind == PlaceKind.Airport ? "airport" : p.Naval ? "naval" : p.Capital ? "capital" : "city";
            w.WriteLine(string.Join("	", p.Label, x.ToString("0.0", CultureInfo.InvariantCulture), y.ToString("0.0", CultureInfo.InvariantCulture),
                                    kind, p.Naval ? p.NavalSide.ToString() : p.AirSide.ToString(), p.Rank, picked[p] ? "left" : "right"));
        }
        Console.WriteLine($"{output}: {picked.Count} labels picked for a {span}° view, up to rank {LabelPicker.MaxRank(span, Density.Normal)}; " +
                          $"{lineCount} border pieces at level {level}");
    }
}
