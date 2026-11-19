using System.Collections.Generic;
using MapControl;
using Noesis;
using SeaPowerBorders.Borders;

namespace SeaPowerBorders.Map;

/// <summary>
/// The lines on a flat (equirectangular) map: one Noesis <see cref="Path"/> per tile and line style, which the map
/// places by the tile's bounding box on every pan and zoom (and wraps past +/-180). The geometry is in degrees from
/// the tile's north-west corner, scaled to the zoom by a transform on the geometry rather than on the path, so the
/// stroke keeps its width on screen. The scale follows the map's <c>ViewportChanged</c> event (before the map lays out
/// its children, so lines never lag a frame behind); the level of detail follows the zoom.
/// <para>The coastal distance line goes the same way, one small path per <see cref="SeaLimits.CellDegrees"/> cell as
/// the tiles come in, only around the view.</para>
/// </summary>
internal sealed class FlatDrawing : IBorderDrawing
{
    private readonly MapBase map;
    private readonly MapPanel panel, waterPanel;
    private readonly BorderLook look;
    private readonly BorderSelection lines;
    private readonly SolidColorBrush brush, coastalBrush;
    private readonly List<ScaleTransform> scales = new List<ScaleTransform>();
    private readonly CoastalTiles<List<ScaleTransform>> coast;
    private readonly List<Point> buffer = new List<Point>();
    private int level = -1;
    private float scale = float.NaN;

    public FlatDrawing(MapBase map, MapPanel panel, MapPanel waterPanel, BorderLook look, BorderSelection lines)
    {
        this.map = map;
        this.panel = panel;
        this.waterPanel = waterPanel;
        this.look = look;
        this.lines = lines;
        // The opacity is in the brush, as on the globe, so the panel stays fully opaque.
        panel.Opacity = 1f;
        brush = look.LineBrush();
        coastalBrush = look.CoastalBrush();
        coast = new CoastalTiles<List<ScaleTransform>>(() => waterPanel.Children.Clear());
    }

    private float MapScale => (float)(map.ViewTransform.Scale * Earth.MetresPerDegree);

    public void Update(SeaLimitCache waters)
    {
        ViewportChanged();
        double pixelsPerDegree = map.ViewTransform.Scale * Earth.MetresPerDegree;
        if (pixelsPerDegree <= 0)
            return;
        int wanted = lines.Data.LevelFor(pixelsPerDegree);
        if (wanted != level)
        {
            Build(wanted);
            level = wanted;
        }
        coast.Fetch(waters, look.CoastalNm, pixelsPerDegree, PaddedView(), allLongitudes: false, AddCoastalPaths);
    }

    public void ViewportChanged()
    {
        if (scales.Count == 0 && !coast.Any)
            return;
        float s = MapScale;
        if (s == scale || s <= 0)
            return;
        scale = s;
        foreach (ScaleTransform t in scales)
            SetScale(t, s);
        foreach (List<ScaleTransform> transforms in coast.Drawn)
        {
            if (transforms == null)
                continue;
            foreach (ScaleTransform t in transforms)
                SetScale(t, s);
        }
    }

    public void Clear()
    {
        panel.Children.Clear();
        waterPanel.Children.Clear();
    }

    private static void SetScale(ScaleTransform t, float s)
    {
        t.ScaleX = s;
        t.ScaleY = s;
    }

    private void Build(int detail)
    {
        panel.Children.Clear();
        scales.Clear();
        scale = MapScale;
        var tiles = new Dictionary<(int x, int y, bool dashed), List<GeoPiece>>();
        foreach (BorderPiece piece in lines.Data.Pieces)
        {
            if (piece.Level != detail || !lines.Drawn.Contains(piece.Line) || (piece.Line.Dashed && !look.Disputed))
                continue;
            var key = (piece.TileX, piece.TileY, piece.Line.Dashed);
            if (!tiles.TryGetValue(key, out List<GeoPiece> list))
                tiles[key] = list = new List<GeoPiece>();
            list.Add(piece);
        }
        int points = 0;
        foreach (KeyValuePair<(int x, int y, bool dashed), List<GeoPiece>> tile in tiles)
        {
            Path path = NewPath(tile.Value, tile.Key.x * Tiles.Size - 180f, tile.Key.y * Tiles.Size - 90f, Tiles.Size, scales);
            path.Stroke = brush;
            path.StrokeThickness = look.Width;
            if (tile.Key.dashed)
                path.StrokeDashArray = BorderLook.DashedPattern;
            panel.Children.Add(path);
            foreach (GeoPiece piece in tile.Value)
                points += piece.Points.Length / 2;
        }
        ModLog.Debug($"Borders: level {detail} ({lines.Data.Tolerances[detail]}°), {tiles.Count} tiles, {points} points");
    }

    // The view and a quarter of it around, so a short pan finds its tiles drawn.
    private (double west, double south, double east, double north)? PaddedView()
    {
        if (map.ActualWidth <= 0 || map.ActualHeight <= 0)
            return null;
        BoundingBox view = map.ViewRectToBoundingBox(new Rect(0, 0, map.ActualWidth, map.ActualHeight));
        if (view == null)
            return null;
        double padX = (view.East - view.West) / 4, padY = (view.North - view.South) / 4;
        return (view.West - padX, view.South - padY, view.East + padX, view.North + padY);
    }

    // One small path per cell of the tile, each placed by its own bounding box. Null when the tile has no line at
    // this level (it is still remembered as done).
    private List<ScaleTransform> AddCoastalPaths(SeaLimitTile tile)
    {
        List<SeaLimitPiece> pieces = tile.Levels[coast.Level];
        if (pieces.Count == 0)
            return null;
        ViewportChanged();
        if (float.IsNaN(scale))
            scale = MapScale;
        const float cell = (float)SeaLimits.CellDegrees;
        var cells = new Dictionary<(int x, int y), List<GeoPiece>>();
        foreach (SeaLimitPiece piece in pieces)
        {
            if (!cells.TryGetValue((piece.Cx, piece.Cy), out List<GeoPiece> list))
                cells[(piece.Cx, piece.Cy)] = list = new List<GeoPiece>();
            list.Add(piece);
        }
        var transforms = new List<ScaleTransform>();
        foreach (KeyValuePair<(int x, int y), List<GeoPiece>> kv in cells)
        {
            Path path = NewPath(kv.Value, kv.Key.x * cell - 180f, kv.Key.y * cell - 90f, cell, transforms);
            path.Stroke = coastalBrush;
            path.StrokeThickness = look.CoastalWidth;
            path.StrokeDashArray = BorderLook.CoastalPattern;
            waterPanel.Children.Add(path);
        }
        return transforms;
    }

    // A path of the pieces in one square of the map (west, south, size in degrees), placed by its bounding box; its
    // scale transform goes into the list.
    private Path NewPath(List<GeoPiece> pieces, float west, float south, float size, List<ScaleTransform> transforms)
    {
        float north = south + size;
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            foreach (GeoPiece piece in pieces)
            {
                float[] p = piece.Points;
                context.BeginFigure(new Point(p[0] - west, north - p[1]), false, false);
                buffer.Clear();
                for (int k = 2; k < p.Length; k += 2)
                    buffer.Add(new Point(p[k] - west, north - p[k + 1]));
                context.PolyLineTo(buffer, true, true);
            }
        }
        var transform = new ScaleTransform(scale, scale);
        geometry.Transform = transform;
        transforms.Add(transform);
        var path = new Path { Data = geometry, StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false };
        MapPanel.SetBoundingBox(path, new BoundingBox(south, west, north, west + size));
        return path;
    }
}
