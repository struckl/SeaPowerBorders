"""Builds src/SeaPowerBorders/mod/borders/borders.bin from Natural Earth's boundary lines.

Today's land borders, each with the countries on both sides and its class (international, disputed, line of
control ...); the mod leaves out by mission date the ones that weren't borders yet (mod/borders/eras.csv). Added, with
their own years: the inner-German border and the North/South Yemen line (both from Natural Earth's state lines),
West Berlin and the Vietnam DMZ (tools/borders-data/extra.csv).

Every line is simplified at a few tolerances (the mod picks one by zoom) and cut into tiles.

Input from Natural Earth (public domain), release v5.1.2, into tools/borders-data/input/:
https://raw.githubusercontent.com/nvkelso/natural-earth-vector/v5.1.2/geojson/ne_10m_admin_0_boundary_lines_land.geojson
https://raw.githubusercontent.com/nvkelso/natural-earth-vector/v5.1.2/geojson/ne_10m_admin_1_states_provinces_lines.geojson

    python -I tools/borders-data/build.py tools/borders-data/input tools/borders-data/extra.csv \
        src/SeaPowerBorders/mod/borders/borders.bin

The file layout is in docs/borders-format.md. The file version, the tile size and the class codes come from the
mod's C# (BorderData.cs, GeoPiece.cs), so the two cannot drift apart.
"""

import json
import math
import os
import re
import struct
import sys

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "src", "SeaPowerBorders", "Borders")


def from_csharp(file, pattern):
    """The first group of a pattern in one of the mod's C# files."""
    with open(os.path.join(SRC, file), encoding="utf-8") as f:
        match = re.search(pattern, f.read(), re.S)
    if not match:
        raise SystemExit(f"{file}: {pattern} not found")
    return match.group(1)


VERSION = int(from_csharp("BorderData.cs", r"public const int Version = (\d+);"))
TILE = float(from_csharp("GeoPiece.cs", r"public const float Size = ([\d.]+)f;"))
# The class byte is the position of the name in BorderData.cs's enum BorderClass.
CLASS_NAMES = [n.strip() for n in from_csharp("BorderData.cs", r"enum BorderClass : byte\s*\{(.*?)\}").split(",") if n.strip()]
# Douglas-Peucker tolerances in degrees (the map is equirectangular: a degree is as wide as it is high). The mod
# shows the coarsest level that stays within half a pixel.
LEVELS = [0.0005, 0.003, 0.012, 0.05]

# Natural Earth's FEATURECLA (by prefix) -> the enum's name.
FEATURE_CLASSES = {
    "International boundary": "International",
    "Indefinite": "Indefinite",
    "Disputed": "Disputed",
    "Line of control": "LineOfControl",
    "Lease limit": "Lease",
    "Overlay limit": "Overlay",
    "Indeterminant frontier": "Indeterminant",
    "Unrecognized": "Unrecognized",
}
missing = set(CLASS_NAMES) - set(FEATURE_CLASSES.values()) - {"Added"}
if missing or set(FEATURE_CLASSES.values()) - set(CLASS_NAMES):
    raise SystemExit(f"BorderClass and FEATURE_CLASSES differ: {sorted(missing)} / {sorted(set(FEATURE_CLASSES.values()) - set(CLASS_NAMES))}")
CLASSES = {prefix: CLASS_NAMES.index(name) for prefix, name in FEATURE_CLASSES.items()}
ADDED = CLASS_NAMES.index("Added")

# Lines of the time made from Natural Earth's state lines: name, the countries on its two sides, its years, the
# country the states are in today, and the states on either side.
STATE_LINES = [
    ("Inner-German border", "DDR", "BRD", "1949-05-23", "1990-10-03", "DEU",
     {"Mecklenburg-Vorpommern", "Brandenburg", "Sachsen-Anhalt", "Thüringen", "Sachsen"},
     {"Schleswig-Holstein", "Niedersachsen", "Hessen", "Bayern"}),
    # Today's governorates on either side of the 1967-1990 line. Al Dali' was made in 1998 from both; most of it was
    # South Yemen, so the line runs a few km off around Qa'tabah and Damt.
    ("North/South Yemen", "YMD", "YAR", "1967-11-30", "1990-05-22", "YEM",
     {"Lahij", "Abyan", "Shabwah", "Hadramawt", "Al Mahrah", "`Adan", "Al Dali'"},
     {"Ta`izz", "Ibb", "Al Bayda'", "Ma'rib", "Al Jawf", "Sana'a", "Dhamar", "Al Hudaydah", "Sa`dah",
      "Hajjah", "Amran", "Raymah", "Al Mahwit", "Amanat Al Asimah"}),
]
SNAP = 0.25


def parts(geometry):
    if geometry is None:
        return []
    if geometry["type"] == "LineString":
        return [geometry["coordinates"]]
    if geometry["type"] == "MultiLineString":
        return geometry["coordinates"]
    return []


def ymd(text):
    if not text:
        return 0
    y, m, d = (int(v) for v in text.split("-"))
    return y * 10000 + m * 100 + d


def class_of(featurecla):
    for prefix, code in CLASSES.items():
        if featurecla.startswith(prefix):
            return code
    raise ValueError(f"unknown border class: {featurecla}")


def simplify(points, tolerance):
    """Douglas-Peucker, ends kept (so lines still meet where they did). The mod has the same in C#
    (SeaLimits.Simplify) for the coastal line it works out in the game."""
    if len(points) < 3:
        return list(points)
    keep = [False] * len(points)
    keep[0] = keep[-1] = True
    stack = [(0, len(points) - 1)]
    while stack:
        a, b = stack.pop()
        ax, ay = points[a]
        bx, by = points[b]
        dx, dy = bx - ax, by - ay
        length = math.hypot(dx, dy)
        worst, index = -1.0, -1
        for i in range(a + 1, b):
            px, py = points[i]
            if length == 0:
                d = math.hypot(px - ax, py - ay)
            else:
                d = abs(dy * (px - ax) - dx * (py - ay)) / length
            if d > worst:
                worst, index = d, i
        if worst > tolerance:
            keep[index] = True
            stack.append((a, index))
            stack.append((index, b))
    return [p for p, k in zip(points, keep) if k]


def tile_of(lon, lat):
    tx = min(int((lon + 180.0) // TILE), int(360 / TILE) - 1)
    ty = min(int((lat + 90.0) // TILE), int(180 / TILE) - 1)
    return max(tx, 0), max(ty, 0)


def cut(points):
    """Splits a polyline where it crosses a tile edge: [(tile, points)], neighbours sharing the crossing point."""
    pieces = []
    current = [points[0]]
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        ts = []
        for edge in range(int(math.floor(min(x0, x1) / TILE)) + 1, int(math.floor(max(x0, x1) / TILE)) + 1):
            if x1 != x0:
                ts.append((edge * TILE - x0) / (x1 - x0))
        for edge in range(int(math.floor(min(y0, y1) / TILE)) + 1, int(math.floor(max(y0, y1) / TILE)) + 1):
            if y1 != y0:
                ts.append((edge * TILE - y0) / (y1 - y0))
        for t in sorted(t for t in ts if 0 < t < 1):
            p = (x0 + (x1 - x0) * t, y0 + (y1 - y0) * t)
            current.append(p)
            pieces.append(current)
            current = [p]
        current.append((x1, y1))
    pieces.append(current)
    out = []
    for piece in pieces:
        if len(piece) < 2:
            continue
        mx = (piece[0][0] + piece[1][0]) / 2
        my = (piece[0][1] + piece[1][1]) / 2
        out.append((tile_of(mx, my), piece))
    return out


def nearest(point, candidates):
    best, distance = None, float("inf")
    for c in candidates:
        d = math.hypot(c[0] - point[0], c[1] - point[1])
        if d < distance:
            best, distance = c, d
    return best, distance


def join(segments):
    """Chains line parts that share end points into as few polylines as possible."""
    segments = [list(s) for s in segments]
    chains = []
    while segments:
        chain = segments.pop()
        grown = True
        while grown:
            grown = False
            for i, s in enumerate(segments):
                if s[0] == chain[-1]:
                    chain += s[1:]
                elif s[-1] == chain[-1]:
                    chain += s[::-1][1:]
                elif s[-1] == chain[0]:
                    chain = s[:-1] + chain
                elif s[0] == chain[0]:
                    chain = s[::-1][:-1] + chain
                else:
                    continue
                segments.pop(i)
                grown = True
                break
        chains.append(chain)
    return chains


def land_lines(land):
    """Today's borders: (class, left, right, since, until, name, [polylines]); and how many zone edges were left out."""
    lines, skipped = [], 0
    for feature in land:
        p = feature["properties"]
        left, right = p["ADM0_A3_L"], p["ADM0_A3_R"]
        # Lines with the same country on both sides are the edges of zones inside it (the Korean DMZ) - not borders.
        if not left or not right or left == right:
            skipped += 1
            continue
        # Natural Earth stores many lines as hundreds of short parts end to end: one polyline each again.
        polylines = join([[tuple(c[:2]) for c in part] for part in parts(feature["geometry"])])
        lines.append((class_of(p["FEATURECLA"]), left, right, 0, 0, f"{left}-{right}", polylines))
    return lines, skipped


def state_lines(states):
    """The STATE_LINES as lines, and the state line around Berlin (one closed ring) for West Berlin."""
    found = {line[0]: [] for line in STATE_LINES}
    berlin = []
    for feature in states:
        p = feature["properties"]
        a, b = p.get("NAME_L"), p.get("NAME_R")
        polylines = [[tuple(c[:2]) for c in part] for part in parts(feature["geometry"])]
        if p.get("ADM0_A3") == "DEU" and {a, b} == {"Berlin", "Brandenburg"}:
            berlin.extend(polylines)
        for name, _, _, _, _, country, side_a, side_b in STATE_LINES:
            if p.get("ADM0_A3") == country and ((a in side_a and b in side_b) or (a in side_b and b in side_a)):
                found[name].extend(polylines)
    if not all(found.values()) or not berlin:
        raise SystemExit(f"state lines missing: {', '.join(f'{n} {len(v)}' for n, v in found.items())}, Berlin {len(berlin)}")
    lines = [(ADDED, left, right, ymd(since), ymd(until), name, join(found[name]))
             for name, left, right, since, until, _, _, _ in STATE_LINES]
    chains = join(berlin)
    ring = chains[0]
    if len(chains) != 1 or ring[0] != ring[-1]:
        raise SystemExit(f"the state line around Berlin is not one closed ring ({len(chains)} pieces)")
    return lines, ring


def extra_lines(extra_path, by_pair, ring):
    """The hand-drawn lines of extra.csv, their ends snapped onto the line they meet."""
    with open(extra_path, encoding="utf-8") as f:
        rows = [l.strip() for l in f if l.strip() and not l.startswith("#")]
    lines = []
    for row in rows[1:]:
        name, since, until, countries, snap, points = row.split(";")
        left, right = countries.split("-")
        polyline = [tuple(reversed([float(v) for v in pair.split()])) for pair in points.split(",")]
        targets = ring if snap == "Berlin" else [pt for pl in by_pair.get(frozenset(snap.split("-")), []) for pt in pl]
        if not targets:
            raise SystemExit(f"{name}: nothing to snap to ({snap})")
        for end in (0, -1):
            point, distance = nearest(polyline[end], targets)
            if distance <= SNAP:
                polyline[end] = point
        polylines = [polyline]
        if snap == "Berlin":
            polylines.append(west_arc(ring, polyline))
        lines.append((ADDED, left, right, ymd(since), ymd(until), name, polylines))
    return lines


def west_arc(ring, wall):
    """West Berlin's outer boundary: the arc of the state line between the Wall's two ends on the west side."""
    i, j = ring.index(wall[0]), ring.index(wall[-1])
    i, j = min(i, j), max(i, j)
    arc_a = ring[i:j + 1]
    arc_b = ring[j:] + ring[1:i + 1]
    return arc_a if sum(p[0] for p in arc_a) / len(arc_a) < sum(p[0] for p in arc_b) / len(arc_b) else arc_b


def write(target, lines):
    pieces = []    # (line, level, tile, points)
    counts = [0] * len(LEVELS)
    for index, line in enumerate(lines):
        for polyline in line[6]:
            for level, tolerance in enumerate(LEVELS):
                for tile, piece in cut(simplify(polyline, tolerance)):
                    pieces.append((index, level, tile, piece))
                    counts[level] += len(piece)
    with open(target, "wb") as f:
        f.write(b"SPBD" + struct.pack("<i", VERSION))
        f.write(struct.pack("<i", len(LEVELS)) + struct.pack(f"<{len(LEVELS)}f", *LEVELS))
        f.write(struct.pack("<i", len(lines)))
        for cls, left, right, since, until, name, _ in lines:
            encoded = name.encode("utf-8")
            f.write(struct.pack("<B3s3siiB", cls, left.encode("ascii")[:3].ljust(3), right.encode("ascii")[:3].ljust(3),
                                since, until, len(encoded)) + encoded)
        f.write(struct.pack("<i", len(pieces)))
        for line, level, (tx, ty), piece in pieces:
            f.write(struct.pack("<iBBBi", line, level, tx, ty, len(piece)))
            f.write(struct.pack(f"<{2 * len(piece)}f", *[v for pt in piece for v in pt]))
    return pieces, counts


def main(input_dir, extra_path, target):
    with open(f"{input_dir}/ne_10m_admin_0_boundary_lines_land.geojson", encoding="utf-8") as f:
        land = json.load(f)["features"]
    with open(f"{input_dir}/ne_10m_admin_1_states_provinces_lines.geojson", encoding="utf-8") as f:
        states = json.load(f)["features"]

    lines, skipped = land_lines(land)
    by_pair = {}
    for _, left, right, _, _, _, polylines in lines:
        by_pair.setdefault(frozenset((left, right)), []).extend(polylines)
    added, ring = state_lines(states)
    lines += added
    lines += extra_lines(extra_path, by_pair, ring)
    pieces, counts = write(target, lines)
    print(f"{target}: {len(lines)} lines ({skipped} zone edges left out), {len(pieces)} pieces, points per level "
          + ", ".join(f"{t} deg: {n}" for t, n in zip(LEVELS, counts)))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3])
