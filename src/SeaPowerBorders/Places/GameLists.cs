using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SeaPowerBorders.Places;

/// <summary>
/// The game's own campaign lists in <c>StreamingAssets/original/campaigns</c>, read from the player's install:
/// cities (<c>*cities*.ini</c>, Cold War names already), ports (<c>*ports*.ini</c>: a <c>Role</c> with "Military" is a
/// naval base; files without roles list port cities) and airports (<c>airports.ini</c>, ICAO code as section, the
/// name in the comment line under it).
/// </summary>
internal static class GameLists
{
    private sealed class Section
    {
        public string Name, Comment;
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static List<Place> Load(string campaignsDir)
    {
        var places = new List<Place>();
        if (!Directory.Exists(campaignsDir))
        {
            ModLog.Warn($"Game campaign lists not found ({campaignsDir}) - only the mod's own places.");
            return places;
        }
        foreach (string file in Directory.GetFiles(campaignsDir, "*.ini"))
        {
            string name = Path.GetFileName(file).ToLowerInvariant();
            try
            {
                if (name == "airports.ini")
                    AddAirports(file, places);
                else if (name.Contains("cities"))
                    AddCities(file, places);
                else if (name.Contains("ports"))
                    AddPorts(file, places);
            }
            catch (Exception ex)
            {
                ModLog.Warn($"Could not read {name}: {ex.Message}");
            }
        }
        return places;
    }

    private static void AddCities(string file, List<Place> places)
    {
        foreach (Section s in Read(file))
        {
            if (TryLocation(s, out double lat, out double lon))
                places.Add(new Place { Name = s.Name, Lat = lat, Lon = lon, Kind = PlaceKind.City, FromGame = true });
        }
    }

    private static void AddPorts(string file, List<Place> places)
    {
        foreach (Section s in Read(file))
        {
            if (!TryLocation(s, out double lat, out double lon))
                continue;
            string name = s.Name.StartsWith("Port ", StringComparison.OrdinalIgnoreCase) ? s.Name.Substring(5).Trim() : s.Name;
            var place = new Place { Name = name, Lat = lat, Lon = lon, FromGame = true };
            if (!s.Values.TryGetValue("Role", out string role))
            {
                place.Kind = PlaceKind.City;             // a list of port cities (East Asia, the Soviet Pacific)
            }
            else if (role.IndexOf("Military", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                place.Kind = PlaceKind.NavalBase;
                place.Naval = true;
                place.NavalSide = SideOf(s);
                place.NavalRank = role.IndexOf("LargeMilitary", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1;
            }
            else
            {
                place.Kind = PlaceKind.Port;
            }
            places.Add(place);
        }
    }

    private static void AddAirports(string file, List<Place> places)
    {
        foreach (Section s in Read(file))
        {
            if (!TryLocation(s, out double lat, out double lon))
                continue;
            s.Values.TryGetValue("Role", out string role);
            bool military = role != null && role.IndexOf("Military", StringComparison.OrdinalIgnoreCase) >= 0;
            places.Add(new Place
            {
                Name = string.IsNullOrWhiteSpace(s.Comment) ? s.Name : s.Comment,
                Lat = lat,
                Lon = lon,
                Kind = military ? PlaceKind.AirBase : PlaceKind.Airport,
                AirRank = role != null && role.IndexOf("LargeMilitary", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1,
                AirSide = military ? SideOf(s) : Bloc.None,
                FromGame = true,
            });
        }
    }

    // The game's nation names, sorted into the two blocs of the time.
    private static readonly HashSet<string> West = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "US", "USA", "UK", "France", "Germany", "Italy", "Spain", "Netherlands", "Belgium", "Norway", "Denmark", "Canada",
        "Turkey", "Greece", "Portugal", "Iceland", "Luxembourg", "Japan", "Australia", "New Zealand", "South Korea",
        "Philippines", "Taiwan",
    };

    private static readonly HashSet<string> East = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Soviet", "USSR", "GDR", "Poland", "Czechoslovakia", "Hungary", "Romania", "Bulgaria", "Cuba", "Vietnam",
        "North Korea", "Belarus", "Ukraine",
    };

    private static Bloc SideOf(Section s)
    {
        if (!s.Values.TryGetValue("Nation", out string nation))
            return Bloc.Other;
        return West.Contains(nation) ? Bloc.West : East.Contains(nation) ? Bloc.East : Bloc.Other;
    }

    private static bool TryLocation(Section s, out double lat, out double lon)
    {
        lat = lon = 0;
        if (!s.Values.TryGetValue("Location", out string text))
            return false;
        string[] parts = text.Split(',');
        return parts.Length >= 2 &&
               double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out lat) &&
               double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out lon) &&
               lat >= -90 && lat <= 90;
    }

    // A forgiving INI reader: [sections], key=value, ';' comment lines, '//' comments after a value. The first
    // comment line under a section is kept (airports name themselves there). Separator sections ("[---Europe---]")
    // and sections without a location simply don't produce places.
    private static IEnumerable<Section> Read(string file)
    {
        Section current = null;
        foreach (string raw in File.ReadAllLines(file))
        {
            string line = raw.Trim();
            if (line.Length == 0)
                continue;
            if (line.StartsWith(";"))
            {
                if (current != null && current.Comment == null && current.Values.Count == 0)
                    current.Comment = line.TrimStart(';').Trim();
                continue;
            }
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                if (current != null)
                    yield return current;
                current = new Section { Name = line.Substring(1, line.Length - 2).Trim() };
                continue;
            }
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0)
                line = line.Substring(0, comment).Trim();
            int eq = line.IndexOf('=');
            if (current != null && eq > 0)
                current.Values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
        }
        if (current != null)
            yield return current;
    }
}
