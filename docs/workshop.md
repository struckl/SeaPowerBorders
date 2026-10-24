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

1. Run the checklists in [testing.md](testing.md) on the current game version (0.3.1 is not yet checked in the
   game: the coastal line was reworked after a bug).
2. Bump the version in `src/SeaPowerBorders/Plugin.cs` (`Version`) and `src/SeaPowerBorders/SeaPowerBorders.csproj`
   (`<Version>`) – 1.0.0 for the first upload – and move the CHANGELOG entries under it.
3. Check `src/SeaPowerBorders/mod/_info.ini`: Mod Manager name and description, and `[Compatibility]`
   (`ApproximateVersion` = the game version tested against).
4. Make the GitHub repository public (the description links to the source) – or take the link out.
5. Close Sea Power, then stage and install the release folder:

   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\package.ps1 -Install
   ```

   This replaces `<Sea Power>\Sea Power_Data\StreamingAssets\SeaPowerBorders\` with exactly what players get: the
   DLL, `_info.ini`, `LICENSE.txt`, `preview.jpg`, and the data folders `borders\` and `places\` (about 1.5 MB).
   Without `-Install` it only stages `dist\SeaPowerBorders\`.

## Upload

Start Sea Power → Mods → Create Mod:

| Field | Value |
| --- | --- |
| Folder | `StreamingAssets\SeaPowerBorders` (the folder `package.ps1 -Install` filled). **Double-click** `SeaPowerBorders` so the picker is *inside* it, then confirm; confirming in `StreamingAssets` itself gets "… is a forbidden path!" |
| Name | Sea Power Borders |
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

[workshop/promotion.md](../workshop/promotion.md) has the Reddit, Steam forum and Discord texts. Fill in the Workshop
link once the item exists. Each carries the short version of the points above; do not cut that paragraph.

## Testing the Workshop copy

Subscribe, then **remove your local `StreamingAssets\SeaPowerBorders` folder** (or rename it): with two copies only
the first one Anchor Chain finds starts, and the log says `Already loaded from another folder`. The subscribed copy
lives in `steamapps\workshop\content\1286220\<item id>\`. Start a mission and run the short checklist; then switch
the item to Public.

## Still to do after the first release

- Country names of the time on the map (USSR, GDR, FRG, Yugoslavia …).
- Populations of 1985 for the city ranking (the UN's World Urbanization Prospects have them).
- A per-state coastal limit by date (needs a table of claims and the baselines) – or leave it a plain distance.
