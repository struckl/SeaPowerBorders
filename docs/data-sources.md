# R.A.T.S. Cities and Borders: where the data comes from

The full table. The README says it in one line.

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
