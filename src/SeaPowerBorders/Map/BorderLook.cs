using System;
using Noesis;

namespace SeaPowerBorders.Map;

/// <summary>
/// How the lines look on one map: the settings that shape them and the map's theme. Both drawings (flat tiles and the
/// globe's projected paths) take their colours, widths and dash patterns from here; a different look makes them
/// draw again.
/// </summary>
internal sealed class BorderLook : IEquatable<BorderLook>
{
    public const string DashedPattern = "6 3";
    public const string CoastalPattern = "3 3";
    private const float CoastalWidthShare = 0.8f;

    public readonly bool Dark, Disputed;
    public readonly float Width, Opacity;

    /// <summary>The coastal distance line in nm, 0 for none.</summary>
    public readonly int CoastalNm;

    public BorderLook(bool dark, bool disputed, float width, float opacity, int coastalNm)
    {
        Dark = dark;
        Disputed = disputed;
        Width = width;
        Opacity = opacity;
        CoastalNm = coastalNm;
    }

    public float CoastalWidth => Width * CoastalWidthShare;

    /// <summary>
    /// The border colour, fully opaque (the flat drawing fades its whole panel) or with the opacity in the brush (the
    /// globe). Dashed lines take the same colour: a lighter tint got lost on the terrain (the Korean line by day).
    /// </summary>
    public SolidColorBrush LineBrush(bool withOpacity)
    {
        byte alpha = withOpacity ? Alpha : (byte)255;
        return new SolidColorBrush(Dark ? Color.FromArgb(alpha, 205, 200, 215) : Color.FromArgb(alpha, 58, 50, 72));
    }

    /// <summary>
    /// The coastal line has its opacity in the brush on every map: a panel opacity would have Noesis draw all its
    /// paths into an offscreen picture first.
    /// </summary>
    public SolidColorBrush CoastalBrush() =>
        new SolidColorBrush(Dark ? Color.FromArgb(Alpha, 140, 180, 215) : Color.FromArgb(Alpha, 45, 90, 140));

    private byte Alpha => (byte)Math.Round(255.0 * Opacity);

    public bool Equals(BorderLook other) =>
        other != null && Dark == other.Dark && Disputed == other.Disputed && Width == other.Width && Opacity == other.Opacity &&
        CoastalNm == other.CoastalNm;

    public override bool Equals(object obj) => Equals(obj as BorderLook);

    public override int GetHashCode() => HashCode.Combine(Dark, Disputed, Width, Opacity, CoastalNm);
}
