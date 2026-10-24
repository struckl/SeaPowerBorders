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
