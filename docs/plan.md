# Build plan

Goal: a **Borders** entry in the map's Layers menu that draws national land borders **as they were on the mission's
date** (the Soviet Union, East and West Germany, Yugoslavia, …), a line marking **territorial waters**, and
optionally the country names of the time. Written 2026-10-24 before anything was built; check the game version
before starting (tested pieces below are from Sea Power 0.8.7).

## What the game gives us

- **No border data.** `StreamingAssets/original/nations_reference.ini` and `language_*/nations.ini` only list the
  unit factions (US, Soviet, UK, Iran, …). The stock map draws no borders.
- **The mission date.** Every mission ini has `[Environment] Date=YYYY,M,D` (`Date=1985,6,26`); at runtime
  `Singleton<SeaPower.Environment>.Instance.Year / Month / Day` (written back by `SaveLoadManager`). The stock
  missions are mostly 1976–1991, most of them 1985–1988, a few later.
- **The map.** `SeaPower.LiveMap.MapScene` is a Unity scene of lon/lat quads on layer 19, rendered by the map camera
  into the texture the map window shows. Anything we add to that scene on layer 19 pans, zooms and wraps with the
  map. Sea Power Topographic Map does exactly this – reuse its pieces:
  - `Render/OverlayRenderer.cs` (`MapSceneRef`): finding the scene and the view (`Viewport`, degrees per pixel).
  - `TopoLayer.cs`: the entry in the map's Layers menu (Topography is #600; Borders takes **#601**; Better Tac Map
    uses 500–509).
  - `Patches.cs`: mission start / end hooks, and why Noesis classes may only be patched once NoesisGUI runs.
- **The coastline** is the game's DEM (30″ chunks via `TerrainLoader.TryGetCached`, the 3′ world grid behind the
  zoomed-out map) – see Topographic Map's `Terrain/HeightReader.cs`.

## Border data

**Natural Earth 1:10m "Admin 0 – Boundary Lines", land** (`ne_10m_admin_0_boundary_lines_land`): land borders only,
no coastlines – exactly what is needed. Public domain (naturalearthdata.com/about). Each line carries the countries
on both sides (`ADM0_A3_L`, `ADM0_A3_R`) and a class (`FEATURECLA`: international, disputed, line of control,
indefinite …). It shows **today's** de facto borders, so the era rules below take out or add lines by date.

Not used: CShapes 2.0 (every border by date 1886–2019, but non-commercial licence, and the sources disagree whether
it is CC BY-NC or BY-NC-SA); aourednik/historical-basemaps (GPL, work in progress); Marine Regions 12 nm / EEZ
(CC BY 4.0, but today's claims – the US had 3 nm until December 1988).

### Era rules (`mod/borders/eras.txt`)

A line between two of these countries is **not** drawn before the date; a hand-drawn line is drawn **only** before it.

| Until | Rule |
| --- | --- |
| 1991-09-06 | Estonia, Latvia, Lithuania part of the USSR: no lines between them and Russia / Belarus |
| 1991-12-26 | USSR: no lines between Russia, Ukraine, Belarus, Moldova, Georgia, Armenia, Azerbaijan, Kazakhstan, Uzbekistan, Turkmenistan, Kyrgyzstan, Tajikistan |
| 1990-10-03 | Germany divided: **add** the inner-German border – in Natural Earth admin-1 as the boundary between the old and the new Länder (Schleswig-Holstein / Lower Saxony / Hesse / Bavaria against Mecklenburg-Vorpommern / Saxony-Anhalt / Thuringia / Saxony) – and West Berlin's boundary (hand-drawn) |
| 1990-05-22 | Yemen divided: **add** the North–South Yemen line (hand-drawn, approximate) |
| 1991-06-25 / 1991-09-08 / 1992-03-03 | Yugoslavia: no lines between Slovenia, Croatia (both until 1991-06-25), Macedonia (1991-09-08), Bosnia (1992-03-03) and the rest |
| 2006-06-03 | Serbia and Montenegro one country |
| 2008-02-17 | Kosovo part of Serbia (Natural Earth code to check: `KOS` / `-99`) |
| 1993-01-01 | Czechoslovakia: no Czech–Slovak line |
| 1993-05-24 | Eritrea part of Ethiopia |
| 2011-07-09 | South Sudan part of Sudan |
| 1976-07-17 … 2002-05-20 | East Timor part of Indonesia |
| 1997-07-01 / 1999-12-20 | Hong Kong (British) and Macau (Portuguese): **add** their boundary with China (Natural Earth "map unit" lines) |
| 1990-03-21 | Namibia under South African rule – keep the line, name it "South West Africa" |

Check the codes against the data before trusting the table. Small things (Amt Neuhaus 1993, Walvis Bay 1994, the
Panama Canal Zone) are left out.

## Territorial waters

No outside data: a thin dashed line **12 nm** (setting: 3 / 12 nm) from the game's **own** coastline, so it fits
the coast the player sees. At mission start, for the area around all units (same rule as Topographic Map's
pre-drawn mission area): land mask from the DEM → exact Euclidean distance transform (Felzenszwalb–Huttenlocher, a
30″ cell is ½ nm north–south) → marching squares at 12 nm → polylines, simplified. A 20° × 10° area is about
2400 × 1200 cells, well under 100 ms on a worker thread. Not computed from the game files offline and shipped:
that would distribute data derived from the game's terrain.

No EEZ / median lines between neighbours – they need claims data of the time.

## Data tool (`tools/borders-data`)

A small .NET console app run once by the developer:

1. Read Natural Earth's GeoJSON (from the `nvkelso/natural-earth-vector` repository at a pinned release tag –
   download it by hand into `tools/borders-data/input/`, git-ignored) for land boundary lines, admin-1 lines
   (inner-German border), map unit lines (Hong Kong, Macau) and the country label points.
2. Tag every line with its two countries and class; apply nothing yet – the era rules run in the game.
3. Simplify (Douglas–Peucker) at three tolerances for zoom levels.
4. Write `src/SeaPowerBorders/mod/borders/borders.bin` (little-endian: line count; per line: flags, two country
   codes, point count per level, float lon/lat pairs) and `labels.txt`, plus the hand-drawn lines (Yemen, West
   Berlin) from `tools/borders-data/extra.geojson`.

Expected size well under 1 MB. Credit Natural Earth in the README and on the Workshop page even though it is public
domain.

## Drawing

- **Vector lines, not part of a raster:** one mesh of thin quads per layer in the map scene (own GameObject on
  layer 19, z just in front of Topographic Map's overlay, which uses −0.03 … −0.05), `Sprites/Default` material.
- **Constant width on screen:** rebuild the quad strips when the zoom changes (after a short settle, like
  Topographic Map's redraw) with a half-width of ~0.8 px × degrees per pixel; only lines touching the view plus a
  margin; the simplification level by zoom. MeshTopology.Lines would be simpler but is 1 px – too thin on 4K.
- **Style:** land borders solid (dark grey by day, light grey on the night theme), disputed / lines of control
  dashed, territorial waters thin dashed blue-grey. Unit symbols must stay on top (check the draw order).
- **Labels (optional, last):** country names of the time (USSR, GDR, FRG, Yugoslavia, Czechoslovakia, North and South
  Yemen, …) at Natural Earth's label points, fading in by zoom. Either Noesis text laid over the map control (like
  Topographic Map's legend) or a TextMesh in the scene.

## Settings (section "2. Borders")

Visible (written by the Layers-menu entry), toggle key, era (`MissionDate` / `Today`), territorial waters (`Off`,
`3 nm`, `12 nm`), labels on/off, line width, opacity.

## Order of work

1. Data tool, `borders.bin`, `eras.txt`, `extra.geojson` (Yemen, West Berlin).
2. Layers-menu entry and mission hooks (copy from Topographic Map), load the data, log the line counts.
3. Line mesh renderer with constant width and zoom levels – first test: today's borders everywhere.
4. Era rules from the mission date (log which rules applied).
5. Territorial waters.
6. Labels.
7. README, CHANGELOG, `docs/testing.md`, Workshop page and packaging (copy `workshop/`, `tools/package.ps1` and
   `docs/workshop.md` from Topographic Map).

## Test checklist (draft)

- A 1985 mission in the Baltic or Norwegian Sea: no borders between the Baltic states and Russia; the inner-German
  border is drawn; Poland–GDR and GDR–FRG lines meet the coast where the stock coastline is.
- A 1991-06 mission (there is one, `Date=1991,6,3`): USSR still whole, Germany already one.
- Zoom from the whole world down to a fjord: lines stay the same width, no gaps where line pieces meet.
- Territorial waters line follows the coast and the islands at 12 nm (measure with the map's ruler).
- With Topographic Map on as well: borders on top of the colours, both layers toggle independently.
