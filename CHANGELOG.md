# Changelog

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
