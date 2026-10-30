# Changelog

## 1.1.0 – 2026-10-31 – the mission editor (not yet run in the game)

Asked for by a player: borders and cities in the mission editor, not only in missions.

- **The editor's map gets the border lines and the city, base and capital labels**, built for 0.8.7 and
  the 3D-globe build (the editor's map is a flat map in both). Found the way Topographic Map finds it (the editor's view
  model holds the map; the control showing it is looked for in the UI once a second while no mission runs). The panels go
  into the map view model's background elements, after the tiles and before the units, so they stay below the symbols
  and the editor's own drawing and clicks pass through.
- **Switches**: two small buttons, *Borders* and *Cities*, at the top left of the editor's map, and the toggle keys
  (Shift+F7 / F7). The editor has no Layers menu, so the state is kept in the config (the same Visible settings the
  Layers menu entries use); the menu entries appear in missions as before.
- **The date set in the editor** picks the borders and the places, as the mission's date does in a mission; change the
  date in the editor and the map follows within a second. The Era setting (`MissionDate` / `Today`) applies as before.
- Display only, as everywhere: the lines and labels are a reference map, not the mission's zones or rules - the zones
  and rules you set in the editor are the ones that count in the mission.
- Starting a test run from the editor hands the map over to the mission; leaving the mission brings the editor's back.

## Unreleased – both game versions: 0.8.7 and the 3D globe map (game build 26223125, the 11 November release)

Sea Power exists in two versions with different map code, and a mod compiled against one fails on the other. The
package now carries **two builds of the same sources**: `SeaPowerBorders.dll` for the 3D-globe game and
`SeaPowerBordersFlat.dll` for 0.8.7; each looks at the game it runs on (the globe build has `MapDisplayState`) and stays
off on the other, and the log says which runs. The flat build is the code tested in the game with 0.8.7 (0.3.1), plus
the entry check. Checked offline with `tools/compat-check`: every DLL has nothing missing on its own game, and its
entry point nothing missing on either. **Run in the game on the globe build (2026-10-28, Trollfjorden 1985):** layer entries,
labels, borders at three zooms in both palettes, and the coastal line work (two bugs found there and fixed: see below).
**Run in the game on 0.8.7 (2026-10-28, Dangerous Straits 1985):** the flat build runs and the globe build stays off, layer entries
(#601, #602) and the 1619 places / 446 of 511 border lines are loaded, borders and city labels show at zoom 5 and 7 in the day and
night colours. Found there (and fixed before any release): the globe DLL had an internal class with a field of a type that exists
only in the globe game (`GameApi`: `Func<MapPalette, bool>`); Unity's ECS start-up loads every type of every loaded assembly, so on
0.8.7 it failed and **no mission could load** although the globe build stays off. The field is a plain `Delegate` now, and
`tools/compat-check --classload` (run by `tools/package.ps1`) checks every type's base, interfaces and fields against the other
game. Not re-run on the globe with that change. The coastal line was tested on 0.8.7 as well (2026-10-28).

The mission map of the new build is a globe (an orthographic projection), and the old drawing no longer fits it:

- **Layers menu and night colours** use the new map state: the entries go into `MapDisplayState.Tactical`, and the
  line and label colours follow each map's own palette (there is no global theme any more).
- **Border lines and the coastal distance line on the globe** are projected point by point on every change of the view,
  instead of being laid out as lon/lat rectangles (which the globe's converging meridians skew by up to ~9° at 70° N).
  Long straight stretches are cut into steps so they follow the curve (within 0.4 px), lines are cut where they cross the
  horizon, and only the pieces that meet the visible area are projected (0.1–3 ms per view on a 4K screen).
- **City labels** skip the far side of the globe, and their pick follows the point the globe is turned to.
- Found in the game and fixed: the lines go to Noesis in paths of at most 600 points (one path with thousands of points is not
  drawn at all - the borders vanished at wide zoom), the line opacity is in the brush instead of the panel, and a delegate that
  had been left unassigned by the file split.
- The editor's flat map keeps the old drawing (equirectangular projection).
- New offline check `tools/globe-check` (accuracy against the game's projection, horizon cuts, timing, pictures).

## 1.0.0 – first release

Borders, capitals, cities and naval bases of the mission's date on the tactical map, with an optional coastal
distance line. Display only: it changes nothing in the game or in any mission, and its lines are a reference, not the
rules of a mission (README: "Read this first"). Tested in the game with a 1985 and a 1991 mission. What went into it:
the cities (0.1.0), the borders (0.2.0), the coastal line (0.3.x), below.

## 0.3.1

Tested in the game on 2026-10-25: the missing line east of 20°E is gone at all four positions that showed it, median
distance from the drawn coast 12.2 nm.

- The line is called **coastal distance line** and is **off by default**: it is one distance from the game's coast for
  every country, not any state's territorial sea, and not a zone of any mission. README ("Read this first") and the
  config text say so, with the data sources.
- Drawn as small 2.5° cells with the opacity in the brush, instead of one path per 10° tile inside an opacity group:
  at some map positions a tile's line did not show although its data was there. The scale is also re-synced every
  frame. Verbose log: every few seconds the size, position and visibility of the cell paths nearest the view.

## 0.3.0

- **Coastal distance line** (then called territorial waters) in the Borders layer: a dashed blue line 12 nm off the
  coast the game's map draws (3 or 6 nm or none in the config), around islands too, once zoomed in. Worked out in the
  game from its 3′ world grid – sea flooded from the oceans, so the Caspian and the Dead Sea get none – with an exact
  distance transform per 10° tile on a worker thread. Tested in the game: a median 12.0 nm from the drawn coast.

## 0.2.0

- **Borders** layer in the map's Layers menu (Shift+F7): land borders as they were on the mission's date – the Soviet
  Union, the inner-German border and West Berlin, Yugoslavia, Czechoslovakia, North and South Yemen, the Vietnam DMZ,
  Eritrea, South Sudan, East Timor, Hong Kong, Macau, Cyprus. Natural Earth's 1:10m land borders with date rules
  (`mod/borders/eras.csv`), plus the Cold War lines it doesn't have. Disputed lines and lines of control dashed. The
  same width at every zoom. Tested in the game with a 1985 and a 1991 mission; dashed lines drawn in the border
  colour with longer dashes after the test (a lighter tint got lost on the terrain).
- Config section "3. Borders": toggle key, era (mission date or today), disputed lines, line width, opacity.

## 0.1.0

- **Cities and bases** layer in the map's Layers menu (F7): cities, capitals, naval bases and air bases of the
  mission's date. The game's own campaign lists, about 80 more Cold War naval bases with side and years, and Natural
  Earth for the rest of the world; names, capitals and bases by date (Leningrad, East Berlin, Bonn, Holy Loch until
  1992, …). Labels by zoom without overlaps, below the units. Tested in the game with a 1985 and a 1991 mission.
- Small places keep their distance from each other (crowded regions such as the North China Plain no longer fill up
  with names) and show from a 14° view instead of 9° (East Germany gets Leipzig, Dresden, Karl-Marx-Stadt).
- The game's "Mildenahll" airport is spelled RAF Mildenhall.

## Project set up

- Anchor Chain plugin that loads and logs, config file, Mod Manager entry; build plan in [docs/plan.md](docs/plan.md).
