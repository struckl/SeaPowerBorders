# Sea Power: R.A.T.S. Cities and Borders

Draws national borders, cities, capitals and naval bases of the mission's date on the tactical map of
[Sea Power: Naval Combat in the Missile Age](https://store.steampowered.com/app/1286220/), and optionally a coastal distance
line. The stock map shows coastlines only.

- Needs a mod loader, either one: [R.A.T.S. Keel](https://steamcommunity.com/sharedfiles/filedetails/?id=3835527459) or
  [Anchor Chain](https://steamcommunity.com/sharedfiles/filedetails/?id=3380210757).
- [Steam Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3827360064)
- [R.A.T.S. feedback board](https://hub.sewerlabs.dev/s/borders): bugs, ideas, questions

## What the lines are and what they are not

The mod only draws on the map. It changes nothing in the game or in any mission: no rules of engagement, triggers, zones or
scoring, and nothing about what units see or do. What it draws is a reference map. It is not the rules of your mission.

- Missions set their own areas and rules, and these often differ from real geography.
  - *Northern Vigil* announces "weapons free" when two Soviet vessels enter its own 10 nm circle around a point, not when they
    cross a coast or border. The game's Map Layers > On Map Areas shows such areas.
  - *Don't Mess With Texas* fails the mission for attacking an airbase.
  - *Operation Trident* has an exclusion zone off Karachi.
  - Where a mission says "don't escalate" or "hold fire", follow the briefing and the mission's own map areas. Crossing a border
    or the coastal line drawn here is not a violation unless the mission says so.
- Borders are Natural Earth's present-day de-facto lines with date rules: approximate and simplified, with disputed ones drawn
  as Natural Earth draws them. They are land borders only. There are no sea borders, airspace or zones.
- The coastal distance line is one distance from the game's own coastline, the same for every country. It is not any state's
  territorial sea.
  - In 1985 Norway claimed 4 nm, the United States and the United Kingdom 3, Greece and Turkey 6 in the Aegean, the Soviet
    Union 12, and Libya claimed the whole Gulf of Sidra.
  - States also draw their limits from baselines across bays and fjords, not from the shore.
- Cities, capitals and bases come from the game's own lists plus public data. A base on the map does not mean the mission
  models it. The labels are public Cold War locations, not information about the mission's forces. Units and contacts are
  exactly as the game shows them.

## What it does

### Borders

- Land borders as they were on the mission's date:
  - Soviet Union whole (the Baltic states apart from September 1991, the other republics from December 1991)
  - inner-German border and West Berlin until October 1990
  - Yugoslavia until 1991-92, then Serbia and Montenegro until 2006, Kosovo from 2008
  - Czechoslovakia until 1993, North and South Yemen until May 1990, the Vietnam DMZ until 1976
  - Eritrea, South Sudan, East Timor, Hong Kong and Macau by their years
  - the line across Cyprus from August 1974
- Solid lines for borders. Dashed lines for disputed borders, lines of control (Korea, Kashmir, the Golan, Cyprus) and lease
  limits (Guantánamo, Baikonur).
- Same line width on screen at every zoom, with the detail following the zoom. Light lines on the night theme.
- Switched on and off with the Borders entry in the map's Layers menu, or Shift+F7.

### Coastal distance line

- Off by default. A dashed blue line 3, 6 or 12 nm off the coast as the game's map draws it.
- Shows once you are zoomed in far enough that it stands clear of the coast.
- Runs around islands too, and merges where two coasts are less than twice the distance apart.
- Lakes and dry land below sea level (the Caspian, the Dead Sea) get no line.
- Worked out in the game from its own world grid; no terrain data is shipped.
- Checked in the game against its scale bar: a median of 12.0 nm from the drawn coast at the 12 nm setting.

### Cities and bases

- Cities, capitals of the time, naval bases and air bases as small symbols with names, below the unit symbols.
  - Far out you see only capitals, the biggest cities and the main fleet bases. More come in as you zoom.
  - Names do not overlap. Where the right side is taken, the name goes to the left of its symbol.
- Symbols:
  - capital: a ring with a dot
  - city: a dot sized by population
  - air base: an aircraft in its side's colour
  - naval base: an anchor, blue for the US and its allies, red for the Soviet Union and its allies, grey for others
  - civil airports: optional
- The mission's date decides names, capitals and base years:
  - Leningrad, Gorky, Kuibyshev, Sverdlovsk, Voroshilovgrad, Karl-Marx-Stadt, Bombay, Rangoon and Peking (before 1979) carry
    their names of the time
  - East Berlin, West Berlin and Bonn as capital until October 1990
  - Soviet republic capitals are not national capitals before the Union ended
  - bases show only in the years they were used (Holy Loch until 1992, Cam Ranh Bay Soviet 1979-2002, Berbera Soviet
    1972-77, Kings Bay from 1979)
- Light names on dark on the map's night theme.
- Switched on and off with the Cities and bases entry in the Layers menu, or F7.

### Mission editor

- The editor's map shows the same border lines and place labels (1.1.0).
- Two small buttons, Borders and Cities, sit at the top left of the editor's map. Shift+F7 and F7 work there too.
- The editor has no Layers menu, so the choice is kept in the config file.
- Borders and places follow the date set in the editor.
- Where the editor already labels the game's own cities and bases (its Map Labels checkbox), the mod leaves those names to it
  and adds the rest.
- It is still a reference drawing. The zones and rules you set in the editor are what count.

## Where the data comes from

| What | Source |
| --- | --- |
| Land borders | [Natural Earth](https://www.naturalearthdata.com/) 1:10m boundary lines, public domain (release 5.1.2) |
| Date rules (which borders existed when) | the author's rules in `mod/borders/eras.csv` |
| Inner-German border, North/South Yemen, West Berlin | Natural Earth's state lines (the old Länder, today's governorates) |
| Berlin Wall, Vietnam DMZ | drawn by hand in `tools/borders-data/extra.csv`, approximate |
| Coastal distance line | computed in the game from its own 3' world grid |
| Cities, ports, airfields | the game's own campaign lists (`StreamingAssets/original/campaigns`), read from your install at mission start |
| More cities, capitals | Natural Earth populated places, public domain; today's populations, so the ranking of cities of 1985 is not exact |
| Naval bases with side and years | the author's list, `mod/places/naval_bases.csv`, about 90 bases, approximate positions |

Made with Natural Earth. Free vector and raster map data at naturalearthdata.com.

- The game's own lists give about 300 cities with the developers' Cold War names, ports whose role makes them naval bases,
  airports and air bases.
- Natural Earth adds every capital and every place of at least 350,000 people, renamed and re-ranked by date through
  `mod/places/eras.csv`.
- A place that two lists have (same name within 60 km, or anything within 6 km) is shown once, under the first list's name.
  Natural Earth adds the population, a base adds its anchor.

## Install

- Install a mod loader: R.A.T.S. Keel or Anchor Chain (links above).
- Subscribe to the mod on the Steam Workshop.
- Tick R.A.T.S. Cities and Borders in the game's Mod Manager.
- In a mission, turn the layers on in the map's Layers menu (Borders, Cities and bases), or press Shift+F7 and F7.

## Settings

Config file: `BepInEx/config/dev.sewerlabs.seapowerborders.cfg`. The names are in the file's own comments.

| Section | Setting | Default |
| --- | --- | --- |
| 2. Cities | Visible, Toggle key | on, F7 |
| 2. Cities | Density (Fewer, Normal, More) | Normal |
| 2. Cities | Naval bases, Air bases | on |
| 2. Cities | Civil airports | off |
| 2. Cities | Label size (0.7 to 1.6) | 1 |
| 3. Borders | Visible, Toggle key | on, Shift+F7 |
| 3. Borders | Era (MissionDate, Today) | MissionDate |
| 3. Borders | Disputed lines | on |
| 3. Borders | Line width (0.5 to 3) | 1.3 |
| 3. Borders | Opacity (0.2 to 1) | 0.85 |
| 3. Borders | Coastal distance line, nm (0, 3, 6, 12) | 0 (off) |

## Compatibility

- Game: `_info.ini` declares 0.8.7 up to every 0.9.x (`LessThanVersion=0.10.0`).
- Two builds in one package: `SeaPowerBorders.dll` for the 3D-globe game and `SeaPowerBordersFlat.dll` for 0.8.7's flat map.
  Each stays off on the other game. The log says which one runs.
- Run in the game on both the globe build and 0.8.7. The last run was 1.1.3 on 0.9.1 (build 261112), 12 November 2026, under
  Keel (Red Sea Breakout).
- The editor support (1.1.0) was run in the game on 0.8.7; the globe build was checked offline only.
- The mod runs under either loader and uses neither one's own features. On the globe beta Anchor Chain did not start (Anchor
  Chain issue 19) when this was last checked, so there the game runs were under Keel.

## Feedback

- Bugs, ideas and questions go on the [R.A.T.S. feedback board](https://hub.sewerlabs.dev/s/borders).
- For a bug, link `<Sea Power>\BepInEx\LogOutput.log` and name the mission and its start date.

## Building

Needs the Keel repo next to this one: the shared project part, entry point, host, log, settings and hook registry are
`SeaPowerKeel\shared`, compiled into the DLL as linked source. Without the workspace layout pass `-p:RatsWorkspace=<workspace>`.

```bash
dotnet build src/SeaPowerBorders/SeaPowerBorders.csproj -c Release
```

- Copies `SeaPowerBorders.dll` and `mod/` into `Sea Power_Data/StreamingAssets/SeaPowerBorders/` when the game is installed.
- `-p:Deploy=false` skips that, `-p:GameDir=...` picks another install.
- `-p:GameBuild=Flat` builds against 0.8.7's `Managed` folder (`-p:FlatManagedDir=...`; default
  `..\_gamerefs\SeaPower-0.8.7-public\Managed`) into `SeaPowerBordersFlat.dll`, registered as
  `dev.sewerlabs.seapowerborders.flatmap`.
- `tools/package.ps1` builds and checks both and stages the release folder.
- `python tools/modtool.py compat borders` from the workspace checks that every game reference of each DLL exists in the game
  it is made for.

The Natural Earth files are turned into the mod's data once. Download the files the scripts name into
`tools/cities-data/input/` and `tools/borders-data/input/`, then:

```bash
python -I tools/cities-data/build.py tools/cities-data/input/ne_10m_populated_places_simple.geojson src/SeaPowerBorders/mod/places/world.csv
python -I tools/borders-data/build.py tools/borders-data/input tools/borders-data/extra.csv src/SeaPowerBorders/mod/borders/borders.bin
```

Offline check of the places and borders of a date against your game files, and a picture of what a view would show
(`--era Today` for today's borders, `--waters 12` for the coastal line):

```bash
dotnet run --project tools/places-check -c Release -- --date 1985-06-26 --find Leningrad
dotnet run --project tools/places-check -c Release -- --view 68.9,33,3 --size 1920x1080 --out kola.tsv
python -I tools/places-check/draw.py kola.tsv "E:/Games/steamapps/common/Sea Power" kola.png
dotnet run --project tools/globe-check -c Release -- --check
```

The format of `borders.bin` is in [docs/borders-format.md](docs/borders-format.md).

## Layout

```
src/SeaPowerBorders/
  Plugin.cs, ModInfo.cs   entry point and what the shared skeleton reads (GUID, name, version, log source)
  Host.cs                 per-frame driver: Layers entries, toggle keys, the data date, every map's layers
  Features/               the host's parts: places and borders of a date, the editor's map, maps decorated, looks
  Hooks.cs                the hooked game methods: mission start and end, the late map hook
  Borders/                border data, which borders a date has, tiles and levels, the coastal line (SeaMask, SeaLimits)
  Places/                 the game's lists, the mod's lists, merging and dates (Atlas), places the mission marks itself
  Map/                    border lines (flat and globe drawing), labels, label picking, symbols, panel order, projections
  Ui/                     the editor's Borders and Cities buttons
  mod/borders/            borders.bin (generated), eras.csv
  mod/places/             world.csv (generated), naval_bases.csv, eras.csv
  Settings.cs, Choices.cs, DataFiles.cs, FailureLog.cs, mod/_info.ini
tools/borders-data/       Natural Earth -> borders.bin, with the hand-drawn lines (extra.csv)
tools/cities-data/        Natural Earth -> world.csv
tools/places-check/       offline check, label and border picture
tools/globe-check/        offline check of the globe drawing against the game's own projection
tools/package.ps1         stage or install the release folder
docs/                     plan.md (country names still to build), testing.md, workshop.md, borders-format.md
workshop/                 description.bbcode, changenote.txt, preview.jpg, screenshots/
```

## Licenses and credits

- [R.A.T.S. Source License](LICENSE): read it, fork it, send fixes; no re-uploads, no reuse in other projects.
- Natural Earth data is public domain. Made with Natural Earth.
- The game's campaign lists are read from your install, not shipped.
- Related: [R.A.T.S. Topo](https://github.com/struckl/SeaPowerTopo) (heights and depths on the same map),
  [R.A.T.S. Radio](https://github.com/struckl/SeaPowerRadio), [R.A.T.S. Pop-Out](https://github.com/struckl/SeaPowerPopOut).
