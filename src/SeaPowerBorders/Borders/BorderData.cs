using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SeaPowerBorders.Borders;

/// <summary>How Natural Earth classes a border line; <see cref="Added"/> for the Cold War lines the mod adds.</summary>
internal enum BorderClass : byte
{
    International, Indefinite, Disputed, LineOfControl, Lease, Overlay, Indeterminant, Unrecognized, Added,
}

/// <summary>One border: the countries on its two sides (Natural Earth's ADM0_A3 codes) and its class.</summary>
internal sealed class BorderLine
{
    public BorderClass Class;
    public string Left, Right, Name;

    /// <summary>The years of an added line (the inner-German border, West Berlin, ...); null for today's borders.</summary>
    public DateTime? Since, Until;

    /// <summary>Disputed borders, lines of control, lease limits and the like are drawn dashed.</summary>
    public bool Dashed => Class != BorderClass.International && Class != BorderClass.Indefinite && Class != BorderClass.Added;
}

/// <summary>A border line's stretch inside one 10° tile, at one level of detail.</summary>
internal sealed class BorderPiece
{
    public BorderLine Line;
    public int Level, TileX, TileY;

    /// <summary>Longitude, latitude, longitude, latitude, ...</summary>
    public float[] Points;
}

/// <summary>
/// The border lines of <c>borders/borders.bin</c> (made by <c>tools/borders-data/build.py</c> from Natural Earth):
/// every line simplified at a few tolerances and cut into 10° tiles. Plain C#, so the offline check reads it too.
/// </summary>
internal sealed class BorderData
{
    public const float Tile = 10f;
    private const int Version = 1;

    public float[] Tolerances;
    public readonly List<BorderLine> Lines = new List<BorderLine>();
    public readonly List<BorderPiece> Pieces = new List<BorderPiece>();

    public static BorderData Load(string path)
    {
        var data = new BorderData();
        using var r = new BinaryReader(File.OpenRead(path));
        if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "SPBD" || r.ReadInt32() != Version)
            throw new InvalidDataException($"{Path.GetFileName(path)} is not a borders file of this mod version");
        data.Tolerances = new float[r.ReadInt32()];
        for (int i = 0; i < data.Tolerances.Length; i++)
            data.Tolerances[i] = r.ReadSingle();
        int lines = r.ReadInt32();
        for (int i = 0; i < lines; i++)
        {
            var line = new BorderLine
            {
                Class = (BorderClass)r.ReadByte(),
                Left = Code(r.ReadBytes(3)),
                Right = Code(r.ReadBytes(3)),
                Since = Date(r.ReadInt32()),
                Until = Date(r.ReadInt32()),
            };
            line.Name = Encoding.UTF8.GetString(r.ReadBytes(r.ReadByte()));
            data.Lines.Add(line);
        }
        int pieces = r.ReadInt32();
        for (int i = 0; i < pieces; i++)
        {
            var piece = new BorderPiece { Line = data.Lines[r.ReadInt32()], Level = r.ReadByte(), TileX = r.ReadByte(), TileY = r.ReadByte() };
            piece.Points = new float[2 * r.ReadInt32()];
            for (int k = 0; k < piece.Points.Length; k++)
                piece.Points[k] = r.ReadSingle();
            data.Pieces.Add(piece);
        }
        return data;
    }

    /// <summary>The coarsest level whose simplification stays within half a pixel at this zoom.</summary>
    public int LevelFor(double pixelsPerDegree)
    {
        for (int level = Tolerances.Length - 1; level > 0; level--)
        {
            if (Tolerances[level] * pixelsPerDegree <= 0.5)
                return level;
        }
        return 0;
    }

    private static string Code(byte[] b) => Encoding.ASCII.GetString(b).Trim();

    private static DateTime? Date(int ymd) =>
        ymd == 0 ? (DateTime?)null : DateTime.ParseExact(ymd.ToString(CultureInfo.InvariantCulture), "yyyyMMdd", CultureInfo.InvariantCulture);
}
