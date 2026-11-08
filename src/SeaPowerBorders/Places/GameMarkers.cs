using System;
using System.Collections.Generic;
using SeaPower;
using SeapowerUI;

namespace SeaPowerBorders.Places;

/// <summary>
/// The places a mission draws itself: its land units (a port, an airfield, a SAM site) and its city markers (the mission's background
/// data: <c>MissionManager.OnMapPoints</c>, a red dot with the city's name) get a marker and a name from the game. Borders keeps its
/// own label off a place that one of them already marks, so a place is not written twice (the Battle of Latakia showed "Latakia"
/// twice: the game's port marker and our city label; the Yom Kippur War missions showed every city twice: the game's red dot and ours).
/// </summary>
internal static class GameMarkers
{
    private static readonly List<(double lat, double lon, string name)> Units = new List<(double, double, string)>();

    /// <summary>Reads the land units of every taskforce again; true when the list changed.</summary>
    public static bool Collect()
    {
        var now = new List<(double, double, string)>();
        try
        {
            foreach (Taskforce tf in new[] { Globals._playerTaskforce, Globals._enemyTaskforce, Globals._neutralTaskforce })
            {
                if (tf?._taskforceLandUnits == null) continue;
                foreach (ObjectBase u in tf._taskforceLandUnits)
                {
                    if (u == null || u.IsDestroyed) continue;
                    GeoPosition g = u.getGeoPosition();
                    now.Add((g._latitude, g._longitude, u.getName() ?? ""));
                }
            }
        }
        catch (Exception ex) { ModLog.Debug("land units: " + ex.Message); }
        try
        {
            if (Singleton<MissionManager>.InstanceExists())
                foreach (OnMapPointViewModel pt in Singleton<MissionManager>.Instance.OnMapPoints)
                {
                    if (pt == null || !pt.Visible) continue;
                    GeoPosition g = pt.Position;
                    now.Add((g._latitude, g._longitude, pt.Label ?? ""));
                }
        }
        catch (Exception ex) { ModLog.Debug("city markers: " + ex.Message); }
        bool changed = now.Count != Units.Count;
        for (int i = 0; !changed && i < now.Count; i++)
            changed = Math.Abs(now[i].Item1 - Units[i].lat) > 1e-5 || Math.Abs(now[i].Item2 - Units[i].lon) > 1e-5;
        if (changed)
        {
            Units.Clear();
            Units.AddRange(now.ConvertAll(x => (x.Item1, x.Item2, x.Item3)));
        }
        return changed;
    }

    public static void Clear() => Units.Clear();

    /// <summary>The game already marks this place: a land unit on it, or a few km away with the same name.</summary>
    public static bool Marks(Place p)
    {
        foreach (var u in Units)
        {
            double d = Place.DistanceKm(p.Lat, p.Lon, u.lat, u.lon);
            if (d < 1.5) return true;
            if (d < 12 && SameName(p.Name, u.name)) return true;
        }
        return false;
    }

    private static bool SameName(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        a = a.Trim(); b = b.Trim();
        return a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0 || b.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
