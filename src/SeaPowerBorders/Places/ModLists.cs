using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SeaPowerBorders.Places;

/// <summary>A rule of <c>eras.csv</c>: what changes with the mission's date.</summary>
internal sealed class EraRule
{
    public string Kind, Country, Name, Value;
    public DateTime? Since, Until;

    public bool AppliesOn(DateTime date) => (Since == null || date >= Since.Value) && (Until == null || date < Until.Value);

    /// <summary>The rule is about this place: same name, and the same country where both know it.</summary>
    public bool Matches(Place place) =>
        string.Equals(Name, place.Name, StringComparison.OrdinalIgnoreCase) && SameCountry(place);

    public bool MatchesAlt(Place place) =>
        place.AltName != null && string.Equals(Name, place.AltName, StringComparison.OrdinalIgnoreCase) && SameCountry(place);

    private bool SameCountry(Place place) => Country == "*" || place.Country == null || Country == place.Country;
}

/// <summary>A base of <c>naval_bases.csv</c>.</summary>
internal sealed class BaseEntry
{
    public string Name;
    public double Lat, Lon;
    public Bloc Side;
    public DateTime? Since, Until;
    public int Rank;

    public bool ActiveOn(DateTime date) => (Since == null || date >= Since.Value) && (Until == null || date < Until.Value);
}

/// <summary>The mod's own lists in <c>places/</c> next to the DLL: semicolon-separated, '#' comment lines.</summary>
internal static class ModLists
{
    public static List<Place> LoadWorld(string dir)
    {
        var places = new List<Place>();
        foreach (string[] f in Rows(Path.Combine(dir, "world.csv"), 6))
        {
            places.Add(new Place
            {
                Name = f[0],
                Lat = D(f[1]),
                Lon = D(f[2]),
                Population = int.Parse(f[3], CultureInfo.InvariantCulture),
                CapitalToday = f[4] == "1",
                Country = f[5],
                Kind = PlaceKind.City,
            });
        }
        return places;
    }

    public static List<BaseEntry> LoadBases(string dir)
    {
        var bases = new List<BaseEntry>();
        foreach (string[] f in Rows(Path.Combine(dir, "naval_bases.csv"), 7))
        {
            bases.Add(new BaseEntry
            {
                Name = f[0],
                Lat = D(f[1]),
                Lon = D(f[2]),
                Side = Enum.TryParse(f[3], true, out Bloc side) ? side : Bloc.Other,
                Since = Date(f[4]),
                Until = Date(f[5]),
                Rank = f[6] == "0" ? 0 : 1,
            });
        }
        return bases;
    }

    public static List<EraRule> LoadEras(string dir)
    {
        var rules = new List<EraRule>();
        foreach (string[] f in Rows(Path.Combine(dir, "eras.csv"), 6))
            rules.Add(new EraRule { Kind = f[0], Country = f[1], Name = f[2], Value = f[3], Since = Date(f[4]), Until = Date(f[5]) });
        return rules;
    }

    // Data rows of a list: the header line (the first that isn't a comment) is skipped, rows with the wrong number
    // of fields are logged and skipped.
    private static IEnumerable<string[]> Rows(string path, int fields)
    {
        if (!File.Exists(path))
        {
            ModLog.Warn($"{Path.GetFileName(path)} missing next to the mod - reinstall it.");
            yield break;
        }
        bool header = true;
        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue;
            if (header)
            {
                header = false;
                continue;
            }
            string[] f = line.Split(';');
            if (f.Length != fields)
            {
                ModLog.Warn($"{Path.GetFileName(path)}: skipped '{line}'");
                continue;
            }
            for (int i = 0; i < f.Length; i++)
                f[i] = f[i].Trim();
            yield return f;
        }
    }

    private static double D(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static DateTime? Date(string s) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d) ? d : (DateTime?)null;
}
