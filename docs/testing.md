# In-game test checklist

Log: `E:\Games\steamapps\common\Sea Power\BepInEx\LogOutput.log`. Config:
`BepInEx\config\dev.sewerlabs.seapowerborders.cfg` – turn on `Verbose log` for this test (every label pick is
logged).

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
