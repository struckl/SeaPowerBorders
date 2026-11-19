using System;
using SeaPowerBorders.Places;

namespace SeaPowerBorders.Map;

/// <summary>
/// How the labels look on one map and which kinds of place get one: the Cities settings and the map's theme. A
/// different look makes the labels again. Plain C#: the offline check picks with the mod's defaults.
/// </summary>
internal sealed class LabelLook : IEquatable<LabelLook>
{
    public readonly bool Dark, NavalBases, AirBases, Airports;
    public readonly float Size;
    public readonly Density Density;

    public LabelLook(bool dark, bool navalBases, bool airBases, bool airports, float size, Density density)
    {
        Dark = dark;
        NavalBases = navalBases;
        AirBases = airBases;
        Airports = airports;
        Size = size;
        Density = density;
    }

    /// <summary>The look the mod starts with (its config defaults), on a light map.</summary>
    public static LabelLook Defaults => new LabelLook(false, true, true, false, 1f, Density.Normal);

    /// <summary>The settings let this kind of place have a label.</summary>
    public bool Shows(Place p) => p.Kind switch
    {
        PlaceKind.AirBase => AirBases,
        PlaceKind.Airport => Airports,
        PlaceKind.NavalBase => NavalBases,
        _ => true,
    };

    /// <summary>The place gets the anchor in its side's colour.</summary>
    public bool Anchor(Place p) => p.Naval && NavalBases;

    public bool Equals(LabelLook other) =>
        other != null && Dark == other.Dark && NavalBases == other.NavalBases && AirBases == other.AirBases &&
        Airports == other.Airports && Size == other.Size && Density == other.Density;

    public override bool Equals(object obj) => Equals(obj as LabelLook);

    public override int GetHashCode() => HashCode.Combine(Dark, NavalBases, AirBases, Airports, Size, Density);
}
