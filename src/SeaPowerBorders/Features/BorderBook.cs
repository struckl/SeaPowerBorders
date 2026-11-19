using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeaPowerBorders.Borders;

namespace SeaPowerBorders.Features;

/// <summary>
/// The border lines of a date (or today's, by the Era setting), from <c>borders/borders.bin</c> and
/// <c>borders/eras.csv</c>, read once per game session. A file that can't be read means no borders this session.
/// </summary>
internal sealed class BorderBook
{
    private BorderData data;
    private BorderEras eras;
    private bool missing;

    /// <summary>The lines last selected; null before the first selection or without the files.</summary>
    public BorderSelection Selected { get; private set; }

    public void Select(DateTime date, BorderEra era)
    {
        if (missing)
            return;
        try
        {
            if (data == null)
            {
                string dir = ModFolder.Sub("borders");
                data = BorderData.Load(Path.Combine(dir, "borders.bin"));
                eras = BorderEras.Load(dir);
            }
            var drawn = new HashSet<BorderLine>(data.Lines.Where(l => eras.Drawn(l, date, era)));
            Selected = new BorderSelection(data, drawn);
            List<string> added = drawn.Where(l => l.Class == BorderClass.Added).Select(l => l.Name).Distinct().ToList();
            ModLog.Info($"{drawn.Count} of {data.Lines.Count} border lines for " +
                        $"{(era == BorderEra.Today ? "today" : date.ToString("yyyy-MM-dd"))}" +
                        $"{(added.Count > 0 ? " with " + string.Join(", ", added) : "")}.");
        }
        catch (Exception ex)
        {
            missing = true;
            ModLog.Error($"Could not load the border lines - no borders this session: {ex}");
        }
    }

    public void Forget() => Selected = null;
}
