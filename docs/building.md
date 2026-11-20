# R.A.T.S. Cities and Borders: building

Build notes, the data tools and the repo layout. Short form in the [README](../README.md).

Needs the R.A.T.S. Core repo next to this one (`SeaPowerCore`, github.com/struckl/SeaPowerCore): the shared project
part, entry point, host, log, settings and hook registry, compiled into the DLL as linked source. Without the workspace layout pass `-p:RatsWorkspace=<workspace>`.

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
