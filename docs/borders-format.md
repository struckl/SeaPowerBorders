# borders.bin

The border lines the mod ships (`src/SeaPowerBorders/mod/borders/borders.bin`).

- Written by `tools/borders-data/build.py` from Natural Earth and `tools/borders-data/extra.csv`
- Read by `src/SeaPowerBorders/Borders/BorderData.cs` (the mod and both offline checks)

## One source for the numbers

| Value | Defined in | build.py |
|---|---|---|
| File version | `BorderData.Version` | reads it from `BorderData.cs` |
| Tile size (10 degrees) | `Tiles.Size` in `Borders/GeoPiece.cs` | reads it from `GeoPiece.cs` |
| Class codes | the order of `enum BorderClass` in `BorderData.cs` | reads the names from there and maps Natural Earth's `FEATURECLA` onto them |
| Tolerances per level | `LEVELS` in `build.py` | written into the file, the mod reads them from there |

- A class added to the enum needs a line in `build.py`'s `FEATURE_CLASSES` (it stops with an error otherwise).
- A change of layout bumps `BorderData.Version`; build.py writes the new number and the mod refuses an old file.

## Layout

Little-endian throughout.

| Part | Fields |
|---|---|
| Header | `"SPBD"` (4 bytes ASCII), int32 version |
| Levels | int32 level count, then float32 Douglas-Peucker tolerance in degrees per level (finest first) |
| Lines | int32 line count, then per line: byte class, 3 bytes country left, 3 bytes country right (ASCII, space-padded), int32 since, int32 until (yyyymmdd, 0 = open), byte name length, name (UTF-8) |
| Pieces | int32 piece count, then per piece: int32 line index, byte level, byte tile x, byte tile y, int32 point count, float32 longitude/latitude pairs |

- Countries are Natural Earth's ADM0_A3 codes; added lines use their own (`DDR`, `BRD`, `WBE`, `YMD`, `YAR`, `VNM`).
- Tile x counts from 180 W, tile y from 90 S, both in tile-size steps.
- A piece lies in one tile and one level; neighbouring pieces share the point where the line crosses the tile edge.

## Simplification in two languages

- `build.py` `simplify` and `SeaLimits.Simplify` are both Douglas-Peucker with the ends kept.
- Both are needed: the borders are simplified offline (Python, Natural Earth), the coastal distance line in the game
  (C#, from the game's own world grid on the player's PC).
- `build.py` `cut` (tiles) and `SeaLimits.CutAtCells` (2.5 degree cells) do the same job for their line sets.
