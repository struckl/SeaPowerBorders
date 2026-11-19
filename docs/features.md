# R.A.T.S. Cities and Borders: feature detail

Everything the mod draws, in full. The short list is in the [README](../README.md).

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

## What it draws

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
