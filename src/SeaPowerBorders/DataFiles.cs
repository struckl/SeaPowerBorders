using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SeaPowerBorders;

/// <summary>
/// Reading the mod's own lists (<c>places/*.csv</c>, <c>borders/eras.csv</c>): semicolon-separated, '#' comment
/// lines, one header row; dates as yyyy-MM-dd and numbers in the invariant culture whatever the player's locale.
/// Plain C#, so the offline checks read the lists the same way.
/// </summary>
internal static class DataFiles
{
    // Data rows of a list: the header line (the first that isn't a comment) is skipped, rows with the wrong number
    // of fields are logged and skipped. Up to <optional> fields may be left off the end of a row (they come back empty).
    public static IEnumerable<string[]> Rows(string path, int fields, int optional = 0)
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
            if (f.Length < fields && f.Length >= fields - optional)
                Array.Resize(ref f, fields);
            if (f.Length != fields)
            {
                ModLog.Warn($"{Path.GetFileName(path)}: skipped '{line}'");
                continue;
            }
            for (int i = 0; i < f.Length; i++)
                f[i] = f[i]?.Trim() ?? "";
            yield return f;
        }
    }

    public static double Number(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    public static bool TryNumber(string s, out double value) =>
        double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    public static DateTime? Date(string s) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d) ? d : (DateTime?)null;

    /// <summary>A date inside a list row's years: from <paramref name="since"/> on, before <paramref name="until"/>; null is open.</summary>
    public static bool InRange(DateTime date, DateTime? since, DateTime? until) =>
        (since == null || date >= since.Value) && (until == null || date < until.Value);
}
