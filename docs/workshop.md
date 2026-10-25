# Releasing on the Steam Workshop

The game uploads Workshop items itself: **Main menu → Mods → Create Mod**, signed in with the Steam account that
owns the item. No SteamCMD needed.

## What every public text must say

The Workshop page, Reddit, the Steam forum and Discord posts all carry the same points (the Workshop description has
them in full, the posts in short):

1. **Display only** – the mod changes nothing in the game or in any mission.
2. **The lines are not the rules of a mission.** Missions set their own areas and rules (*Northern Vigil* announces
   "weapons free" from its own 10 nm circle; *Don't Mess With Texas* fails the mission for attacking an airbase).
   In "don't escalate" missions follow the briefing and the mission's own areas (*Map Layers → On Map Areas*).
3. **Where the data comes from**: Natural Earth (public domain) for borders and cities, the game's own lists read
   from the install, the author's date rules and naval-base list, hand-drawn Berlin Wall and Vietnam DMZ, the coastal
   distance line computed in the game. Credit: "Made with Natural Earth".
4. **What is approximate**: borders are present-day lines with date rules, land only; city ranking uses today's
   populations; base positions are approximate; the coastal distance line is one distance for every country and no
   state's territorial sea.

## Before a release

1. Run the checklists in [testing.md](testing.md) on the current game version (done for 0.1.0–0.3.1 on 2026-10-24
   and 25; 1.0.0 only changed the version number, the Mod Manager text and the docs).
2. Version: `src/SeaPowerBorders/Plugin.cs` (`Version`) and `src/SeaPowerBorders/SeaPowerBorders.csproj`
   (`<Version>`) are 1.0.0; the CHANGELOG has its entry.
3. Check `src/SeaPowerBorders/mod/_info.ini`: Mod Manager name and description, and `[Compatibility]`
   (`ApproximateVersion` = the game version tested against).
4. Make the GitHub repository public (the description links to the source) – or take the link out.
5. Stage the release folder:

   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\package.ps1
   ```

   This builds a release and fills `dist\SeaPowerBorders\` with exactly what players get: the DLL, `_info.ini`,
   `LICENSE.txt`, `preview.jpg`, and the data folders `borders\` and `places\` (about 1.5 MB). With `-Install` (game
   closed) it also copies that folder into `<Sea Power>\Sea Power_Data\StreamingAssets\SeaPowerBorders\`.

## Upload

**Without starting the game** (preferred – like the other mods): `tools/workshop-upload` talks to the running,
signed-in Steam client with the game's own Steamworks.NET. `create` makes a new item and fills it the way the game's
Create Mod screen does (title, description, tags, content, preview, the `[Compatibility]` line of `_info.ini` as
metadata, visibility, then the dependency); it is **private (hidden) unless `--visibility` says otherwise**:

```powershell
dotnet build tools\workshop-upload -c Release
dotnet run --project tools\workshop-upload -c Release --no-build -- create dist\SeaPowerBorders "Sea Power: Rats Cities and Borders" workshop\description.bbcode workshop\preview.jpg workshop\changenote.txt --tags Scripting --depends 3380210757
```

It prints the new item id and link. Put the id into the README and `promotion.md`; later updates are
`... -- upload <item id> dist\SeaPowerBorders workshop\changenote.txt workshop\description.bbcode`, and
`... -- check <item id>` only reads. The `create` command has been built and its `check` sibling run against an
existing item, but **`create` itself has not been run yet** – look at the item page right after (title, tags, the
Anchor Chain requirement, preview) and fix anything in the web form. Make the repository public before the item
goes public.

**Or in the game** (needs the game started, and `package.ps1 -Install` first): Mods → Create Mod:

| Field | Value |
| --- | --- |
| Folder | `StreamingAssets\SeaPowerBorders` (the folder `package.ps1 -Install` filled). **Double-click** `SeaPowerBorders` so the picker is *inside* it, then confirm; confirming in `StreamingAssets` itself gets "… is a forbidden path!" |
| Name | Sea Power: Rats Cities and Borders |
| Description | [workshop/description.bbcode](../workshop/description.bbcode) |
| Preview image | `StreamingAssets\SeaPowerBorders\preview.jpg` (put there by `package.ps1`; Steam wants it under 1 MB) |
| Tags | Scripting |
| Dependencies | `3380210757` (Anchor Chain) |
| Visibility | first upload **Hidden** (or Friends only); Public once the subscribed copy is tested |
| Change log | [workshop/changenote.txt](../workshop/changenote.txt) – a few lines, plain ASCII (long text freezes the field) |

Then add the screenshots on the Workshop page itself: open the item while signed in as its owner → right column
**Owner Controls** → **Add/edit images & videos** → choose the files, drag them into the order below.

| Order | File | Shows |
| --- | --- | --- |
| 1 | 12-borders-europe-1985 | Europe on 26 February 1985 with the Topographic Map colours: borders, capitals, bases of both sides |
| 2 | 13-borders-europe-1991 | the same on 3 June 1991: Germany one, Berlin the capital, the Soviet Union still whole |
| 3 | 14-borders-inner-german | the inner-German border between Wolfsburg and Magdeburg |
| 3b | 21-west-berlin-inner-german | West Berlin as an enclave in the GDR, East Berlin, and the inner-German border down the left (a wider view than 14) |
| 4 | 18-baltic-1985 | the Baltic: Soviet and NATO bases, the Kaliningrad border, (coastal distance line on) |
| 5 | 20-levant-1985 | the Levant: dashed lines of control, (coastal distance line on) |
| 6 | 19-korea-dmz | the Korean demarcation line, dashed |
| 7 | 01-europe-1985 | cities, capitals and bases without borders |
| 8 | 02-kola-northern-fleet | the Northern Fleet bases on the Kola peninsula |
| 9 | 11-germany-1991 | Germany in 1991: Berlin, Chemnitz instead of Karl-Marx-Stadt |
| 10 | 07-night-theme | the night map |
| 11 | 09-layers-menu | the Borders and Cities and bases entries in the map's Layers menu |
| 12–14 | 15, 17, 16 (coastal line) | the optional coastal distance line off Lofoten, in the Aegean (a uniform 12 nm – Greece and Turkey claimed 6 there in 1985) and round the Black Sea. Say in the caption that it is a distance, not a territorial limit |

The other files in `workshop/screenshots/` are spare; 01–11 were taken before the borders existed, so they show
cities and bases only. All were taken in the game at 4K, the FPS strip cut off, resized to 1920 × 1057 (JPG quality
90), day theme, HUD visible, in the missions *Trollfjorden* (1985) and *Strawberries Can Kill* (1991).

## Posts

[workshop/promotion.md](../workshop/promotion.md) has the Reddit, Steam forum and Discord texts, with a caption for
every Reddit picture. The pictures are ready in order: `workshop/reddit/` (gallery, 6) and `workshop/discord/` (3; the
MicroProse post takes the first). Fill in the Workshop link once the item exists. Each text carries the short version
of the points above; do not cut that paragraph. In every picture the game's Discord voice overlay (other people's names
and avatars, top left of the map) is painted out – check that again if you take new ones.

## Testing the Workshop copy

Subscribe, then **remove your local `StreamingAssets\SeaPowerBorders` folder** (or rename it): with two copies only
the first one Anchor Chain finds starts, and the log says `Already loaded from another folder`. The subscribed copy
lives in `steamapps\workshop\content\1286220\<item id>\`. Start a mission and run the short checklist; then switch
the item to Public.

## Still to do after the first release

- Country names of the time on the map (USSR, GDR, FRG, Yugoslavia …).
- Populations of 1985 for the city ranking (the UN's World Urbanization Prospects have them).
- A per-state coastal limit by date (needs a table of claims and the baselines) – or leave it a plain distance.
