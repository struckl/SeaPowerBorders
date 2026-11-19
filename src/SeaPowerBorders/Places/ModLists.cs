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

    public bool AppliesOn(DateTime date) => DataFiles.InRange(date, Since, Until);

    /// <summary>
    /// The rule is about this place: same name, and the same country where both know it. Exact names: the rules are
    /// written for one list's spelling (Natural Earth's, or the game's through <see cref="Place.AltName"/>).
    /// </summary>
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

    public bool ActiveOn(DateTime date) => DataFiles.InRange(date, Since, Until);
}

/// <summary>The mod's own lists in <c>places/</c> next to the DLL: semicolon-separated, '#' comment lines.</summary>
internal static class ModLists
{
    public static List<Place> LoadWorld(string dir)
    {
        var places = new List<Place>();
        foreach (string[] f in DataFiles.Rows(Path.Combine(dir, "world.csv"), 6))
        {
            places.Add(new Place
            {
                Name = f[0],
                Lat = DataFiles.Number(f[1]),
                Lon = DataFiles.Number(f[2]),
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
        foreach (string[] f in DataFiles.Rows(Path.Combine(dir, "naval_bases.csv"), 7))
        {
            bases.Add(new BaseEntry
            {
                Name = f[0],
                Lat = DataFiles.Number(f[1]),
                Lon = DataFiles.Number(f[2]),
                Side = Enum.TryParse(f[3], true, out Bloc side) ? side : Bloc.Other,
                Since = DataFiles.Date(f[4]),
                Until = DataFiles.Date(f[5]),
                Rank = f[6] == "0" ? 0 : 1,
            });
        }
        return bases;
    }

    public static List<EraRule> LoadEras(string dir)
    {
        var rules = new List<EraRule>();
        foreach (string[] f in DataFiles.Rows(Path.Combine(dir, "eras.csv"), 6))
            rules.Add(new EraRule { Kind = f[0], Country = f[1], Name = f[2], Value = f[3], Since = DataFiles.Date(f[4]), Until = DataFiles.Date(f[5]) });
        return rules;
    }

}
