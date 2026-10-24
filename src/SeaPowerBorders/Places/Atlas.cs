using System;
using System.Collections.Generic;

namespace SeaPowerBorders.Places;

/// <summary>
/// Every place of a mission: the game's lists first, then the mod's naval bases and the extra places of the
/// mission's date, then Natural Earth for the rest of the world. A place another list already has (the same name
/// within <see cref="SameNameKm"/>, or anything within <see cref="SamePlaceKm"/>) is merged into it instead of
/// drawn twice – the first list's name stays, the others add population, country and the naval base. Then the
/// mission's date decides names, capitals and how far out each place shows.
/// </summary>
internal static class Atlas
{
    private const double SameNameKm = 60, SamePlaceKm = 6;

    public static List<Place> Build(List<Place> game, List<Place> world, List<BaseEntry> bases, List<EraRule> eras, DateTime date)
    {
        var places = new List<Place>();
        foreach (Place p in game)
            Merge(places, p.Clone());
        foreach (BaseEntry b in bases)
        {
            if (b.ActiveOn(date))
            {
                Merge(places, new Place
                {
                    Name = b.Name, Lat = b.Lat, Lon = b.Lon, Kind = PlaceKind.NavalBase,
                    Naval = true, NavalSide = b.Side, NavalRank = b.Rank,
                });
            }
        }
        foreach (EraRule r in eras)
        {
            if (r.Kind == "place" && r.AppliesOn(date) && TryPlace(r, out Place extra))
                Merge(places, extra);
        }
        foreach (Place p in world)
            Merge(places, p.Clone());
        foreach (Place p in places)
            Resolve(p, eras, date);
        return places;
    }

    private static bool Aerial(PlaceKind kind) => kind == PlaceKind.AirBase || kind == PlaceKind.Airport;

    private static void Merge(List<Place> places, Place p)
    {
        Place match = null;
        foreach (Place q in places)
        {
            if (Aerial(q.Kind) != Aerial(p.Kind) || Math.Abs(q.Lat - p.Lat) > 0.6)
                continue;
            double km = Place.DistanceKm(q.Lat, q.Lon, p.Lat, p.Lon);
            // An extra place of the time (West Berlin) sits next to one that exists today on purpose.
            bool near = km < SamePlaceKm && !q.Distinct && !p.Distinct;
            if (near || (km < SameNameKm && SameName(q, p)))
            {
                match = q;
                break;
            }
        }
        if (match == null)
        {
            places.Add(p);
            return;
        }

        if (!string.Equals(match.Name, p.Name, StringComparison.OrdinalIgnoreCase) && match.AltName == null)
            match.AltName = p.Name;           // Natural Earth's name, so the era rules find the place
        if (match.Country == null)
            match.Country = p.Country;
        match.Population = Math.Max(match.Population, p.Population);
        match.CapitalToday |= p.CapitalToday;
        if (p.Kind == PlaceKind.City)
            match.Kind = PlaceKind.City;      // a city with a naval base stays a city, with an anchor
        if (p.Naval)
        {
            if (!match.Naval || p.NavalRank < match.NavalRank)
                match.NavalRank = p.NavalRank;
            if (!match.Naval || match.NavalSide == Bloc.Other)
                match.NavalSide = p.NavalSide;
            match.Naval = true;
        }
    }

    private static bool SameName(Place a, Place b) =>
        string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase) ||
        (a.AltName != null && string.Equals(a.AltName, b.Name, StringComparison.OrdinalIgnoreCase));

    private static bool TryPlace(EraRule r, out Place place)
    {
        place = null;
        string[] v = r.Value.Split(',');
        if (v.Length != 3 ||
            !double.TryParse(v[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lat) ||
            !double.TryParse(v[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lon) ||
            !int.TryParse(v[2], out int population))
        {
            ModLog.Warn($"eras.csv: place '{r.Name}' needs lat,lon,population");
            return false;
        }
        place = new Place
        {
            Name = r.Name, Country = r.Country, Lat = lat, Lon = lon, Population = population, Kind = PlaceKind.City,
            Distinct = true,
        };
        return true;
    }

    // Name and capital on that date, and the rank: how far out the place shows.
    private static void Resolve(Place p, List<EraRule> eras, DateTime date)
    {
        p.Label = p.Name;
        p.Capital = p.CapitalToday;
        foreach (EraRule r in eras)
        {
            if (r.Kind == "place" || !r.AppliesOn(date) || !(r.Matches(p) || r.MatchesAlt(p)))
                continue;
            switch (r.Kind)
            {
                case "rename":
                    // A name the game already gives (Leningrad) stays; today's names get the old one.
                    if (!p.FromGame || string.Equals(p.Name, r.Name, StringComparison.OrdinalIgnoreCase))
                        p.Label = r.Value;
                    break;
                case "capital":
                    p.Capital = true;
                    break;
                case "notcapital":
                    p.Capital = false;
                    break;
            }
        }
        p.Rank = RankOf(p);
    }

    private static int RankOf(Place p)
    {
        switch (p.Kind)
        {
            case PlaceKind.AirBase:
                return p.AirRank == 0 ? 2 : 3;
            case PlaceKind.Airport:
                return 3;
        }
        int pop = p.Population, rank;
        if (p.Capital)
            rank = pop >= 2_500_000 ? 0 : pop >= 150_000 || pop == 0 ? 1 : 2;   // Monaco needs no label from afar
        else if (pop >= 8_000_000)
            rank = 0;
        else if (pop >= 2_000_000)
            rank = 1;
        else if (pop >= 750_000)
            rank = 2;
        else
            rank = 3;
        if (p.Naval)
            rank = Math.Min(rank, p.NavalRank);
        return rank;
    }
}
