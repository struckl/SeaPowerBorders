using System;
using System.Collections.Generic;
using SeaPowerBorders.Borders;

namespace SeaPowerBorders.Map;

/// <summary>
/// The globe's line work without the drawing: which border pieces a level shows, by line style, and projecting the
/// pieces that meet the view into sinks under a point budget. Plain C#: the offline globe check runs exactly this.
/// </summary>
internal sealed class GlobeLines
{
    /// <summary>Per rebuild and line style; a safety net, normal views use a fraction.</summary>
    public const int MaxPoints = 250000;

    public readonly List<BorderPiece> Solid = new List<BorderPiece>(), Dashed = new List<BorderPiece>();

    /// <summary>The pieces of this level and date, by line style; dashed ones only with <paramref name="disputed"/>.</summary>
    public void Select(BorderData data, HashSet<BorderLine> drawn, int level, bool disputed)
    {
        Solid.Clear();
        Dashed.Clear();
        foreach (BorderPiece piece in data.Pieces)
        {
            if (piece.Level != level || !drawn.Contains(piece.Line))
                continue;
            if (!piece.Line.Dashed)
                Solid.Add(piece);
            else if (disputed)
                Dashed.Add(piece);
        }
    }

    /// <summary>
    /// Projects the pieces that meet the area into the sink; stops (and calls <paramref name="overBudget"/>) once the
    /// sink holds more than <see cref="MaxPoints"/>.
    /// </summary>
    public static void Fill(IEnumerable<GeoPiece> pieces, in GeoArea area, Projector project, double step, ILineSink sink, Action overBudget)
    {
        foreach (GeoPiece piece in pieces)
        {
            if (!area.Meets(piece.Bounds))
                continue;
            ProjectedLines.Add(piece.Points, project, step, sink);
            if (sink.Points > MaxPoints)
            {
                overBudget();
                return;
            }
        }
    }
}
