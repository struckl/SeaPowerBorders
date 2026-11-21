using System;
using System.Collections.Generic;
using SeaPower;
using SeapowerUI;

namespace SeaPowerBorders.Places;

/// <summary>
/// Reads the places a mission draws itself: its land units (a port, an airfield, a SAM site) and its city markers
/// (the mission's background data: <c>MissionManager.OnMapPoints</c>, a red dot with the city's name). See
/// <see cref="MarkedPlaces"/> for what is done with them.
/// </summary>
internal static class GameMarkers
{
    private static bool unitsFailed, markersFailed;

    public static List<(double lat, double lon, string name)> Read()
    {
        var now = new List<(double lat, double lon, string name)>();
        try
        {
            AddLandUnits(now);
        }
        catch (Exception ex)
        {
            WarnOnce(ref unitsFailed, "land units", ex);
        }
        try
        {
            AddCityMarkers(now);
        }
        catch (Exception ex)
        {
            WarnOnce(ref markersFailed, "city markers", ex);
        }
        return now;
    }

    private static void AddLandUnits(List<(double lat, double lon, string name)> now)
    {
        foreach (Taskforce tf in new[] { Globals._playerTaskforce, Globals._enemyTaskforce, Globals._neutralTaskforce })
        {
            if (tf?._taskforceLandUnits == null)
                continue;
            foreach (ObjectBase u in tf._taskforceLandUnits)
            {
                if (u == null || u.IsDestroyed)
                    continue;
                GeoPosition g = u.getGeoPosition();
                now.Add((g._latitude, g._longitude, u.getName() ?? ""));
            }
        }
    }

    private static void AddCityMarkers(List<(double lat, double lon, string name)> now)
    {
        if (!Singleton<MissionManager>.InstanceExists())
            return;
        foreach (OnMapPointViewModel pt in Singleton<MissionManager>.Instance.OnMapPoints)
        {
            if (pt == null || !pt.Visible)
                continue;
            GeoPosition g = pt.Position;
            now.Add((g._latitude, g._longitude, pt.Label ?? ""));
        }
    }

    // A game update that breaks the read must show in the log: the places would be labelled twice again.
    private static void WarnOnce(ref bool warned, string what, Exception ex)
    {
        if (warned)
            return;
        warned = true;
        Rig.Log.Warn($"Could not read the mission's {what} (game updated?) - places they mark may be labelled twice: {ex.Message}");
    }
}
