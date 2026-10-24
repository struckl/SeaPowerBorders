# Changelog

## 0.3.0 – Unreleased

- **Territorial waters** in the Borders layer: a dashed blue line 12 nm off the coast the game's map draws (3 or 6 nm
  or none in the config), around islands too, once zoomed in. Worked out in the game from its 3′ world grid – sea
  flooded from the oceans, so the Caspian and the Dead Sea get none – with an exact distance transform per 10° tile
  on a worker thread. Checked offline, not yet in the game.

## 0.2.0 – Unreleased

- **Borders** layer in the map's Layers menu (Shift+F7): land borders as they were on the mission's date – the Soviet
  Union, the inner-German border and West Berlin, Yugoslavia, Czechoslovakia, North and South Yemen, the Vietnam DMZ,
  Eritrea, South Sudan, East Timor, Hong Kong, Macau, Cyprus. Natural Earth's 1:10m land borders with date rules
  (`mod/borders/eras.csv`), plus the Cold War lines it doesn't have. Disputed lines and lines of control dashed. The
  same width at every zoom. Tested in the game with a 1985 and a 1991 mission; dashed lines drawn in the border
  colour with longer dashes after the test (a lighter tint got lost on the terrain).
- Config section "3. Borders": toggle key, era (mission date or today), disputed lines, line width, opacity.

## 0.1.0 – Unreleased

- **Cities and bases** layer in the map's Layers menu (F7): cities, capitals, naval bases and air bases of the
  mission's date. The game's own campaign lists, about 80 more Cold War naval bases with side and years, and Natural
  Earth for the rest of the world; names, capitals and bases by date (Leningrad, East Berlin, Bonn, Holy Loch until
  1992, …). Labels by zoom without overlaps, below the units. Tested in the game with a 1985 and a 1991 mission.
- Small places keep their distance from each other (crowded regions such as the North China Plain no longer fill up
  with names) and show from a 14° view instead of 9° (East Germany gets Leipzig, Dresden, Karl-Marx-Stadt).
- The game's "Mildenahll" airport is spelled RAF Mildenhall.

## Project set up

- Anchor Chain plugin that loads and logs, config file, Mod Manager entry; build plan in [docs/plan.md](docs/plan.md).
