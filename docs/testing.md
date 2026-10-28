# In-game test checklist

Log: `E:\Games\steamapps\common\Sea Power\BepInEx\LogOutput.log`. Config:
`BepInEx\config\dev.sewerlabs.seapowerborders.cfg` – turn on `Verbose log` for this test (every label pick and
border level change is logged).

## Globe build (needs the game build 26223125)

Offline first (no game): `dotnet run --project tools/globe-check -c Release -- --check` - the mod's projection code
against the game's own `OrthographicProjection` (positions within 0.0002 px of the textbook formula, chords within
0.4 px of the curve, cut ends on the limb, 0.1-3 ms per view). Pictures: `-- --view 70.3,20,60 --out n.tsv`, then
`python -I tools/globe-check/draw.py n.tsv n.png`. Passed 2026-10-28.

### Result 2026-10-28 (Claude, on his go; mouse-free, with the Shot Director dev tool, Anchor Chain through Topo's dev shim, Trollfjorden 1985)

Globe build 26223125, package with both DLLs (the flat DLL logged `This game has the globe map … stays off`, no duplicate-plugin
warning, no `[Error  :SeaPowerBorders]` in the end):
- Layers entries "Borders" (#601) and "Cities and bases" (#602) added; 1619 places and 446 of 511 border lines for 1985-02-26.
- Labels on the globe: right places and symbols (Kirkenes, Murmansk, Severomorsk, East Berlin, Moscow ...), none for the far side.
- Border lines, night and day palette: Norwegian-Soviet and Finnish-Soviet frontier at 70° N (zoom 7), 1985 Europe and the Middle East
  incl. the inner-German line (zoom 5), Africa/Asia with the globe turned (zoom 3.5): on the stock map's features, cut at the limb.
- **Two bugs found only in the game and fixed (558901b):** a delegate that was never assigned after the file split
  (`NullReferenceException` in the first rebuild), and one path with thousands of points that Noesis does not draw at all
  (the borders vanished at zoom 5 although 11,689 points were produced) - now chunked paths of at most 600 points.
- **Coastal distance line** (`Coastal distance line (nm) = 12`, set through the config while running): the tiles are read in the
  background (50-60 ms each) and the dashed line follows the Finnmark coast at zoom 8.5 with the frontier alongside.
- **Not tested yet:** the Layers menu entry clicked (log only), Shift+F7, the editor's flat map, 0.8.7 with the flat DLL (needs the
  Steam branch switched), a mission over the date line or the poles, dragging/zooming by hand for stutter (only jumps by command).

Offline too: `python tools/modtool.py compat borders` (workspace) builds both DLLs and checks them against both games'
assemblies - passed 2026-10-28; `docs/both-versions.md` in the workspace has the whole scheme.

In the game (Claude never starts it for this; turn on `Verbose log`, mouse and keys are his). The package folder holds
**both DLLs**; on the globe build Anchor Chain needs Topo's dev shim (workspace `docs/both-versions.md`):

0. **Which build runs.** Globe game: `SeaPowerBordersFlat.dll` logs `This game has the globe map: the globe build runs, the
   flat-map build stays off.` and `Sea Power Cities and Borders 1.0.0 loaded.` comes from the globe DLL. 0.8.7: the globe DLL
   logs `This game has the flat map: the flat-map build runs, the globe build stays off.` and `Sea Power Cities and Borders
   (flat map) 1.0.0 loaded.` appears. No duplicate-plugin warning from Anchor Chain.
1. Log: `Map hook in place.`, then on the first mission `Attached to the map: border lines place labels.` and `Border
   lines: drawing for the globe.` No `[Error  :SeaPowerBorders]` and no `MissingMethodException`.
2. **Layers menu**: entries "Borders" and "Cities and bases" exist and switch the lines and labels (Shift+F7 / F7 too).
3. **Borders follow the stock map**: Trollfjorden (1985) zoomed on the Norwegian-Soviet-Finnish border at 70° N, Europe
   at a 20-30° view with the globe turned and the map rotated; the lines must sit on the stock coastlines/borders at every
   zoom, also while dragging (no lag, no flicker) and across the date line (a Pacific mission).
4. **Whole globe in view**: no lines on the far side, none across the disc.
5. **Labels**: none for places on the far side; after turning the globe by a tenth of the view the new side gets its labels.
6. **Coastal distance line** (`Coastal distance line (nm) = 12`): follows the drawn coast on the globe, no tiles missing
   east of 20°E (the 0.3.0 bug), off again with the setting at 0.
7. **Night map**: lines and names change colour with each map's palette (Auto follows the sun).
8. The editor's flat map still draws the borders (old drawing).
9. Speed: dragging and zooming stay smooth with everything on (the log line `Border lines (globe): N points` shows the size).

## Sea Power 0.8.7 (flat map) with the two-build package

The flat build is the code of 0.3.1 plus the entry check, so the results below still stand; to confirm nothing changed on
the old game, with the package from `tools\package.ps1 -Install` (both DLLs): step 0 above (flat build runs, globe build
off), then the 0.3.1 checklist below (the coastal line off by default, Layers entries, Shift+F7, a 1985 and a 1991 mission,
borders and labels as before) and no `[Error  :SeaPowerBorders]`.

## 0.3.1 – Coastal distance line (reworked)

The line is **off by default now**: set `Coastal distance line (nm) = 12` first.

1. **Trollfjorden (1985), map zoomed in on the Norwegian coast** (about 6° view or closer). Log: `Sea mask read in …
   ms.` once, then with verbose log `Territorial waters 12 nm, tile …` for the tiles around the view and, every
   four seconds, a `Coastal line: N cell paths …` line. A dashed blue line appears off the coast within a second or so.
2. **The bug that led to the rework – check it first:** `center 70.3 19.8` at zoom 8.6 (view about 5.6° tall), and
   `center 70.5 24` at zoom 8.6: the line must continue east of 20°E along Sørøya and Magerøya and must not stop at
   the 20°E meridian (in 0.3.0 it did, although the tile was computed). If a part is still missing, send the
   `Coastal line:` lines of the log: they say where each cell path ended up and whether Noesis counts it visible.
3. **Measure:** with the map's scale bar, coast to line ≈ 12 nm (east–west distances on the map are shorter at high
   latitude: the map is plate carrée). Measured in the game with 0.3.0: median 12.0 nm, middle half 11.3–12.5.
4. **Zoom out** until the line would crowd the coast: it goes; zoom in again: back, finer. Line width stays the same.
5. **Pan along the coast:** new tiles come in without stutter; no gaps where cells and tiles meet.
6. **The Baltic and the Black Sea** have lines (seas the 3′ grid may cut off); the Caspian has none.
7. Config 3, 6, 0 – applied at once; Borders layer off (Layers menu) – the line goes too; night theme: light blue.
8. **Opacity** is in the brush now: the line looks the same as before with `Opacity = 0.85`, and changing `Opacity`
   in the config changes it at once.

### Result 0.3.1, 2026-10-25 (in the game, Shot Director; mouse-free commands except one click on Play)

- **The missing-line bug is gone.** At 70.3°N 19.8°E and 70.5°N 24°E (zoom 8.6), the two positions that lost their
  line in 0.3.0, and at 70.0°N 20°E and 70.8°N 22°E, the dashed line runs unbroken along the whole coast, east of
  20°E included (checked with the stock map showing, the Topo colours faded out with `topo Opacity 0`). The verbose
  log's `Coastal line:` lines list every cell path as arranged (size 690x690 at the 8.6 zoom, visible True).
- Distance against the game's scale bar: median 12.2 nm, middle half 11.0–12.7 (0.3.0: 12.0, 11.3–12.5).
- Baltic, Black Sea and the Sea of Azov have lines, the Caspian none; the line is gone at a 60° view (zoom 5) and
  back at 32° (zoom 6); 3, 6, 0 and 12 nm apply at once; `Opacity = 0.4` makes it fainter at once; the night map
  shows it in light blue with white borders; no `[Error  :SeaPowerBorders]` line in the log.
- Not checked again (unchanged since 0.3.0, which passed): the Layers-menu entry and Shift+F7 taking the coastal
  line away with the borders – these need mouse or keyboard input on the game window.

### Result 0.3.0, 2026-10-24 (in the game, Shot Director)

Median 12.0 nm from the drawn coast (middle half 11.3–12.5); islands ringed; Black Sea, Azov and Marmara have lines,
the Caspian none; gone at a 60° view, back at 32°; 3 / 6 / 0 / 12 nm apply at once; Layers menu entry removes it with
the borders; night colours; no frame-rate change (about 135 FPS with and without). **Bug:** at some positions the
tiles east of 20°E were not drawn (70.3°N 19.8°E and 70.5°N 24°E at zoom 8.6; fine at zoom 8 and at 69.9°N) –
reworked in 0.3.1, to be checked in the game.

## 0.2.0 – Borders (first build)

Load missions from the **main menu** only, once it has settled (see the 0.1.0 result below).

1. **Start Sea Power.** Log: `Sea Power Borders 0.2.0 loaded.`, `Map hook in place.`
2. **Trollfjorden (1985).** Log: `Map layer 'Borders' added to the Layers menu (#601), on.`, `… of 511 border lines
   for 1985-02-26 with Inner-German border, North/South Yemen, West Berlin.`, `Attached to the map: border lines
   place labels.`
3. **Europe zoomed out:** the inner-German border and West Berlin's ring are drawn; no lines between Russia and the
   Baltic states, Belarus or Ukraine, none inside Yugoslavia or Czechoslovakia; the Kaliningrad–Poland line is there,
   Kaliningrad–Lithuania isn't. Lines below the city labels and the units; clicks and drags work as before.
4. **Zoom from the whole world down to a fjord and back:** the lines keep their width; no gaps where tiles meet
   (every 10°: 10°E, 20°E, 50°N, 60°N ...); the detail changes without a jump (log: `Borders: level …`); no stutter.
5. **Pan across ±180°** (Bering Strait): the Alaska–Canada line moves with the map and doesn't vanish.
6. **Layers menu → Borders** off and on, or Shift+F7 (F7 alone must still toggle only the cities).
7. **Night theme:** light lines. Cyprus, Korea, Kashmir: dashed lines.
8. **The 1991 mission (Strawberries Can Kill, 1991-06-03):** Germany one, Yemen one; the USSR and Yugoslavia still
   whole.
9. Config: `Era = Today` (all of today's borders, no inner-German border), `Line width = 2.5`, `Opacity = 0.5`,
   `Disputed lines = false` – applied at once.
10. With Topographic Map on as well: lines on top of its colours, both layers toggle independently.

### Result 2026-10-24 (Claude, with the Shot Director dev tool from SeaPowerTopo, plus held-Shift keys)

- **Trollfjorden (1985-02-26):** `446 of 511 border lines for 1985-02-26 with Inner-German border, North/South Yemen,
  West Berlin.` Steps 1–7, 9 and 10 as expected: the Cold War Europe (inner-German border, USSR, Yugoslavia and
  Czechoslovakia whole), the same stroke at zoom 5.4 / 7.3 / 9.5 / 12, levels switching 3 → 0 as you zoom in, no gaps
  where lines cross 20°E or 50°N, both sides of the date line (Alaska–Canada, Amur), F7 only the cities and Shift+F7
  only the borders, the Layers entry (#601, between Topography and Cities) toggles and writes `Visible`, night
  theme light lines, `Era = Today` / width / opacity / disputed lines applied at once. FPS the same with the layer on
  and off (the map is GPU-bound).
- **Strawberries Can Kill (1991-06-03):** `444 of 511 border lines`, no added lines; Germany one, USSR, Yugoslavia and
  Czechoslovakia whole.
- Fixed: the dashed lines (a lighter red tint) were hard to see on the terrain – the Korean line by day; now in the
  border colour with longer dashes.
- No `[Error  :SeaPowerBorders]` lines. The game's own `NullReferenceException` in `MapFormationViewModel.Dispose` when
  quitting from inside a mission is not the mod's.

## 0.1.0 – Cities and bases (first build)

1. **Start Sea Power.** Main menu → Mods: tick **Sea Power Borders**, restart if asked. Log:
   `Sea Power Borders 0.1.0 loaded.`, then once the main menu is up `Map hook in place.`
2. **Start a 1985 mission in the north** (Norwegian Sea / Kola). Log: `Map layer 'Cities and bases' added to the
   Layers menu (#602), on.`, `… places for 1985-…-… (… from the game's lists, … naval bases, … capitals).`,
   `Place labels attached to the map.`
3. **Open the map, zoomed out** over Europe: capitals with a ring (London, Paris, East Berlin, Bonn, Warsaw,
   Moscow …), big cities as dots, main naval bases with an anchor – blue for NATO (Faslane / HMNB Clyde, Brest,
   Kiel, Keflavík), red for the Warsaw Pact (Severomorsk, Baltiysk). No overlapping names. Kiev, Minsk, Riga are
   plain cities, not capitals.
4. **Zoom in on the Kola peninsula**: Murmansk, Severomorsk, Polyarny, Gadzhiyevo, Vidyayevo, Zapadnaya Litsa
   appear one after another; air bases (Afrikanda …) with a red aircraft. Names sit right of their symbol, or left
   where the right is taken. Panning moves them with the map without flicker.
5. **Unit symbols, waypoints and the selection stay on top** of the labels; clicks and drags on the map work as
   before, also right on a label.
6. **Layers menu → Cities and bases** off and on (or F7): all labels go and come back.
7. **Night theme** of the map: light names with dark outlines.
8. **A mission after 1991** (there is one in June 1991, others later): Leningrad becomes St. Petersburg (after
   September 1991), Karl-Marx-Stadt Chemnitz, no East/West Berlin and no Bonn as capital after October 1990.
9. Config: `Density = More`, `Civil airports = true`, `Label size = 1.3` – the labels are made again at once.
10. Close the game normally and tell Claude. No `[Error  :SeaPowerBorders]` lines expected.

Worth reporting: a naval base in the wrong place or with the wrong side, a name of the wrong time, labels drawn over
the units, a stutter when zooming (the pick is logged with verbose log on).

### Result 2026-10-24 (Claude, with the Shot Director dev tool from SeaPowerTopo)

- **Trollfjorden (1985-02-26):** `1619 places for 1985-02-26 (494 from the game's lists, 90 naval bases, 177
  capitals)`. Steps 1–7 as expected: East Berlin and Bonn capitals, Kiev not; Kola bases red, NATO bases blue; names
  left of their symbol where the right is taken; units on top; F7 and the Layers entry; day and night palettes.
- **Strawberries Can Kill (Red Side), 1991-06-03:** `1617 places … 87 naval bases, 176 capitals`. Berlin capital (not
  East), Bonn no longer, Chemnitz, Rostock and Peenemünde no longer naval bases, Świnoujście and Gdynia still red
  (until July 1991), Leningrad (renamed only in September), Kiev and Minsk plain cities, Nizhny Novgorod (renamed back
  in 1990).
- No `[Error  :SeaPowerBorders]` lines. Found and fixed: the North China Plain was a carpet of names at a 25° view and
  East Germany empty at 9° (label spacing for small places, rank 3 from 14°), the game's "Mildenahll" spelling.
- Not checked: step 9 (config changes while running).
- Test tool, not the mod: loading a mission from inside a running one, or within seconds of the main menu appearing,
  breaks the game (`Gameobject is not a root in a scene`, a `NullReferenceException` in `SeaPower.Utils.GetSOP`).
  Load from the main menu, once it has settled.
