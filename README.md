# Sea Power Borders

> **Status: in development.** Cities and naval bases are in (0.1.0, tested in the game with a 1985 and a 1991
> mission); national borders come next – see [docs/plan.md](docs/plan.md). Screenshots: [workshop/screenshots](workshop/screenshots).

A mod for [Sea Power: Naval Combat in the Missile Age](https://store.steampowered.com/app/1286220/) that puts the
cities, naval bases and (next) the national borders **of the mission's date** on the tactical map. The stock map
shows coastlines only.

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

The Natural Earth list is generated once (download the file the script names into `tools/cities-data/input/`):

```bash
python -I tools/cities-data/build.py tools/cities-data/input/ne_10m_populated_places_simple.geojson src/SeaPowerBorders/mod/places/world.csv
```

Offline check of the places of a date against your game files, and a picture of the labels a view would show:

```bash
dotnet run --project tools/places-check -c Release -- --date 1985-06-26 --find Leningrad
dotnet run --project tools/places-check -c Release -- --view 68.9,33,3 --size 1920x1080 --out kola.tsv
python -I tools/places-check/draw.py kola.tsv "E:/Games/steamapps/common/Sea Power" kola.png
```

```
src/SeaPowerBorders/
  Plugin.cs           Anchor Chain entry point
  AtlasHost.cs        per-frame driver: Layers entry, toggle key, the mission's places, the labels of every map
  CitiesLayer.cs      the "Cities and bases" entry in the map's Layers menu
  Patches.cs          mission start/end hooks, the late map hook
  Places/             Place, the game's lists (GameLists), the mod's lists (ModLists), merging and dates (Atlas)
  Map/                labels in the map (CityLabels), which ones show (LabelPicker), symbols (Icons)
  mod/places/         world.csv (generated), naval_bases.csv, eras.csv
  Settings.cs, ModLog.cs, mod/_info.ini
tools/cities-data/    Natural Earth -> world.csv
tools/places-check/   offline check and label picture
docs/plan.md          the borders, still to build
docs/testing.md       in-game test checklist and results
workshop/screenshots/ in-game screenshots for the Workshop page
```

## Related

- [Sea Power Topographic Map](https://github.com/struckl/SeaPowerTopo) – heights and depths on the same map.
- [Sea Power Radio](https://github.com/struckl/SeaPowerRadio), [Sea Power Pop-Out Windows](https://github.com/struckl/SeaPowerPopOut).

## License

MIT – see [LICENSE](LICENSE). Natural Earth data is public domain. The game's campaign lists are read from your
install, not shipped.
