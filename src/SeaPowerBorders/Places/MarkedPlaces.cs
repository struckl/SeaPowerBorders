using System;
using System.Collections.Generic;

namespace SeaPowerBorders.Places;

/// <summary>
/// The places the running mission marks itself (<see cref="GameMarkers"/>): its land units and city markers get a
/// marker and a name from the game. Borders keeps its own label off a place that one of them already marks, so a
/// place is not written twice (the Battle of Latakia showed "Latakia" twice: the game's port marker and our city
/// label; the Yom Kippur War missions showed every city twice: the game's red dot and ours). Plain C#.
/// </summary>
internal sealed class MarkedPlaces
{
    private const double SameSpotKm = 1.5, SameNameKm = 12;

    private readonly List<(double lat, double lon, string name)> marks = new List<(double lat, double lon, string name)>();

    /// <summary>Takes the marks read now; true when they moved, came or went.</summary>
    public bool Replace(List<(double lat, double lon, string name)> now)
    {
        bool changed = now.Count != marks.Count;
        for (int i = 0; !changed && i < now.Count; i++)
            changed = Math.Abs(now[i].lat - marks[i].lat) > 1e-5 || Math.Abs(now[i].lon - marks[i].lon) > 1e-5;
        if (changed)
        {
            marks.Clear();
            marks.AddRange(now);
        }
        return changed;
    }

    public void Clear() => marks.Clear();

    /// <summary>The game already marks this place: a mark on it, or a few km away with the same name.</summary>
    public bool Marks(Place p)
    {
        foreach ((double lat, double lon, string name) in marks)
        {
            double d = Place.DistanceKm(p.Lat, p.Lon, lat, lon);
            if (d < SameSpotKm || (d < SameNameKm && SameName(p.Name, name)))
                return true;
        }
        return false;
    }

    // One name inside the other, unlike Atlas: a unit or marker is named after its place with more words around it,
    // and the short distance keeps the loose match local.
    private static bool SameName(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;
        a = a.Trim();
        b = b.Trim();
        return a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0 || b.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
