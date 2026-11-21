# R.A.T.S. Cities and Borders: building

Build notes, the data tools and the repo layout. Short form in the [README](../README.md).

Needs the SeaPowerRigging repo (R.A.T.S. Rigging) next to this one: the shared project part (`build\Rats.Mod.props`) and the
library with the start, host, log, settings and hook registry. Rigging is built on demand as a project reference and its `ref\`
folder holds Anchor Chain's library. Without the workspace layout pass `-p:RatsWorkspace=<workspace>`, or `-p:RatsRigging=<folder>\`
for another copy of the Rigging repo.

```bash
dotnet build src/SeaPowerBorders/SeaPowerBorders.csproj -c Release
```

- Copies `SeaPowerBorders.dll` and `mod/` into `Sea Power_Data/StreamingAssets/SeaPowerBorders/` when the game is installed.
- `-p:Deploy=false` skips that, `-p:GameDir=...` picks another install.
- One build, for the game's current main version: `SeaPowerBorders.dll`, and the metadata says 0.9.0 or newer.
- `-p:ManagedDir=...` points at a saved copy of the game's `Managed` folder instead of the install.
- `tools/package.ps1` builds, checks the DLL against the game and against Rigging, and stages the release folder with the
  built-in copy of `SeaPowerRigging.dll`.
- `python tools/modtool.py compat borders` from the workspace checks that every game reference of the DLL exists in the game.

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
  Plugin.cs, Rig.cs       entry point ([ACPlugin], started by Keel); the mod's R.A.T.S. Rigging object and its start
  Host.cs                 per-frame driver (RigHost): Layers entries, toggle keys, the data date, every map's layers
  Features/               the host's parts: places and borders of a date, the editor's map, maps decorated, looks
  Hooks.cs                the hooked game methods (Rig.Hooks): mission start and end, the late map hook
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
docs/                     plan.md (country names still to build), workshop.md, borders-format.md
workshop/                 description.bbcode, changenote.txt, preview.jpg, screenshots/
```
