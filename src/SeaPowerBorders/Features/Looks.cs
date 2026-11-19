using SeaPowerBorders.Map;

namespace SeaPowerBorders.Features;

/// <summary>
/// The looks of the lines and labels for a light and a dark map, from the settings; made again when a setting
/// changes. The drawing classes get them handed in and read no settings themselves.
/// </summary>
internal static class Looks
{
    private static readonly BorderLook[] borders = new BorderLook[2];
    private static readonly LabelLook[] labels = new LabelLook[2];
    private static int settings = -1;

    public static BorderLook Borders(bool dark)
    {
        Refresh();
        int i = dark ? 1 : 0;
        return borders[i] ??= new BorderLook(dark, Settings.DisputedLines.Value, Settings.LineWidth.Value,
                                             Settings.LineOpacity.Value, Settings.CoastalLine.Value);
    }

    public static LabelLook Labels(bool dark)
    {
        Refresh();
        int i = dark ? 1 : 0;
        return labels[i] ??= new LabelLook(dark, Settings.NavalBases.Value, Settings.AirBases.Value, Settings.Airports.Value,
                                           Settings.LabelSize.Value, Settings.Density.Value);
    }

    private static void Refresh()
    {
        if (settings == Settings.Changes.Version)
            return;
        settings = Settings.Changes.Version;
        borders[0] = borders[1] = null;
        labels[0] = labels[1] = null;
    }
}
