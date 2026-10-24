# Sea Power Borders

> **Status: in development.** Cities and naval bases (0.1.0, tested in the game with a 1985 and a 1991 mission) and
> national borders (0.2.0, tested in the game with the same two missions) are in; territorial waters and country names come
> next – see [docs/plan.md](docs/plan.md). Screenshots: [workshop/screenshots](workshop/screenshots).

A mod for [Sea Power: Naval Combat in the Missile Age](https://store.steampowered.com/app/1286220/) that puts the
national borders, cities and naval bases **of the mission's date** on the tactical map. The stock map shows
coastlines only.

## Borders

A **Borders** entry in the map's Layers menu (or **Shift+F7**):

- **Land borders as they were on the mission's date:** the Soviet Union whole (the Baltic states apart from
  September 1991, the other republics from December 1991), the inner-German border and West Berlin until October
  1990, Yugoslavia until 1991–92 (then Serbia and Montenegro until 2006, Kosovo from 2008), Czechoslovakia until
  1993, North and South Yemen until May 1990, the Vietnam DMZ until 1976; Eritrea, South Sudan, East Timor, Hong Kong
  and Macau by their years; the line across Cyprus from August 1974.
- Borders solid; disputed borders, lines of control (Korea, Kashmir, the Golan, Cyprus) and lease limits
  (Guantánamo, Baikonur) dashed.
- The same width on screen at every zoom, the detail following the zoom; light lines on the night theme.

Where the lines come from:

1. **Natural Earth** 1:10m land boundary lines (public domain): today's land borders, each with the countries on both
   sides and its class, simplified at four levels and cut into 10° tiles by `tools/borders-data/build.py` into
   `mod/borders/borders.bin` (1.1 MB).
2. **Date rules** in `mod/borders/eras.csv`: *unions* of countries that were one (a border between two members isn't
   drawn while both belong – the Soviet Union, Yugoslavia, Czechoslovakia, Ethiopia with Eritrea, ...), and *line*
   rules for one class of border (the disputed line around Crimea only from 2014).
3. **Added Cold War lines**, with their own years: the inner-German border and the North/South Yemen line from
   Natural Earth's state lines (the old Länder against the new ones; today's Yemeni governorates on either side),
   the Berlin Wall and the Vietnam DMZ hand-drawn in `tools/borders-data/extra.csv` (approximate), West Berlin's outer
   boundary from Natural Earth's state line around Berlin.

How: one Noesis `Path` per tile and line style in a `MapControl.MapPanel` laid into the map below the place labels.
The map places each by its tile's bounding box on every pan and zoom (and wraps it past ±180°); the geometry is in
degrees from the tile's corner with a scale transform set from the map's `ViewportChanged`, so the stroke keeps its
width (`Map/BorderLines.cs`).

Config `BepInEx/config/dev.sewerlabs.seapowerborders.cfg`, section "3. Borders": toggle key, era (`MissionDate` /
`Today`), disputed lines, line width, opacity.

## Cities and bases

A **Cities and bases** entry in the map's Layers menu (or **F7**):

- **Cities, capitals of the time, naval bases and air bases**, as small symbols with their names, below the unit
  symbols. Far out only capitals, the biggest cities and the main fleet bases; more come in as you zoom; names never
  overlap (where the right side is taken, a name goes to the left of its symbol).
- **Naval bases** get an anchor in the colour of their side: blue for the US and its allies, red for the Soviet Union
  and its allies, grey for others – Severomorsk, Polyarny, Gadzhiyevo, Baltiysk, Sevastopol, Vladivostok, Cam Ranh
  Bay; Norfolk, Kings Bay, Holy Loch, Faslane, Brest, Toulon, Kiel, Keflavík, Subic Bay, Diego Garcia, …
- **The mission's date** decides: Leningrad, Gorky, Kuibyshev, Sverdlovsk, Voroshilovgrad, Karl-Marx-Stadt, Bombay,
  Rangoon, Peking (before 1979); East Berlin, West Berlin and Bonn as capital until October 1990; the Soviet
  republics' capitals are not national capitals before the Union ended; bases only in the years they were used
  (Holy Loch until 1992, Cam Ranh Bay Soviet 1979–2002, Berbera Soviet 1972–77, Kings Bay from 1979, …).
- Capitals: a ring with a dot. Cities: a dot sized by population. Air bases: an aircraft in their side's colour;
  civil airports optional.
- Night theme of the map: light names on dark.

Where the places come from:

1. **The game's own campaign lists** (`StreamingAssets/original/campaigns`: `*cities*.ini`, `*ports*.ini`,
   `airports.ini`), read from your install at mission start – about 300 cities with the developers' Cold War names,
   ports whose role makes them naval bases, airports and air bases.
2. **The mod's naval bases** (`mod/places/naval_bases.csv`): about 80 Cold War bases the game's lists don't mark, with
   their side and years. Positions are approximate (a few km).
3. **Natural Earth** populated places (public domain): every capital and every place of at least 350,000 people,
   for the rest of the world, renamed and re-ranked by date through `mod/places/eras.csv`.

A place two lists have (the same name within 60 km, or anything within 6 km) is shown once, under the first list's
name; Natural Earth adds the population, a base adds its anchor.

How: the labels are children of a `MapControl.MapPanel` laid into the map control right above its graticule – the
map itself puts each at its latitude and longitude on every pan and zoom, below its drag surface and the units, so
clicks go through. Which labels show is picked when the zoom changes or a pan has gone a quarter of the view
(`Map/LabelPicker.cs`): within the view and half its size around it, by rank, capitals, population, then naval
bases, each where its box doesn't overlap one already placed. Small places also keep three font sizes across and one
down from each other, so a crowded region (the North China Plain, the Ruhr) shows its big places and a few towns
instead of a carpet of names, while a sparse one (East Germany) keeps its towns.

Config `BepInEx/config/dev.sewerlabs.seapowerborders.cfg`, section "2. Cities": toggle key, density (Fewer / Normal /
More), naval bases, air bases, civil airports, label size.

## Building

Same setup as the other Sea Power mods (Anchor Chain on top of BepInEx 5):

```bash
dotnet build src/SeaPowerBorders/SeaPowerBorders.csproj -c Release
```

copies `SeaPowerBorders.dll` and `mod/` (with `places/`) into `Sea Power_Data/StreamingAssets/SeaPowerBorders/` when
the game is installed (pass `-p:Deploy=false` to skip that, `-p:GameDir=...` for another install). Enable "Sea Power
Borders" in the game's Mod Manager.

The Natural Earth files are turned into the mod's data once (download the files the scripts name into
`tools/cities-data/input/` and `tools/borders-data/input/`):

```bash
python -I tools/cities-data/build.py tools/cities-data/input/ne_10m_populated_places_simple.geojson src/SeaPowerBorders/mod/places/world.csv
python -I tools/borders-data/build.py tools/borders-data/input tools/borders-data/extra.csv src/SeaPowerBorders/mod/borders/borders.bin
```

Offline check of the places and borders of a date against your game files, and a picture of the labels and lines a
view would show (`--era Today` for today's borders):

```bash
dotnet run --project tools/places-check -c Release -- --date 1985-06-26 --find Leningrad
dotnet run --project tools/places-check -c Release -- --view 68.9,33,3 --size 1920x1080 --out kola.tsv
python -I tools/places-check/draw.py kola.tsv "E:/Games/steamapps/common/Sea Power" kola.png
```

```
src/SeaPowerBorders/
  Plugin.cs           Anchor Chain entry point
  AtlasHost.cs        per-frame driver: Layers entries, toggle keys, the mission's places and borders, every map's layers
  LayerEntry.cs       an entry in the map's Layers menu ("Borders", "Cities and bases")
  Patches.cs          mission start/end hooks, the late map hook
  Borders/            the border lines (BorderData) and which ones a date has (BorderEras)
  Places/             Place, the game's lists (GameLists), the mod's lists (ModLists), merging and dates (Atlas)
  Map/                lines in the map (BorderLines), labels (CityLabels), which ones show (LabelPicker), symbols
                      (Icons), the order of our panels (MapSlots)
  mod/borders/        borders.bin (generated), eras.csv
  mod/places/         world.csv (generated), naval_bases.csv, eras.csv
  Settings.cs, ModLog.cs, mod/_info.ini
tools/borders-data/   Natural Earth -> borders.bin, with the hand-drawn lines (extra.csv)
tools/cities-data/    Natural Earth -> world.csv
tools/places-check/   offline check, label and border picture
docs/plan.md          build plan; territorial waters and country names still to build
docs/testing.md       in-game test checklist and results
workshop/screenshots/ in-game screenshots for the Workshop page
```

## Related

- [Sea Power Topographic Map](https://github.com/struckl/SeaPowerTopo) – heights and depths on the same map.
- [Sea Power Radio](https://github.com/struckl/SeaPowerRadio), [Sea Power Pop-Out Windows](https://github.com/struckl/SeaPowerPopOut).

## License

MIT – see [LICENSE](LICENSE). Natural Earth data is public domain. The game's campaign lists are read from your
install, not shipped.
