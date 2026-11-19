using System.Collections.Generic;

namespace SeaPowerBorders.Borders;

/// <summary>
/// The border lines of one date: all of <c>borders.bin</c> and the set drawn. A new selection is a new object, so the
/// maps see a change by reference.
/// </summary>
internal sealed class BorderSelection
{
    public readonly BorderData Data;
    public readonly HashSet<BorderLine> Drawn;

    public BorderSelection(BorderData data, HashSet<BorderLine> drawn)
    {
        Data = data;
        Drawn = drawn;
    }
}
