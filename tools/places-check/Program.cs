using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
///     [--view 62,5,20 --size 1920x1080 --out labels.tsv]</code>
/// --view lat,lon,height draws a map view of that height in degrees: the labels the mod would show there go to
/// --out (tab-separated, positions in pixels), for tools/places-check/draw.py.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        DateTime date = new DateTime(1985, 6, 26);
        string game = @"E:\Games\steamapps\common\Sea Power", find = null, view = null, output = "labels.tsv";
        int width = 1920, height = 1080;
        for (int i = 0; i + 1 < args.Length; i += 2)
        {
            if (args[i] == "--date") date = DateTime.ParseExact(args[i + 1], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            else if (args[i] == "--game") game = args[i + 1];
            else if (args[i] == "--find") find = args[i + 1];
            else if (args[i] == "--view") view = args[i + 1];
            else if (args[i] == "--out") output = args[i + 1];
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
        if (view != null)
            Draw(places, view, width, height, output);
        return 0;
    }

    // The map is lon/lat, equirectangular: x by longitude, y by latitude, the same degrees per pixel both ways.
    private static void Draw(List<Place> places, string view, int width, int height, string output)
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
        foreach (Place p in ordered.Where(picked.ContainsKey))
        {
            float x = (float)((p.Lon - west) / dpp), y = (float)((north - p.Lat) / dpp);
            if (x < -50 || y < -20 || x > width + 50 || y > height + 20)
                continue;
            string kind = p.Kind == PlaceKind.AirBase ? "air" : p.Kind == PlaceKind.Airport ? "airport" : p.Naval ? "naval" : p.Capital ? "capital" : "city";
            w.WriteLine(string.Join("	", p.Label, x.ToString("0.0", CultureInfo.InvariantCulture), y.ToString("0.0", CultureInfo.InvariantCulture),
                                    kind, p.Naval ? p.NavalSide.ToString() : p.AirSide.ToString(), p.Rank, picked[p] ? "left" : "right"));
        }
        Console.WriteLine($"{output}: {picked.Count} labels picked for a {span}° view, up to rank {LabelPicker.MaxRank(span, Density.Normal)}");
    }
}
