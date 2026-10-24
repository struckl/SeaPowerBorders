using System;

namespace SeaPowerBorders.Places;

internal enum PlaceKind { City, NavalBase, AirBase, Airport, Port }

/// <summary>Who a base belongs to: the US and its allies, the Soviet Union and its allies, anyone else.</summary>
internal enum Bloc { None, West, East, Other }

/// <summary>One place on the map, from the game's campaign lists, Natural Earth or the mod's own lists.</summary>
internal sealed class Place
{
    /// <summary>Name in the source: the game's (already of the time) or Natural Earth's (today's).</summary>
    public string Name;
    /// <summary>The name another list has for the same place (Natural Earth's "St. Petersburg" for the game's
    /// "Leningrad"), so the era rules, written for Natural Earth's names, find it.</summary>
    public string AltName;
    /// <summary>Natural Earth's three-letter country code; null for the game's places.</summary>
    public string Country;
    public double Lat, Lon;
    /// <summary>Inhabitants (Natural Earth); 0 = unknown.</summary>
    public int Population;
    /// <summary>A national capital today (Natural Earth); the era rules decide about the mission's date.</summary>
    public bool CapitalToday;
    public PlaceKind Kind;

    /// <summary>A naval base here (also for a city that has one): anchor in the side's colour.</summary>
    public bool Naval;
    public Bloc NavalSide;
    /// <summary>0 = main fleet base, 1 = other base.</summary>
    public int NavalRank = 1;
    /// <summary>Air base or airport: how big the military side is (0 = large).</summary>
    public int AirRank = 1;
    public Bloc AirSide;

    /// <summary>The game's own lists win over Natural Earth when both have a place.</summary>
    public bool FromGame;
    /// <summary>Only merged with a place of the same name, never with a neighbour (West Berlin next to Berlin).</summary>
    public bool Distinct;

    // For the mission's date (see Atlas.Resolve):
    public string Label;
    public bool Capital;
    /// <summary>0 = seen from far out (capitals, main bases, the biggest cities) … 3 = only close in.</summary>
    public int Rank;

    public Place Clone() => (Place)MemberwiseClone();

    public override string ToString() => $"{Name} ({Lat:0.00}, {Lon:0.00}) {Kind}";

    /// <summary>Great-circle distance in km.</summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0, D = Math.PI / 180.0;
        double dLat = (lat2 - lat1) * D, dLon = (lon2 - lon1) * D;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * D) * Math.Cos(lat2 * D) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }
}
