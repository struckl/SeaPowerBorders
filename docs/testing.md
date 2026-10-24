# In-game test checklist

Log: `E:\Games\steamapps\common\Sea Power\BepInEx\LogOutput.log`. Config:
`BepInEx\config\dev.sewerlabs.seapowerborders.cfg` – turn on `Verbose log` for this test (every label pick and
border level change is logged).

## 0.3.0 – Territorial waters (first build)

1. **Trollfjorden (1985), map zoomed in on the Norwegian coast** (about 6° view or closer). Log: `Sea mask read in …
   ms.` once, then with verbose log `Territorial waters 12 nm, tile …` for the tiles around the view. A dashed blue
   line appears off the coast within a second or so, outside the outermost islands.
2. **Measure:** with the map's ruler (or its scale bar), coast to line ≈ 12 nm all along, also round islands.
3. **Zoom out** until the line would crowd the coast: it goes; zoom in again: back, finer. Line width stays the same.
4. **Pan along the coast:** new tiles come in without stutter; no gaps where tiles meet (every 10°).
5. **The Baltic and the Black Sea** have lines (seas the 3′ grid may cut off); the Caspian has none.
6. Config `Territorial waters = 3`, `6`, `0` – applied at once; Borders layer off – the line goes too.
7. Night theme: light blue.

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
