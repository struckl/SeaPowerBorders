# Sea Power Borders

> **Status: in development.** The project builds and loads, but nothing is drawn yet. The build plan is in
> [docs/plan.md](docs/plan.md).

A mod for [Sea Power: Naval Combat in the Missile Age](https://store.steampowered.com/app/1286220/) that puts
national borders on the tactical map – the borders **of the mission's date**, not today's. The stock map shows
coastlines only, and the game has no border data at all.

Planned:

- **Borders** entry in the map's Layers menu, switchable on its own (next to Topographic Map's Topography layer).
- **Land borders of the time**, chosen by the mission's date: the Soviet Union in one piece, East and West Germany,
  Yugoslavia, Czechoslovakia, North and South Yemen, … – drawn as sharp lines of the same width at every zoom level;
  disputed lines and lines of control dashed.
- **Territorial waters** as a thin dashed line 12 nm (or 3 nm) from the game's own coastline.
- **Country names** of the time (USSR, GDR, FRG, …), optional.

## Data

Land borders: [Natural Earth](https://www.naturalearthdata.com/) 1:10m Admin 0 boundary lines (public domain), with
the Cold War changes applied by date in the mod. Territorial waters are worked out in the game from its own terrain
data – no outside data. Why not other datasets: see [docs/plan.md](docs/plan.md#border-data).

## Building

Same setup as the other Sea Power mods (Anchor Chain on top of BepInEx 5):

```bash
dotnet build src/SeaPowerBorders/SeaPowerBorders.csproj -c Release
```

copies `SeaPowerBorders.dll` and `mod/` into `Sea Power_Data/StreamingAssets/SeaPowerBorders/` when the game is
installed (pass `-p:Deploy=false` to skip that, `-p:GameDir=...` for another install). Enable "Sea Power Borders" in
the game's Mod Manager. Config: `BepInEx/config/dev.sewerlabs.seapowerborders.cfg`.

```
src/SeaPowerBorders/
  Plugin.cs           Anchor Chain entry point
  Settings.cs         config
  ModLog.cs           log source "SeaPowerBorders"
  mod/_info.ini       Mod Manager entry
docs/plan.md          what to build and in which order
```

## Related

- [Sea Power Topographic Map](https://github.com/struckl/SeaPowerTopo) – heights and depths on the same map; the
  map-scene and Layers-menu code to reuse.
- [Sea Power Radio](https://github.com/struckl/SeaPowerRadio), [Sea Power Pop-Out Windows](https://github.com/struckl/SeaPowerPopOut).

## License

MIT – see [LICENSE](LICENSE). Natural Earth data is public domain.
