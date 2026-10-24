"""Builds src/SeaPowerBorders/mod/borders/borders.bin from Natural Earth's boundary lines.

Today's land borders, each with the countries on both sides and its class (international, disputed, line of
control ...); the mod leaves out by mission date the ones that weren't borders yet (mod/borders/eras.csv). Added, with
their own years: the inner-German border and the North/South Yemen line (both from Natural Earth's state lines),
West Berlin and the Vietnam DMZ (tools/borders-data/extra.csv).

Every line is simplified at a few tolerances (the mod picks one by zoom) and cut into 10° tiles.

Input from Natural Earth (public domain), release v5.1.2, into tools/borders-data/input/:
https://raw.githubusercontent.com/nvkelso/natural-earth-vector/v5.1.2/geojson/ne_10m_admin_0_boundary_lines_land.geojson
https://raw.githubusercontent.com/nvkelso/natural-earth-vector/v5.1.2/geojson/ne_10m_admin_1_states_provinces_lines.geojson

    python -I tools/borders-data/build.py tools/borders-data/input tools/borders-data/extra.csv \
        src/SeaPowerBorders/mod/borders/borders.bin

borders.bin, little-endian:
    "SPBD", int32 version
    int32 level count, float32 tolerance (degrees) per level
    int32 line count, per line: byte class, 3 bytes country left, 3 bytes country right, int32 since, int32 until
        (yyyymmdd, 0 = open), byte name length, name (UTF-8)
    int32 piece count, per piece: int32 line, byte level, byte tile x, byte tile y, int32 point count,
        float32 lon/lat pairs
"""

import json
import math
import struct
import sys

VERSION = 1
TILE = 10.0
# Douglas-Peucker tolerances in degrees (the map is equirectangular: a degree is as wide as it is high). The mod
# shows the coarsest level that stays within half a pixel.
LEVELS = [0.0005, 0.003, 0.012, 0.05]

CLASSES = {
    "International boundary": 0,
    "Indefinite": 1,
    "Disputed": 2,
    "Line of control": 3,
    "Lease limit": 4,
    "Overlay limit": 5,
    "Indeterminant frontier": 6,
    "Unrecognized": 7,
}
ADDED = 8

WEST_LAENDER = {"Schleswig-Holstein", "Niedersachsen", "Hessen", "Bayern"}
EAST_LAENDER = {"Mecklenburg-Vorpommern", "Brandenburg", "Sachsen-Anhalt", "Thüringen", "Sachsen"}
# Today's governorates on either side of the 1967-1990 line. Al Dali' was made in 1998 from both; most of it was
# South Yemen, so the line runs a few km off around Qa'tabah and Damt.
SOUTH_YEMEN = {"Lahij", "Abyan", "Shabwah", "Hadramawt", "Al Mahrah", "`Adan", "Al Dali'"}
NORTH_YEMEN = {"Ta`izz", "Ibb", "Al Bayda'", "Ma'rib", "Al Jawf", "Sana'a", "Dhamar", "Al Hudaydah", "Sa`dah",
               "Hajjah", "Amran", "Raymah", "Al Mahwit", "Amanat Al Asimah"}
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
    """Douglas-Peucker, ends kept (so lines still meet where they did)."""
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


def main(input_dir, extra_path, target):
    with open(f"{input_dir}/ne_10m_admin_0_boundary_lines_land.geojson", encoding="utf-8") as f:
        land = json.load(f)["features"]
    with open(f"{input_dir}/ne_10m_admin_1_states_provinces_lines.geojson", encoding="utf-8") as f:
        states = json.load(f)["features"]

    lines = []    # (class, left, right, since, until, name, [polylines])
    skipped = 0
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

    by_pair = {}
    for cls, left, right, _, _, _, polylines in lines:
        by_pair.setdefault(frozenset((left, right)), []).extend(polylines)

    inner_german, yemen, berlin = [], [], []
    for feature in states:
        p = feature["properties"]
        a, b = p.get("NAME_L"), p.get("NAME_R")
        polylines = [[tuple(c[:2]) for c in part] for part in parts(feature["geometry"])]
        if p.get("ADM0_A3") == "DEU":
            if {a, b} == {"Berlin", "Brandenburg"}:
                berlin.extend(polylines)
            elif (a in WEST_LAENDER and b in EAST_LAENDER) or (a in EAST_LAENDER and b in WEST_LAENDER):
                inner_german.extend(polylines)
        elif p.get("ADM0_A3") == "YEM":
            if (a in SOUTH_YEMEN and b in NORTH_YEMEN) or (a in NORTH_YEMEN and b in SOUTH_YEMEN):
                yemen.extend(polylines)
    if not inner_german or not yemen or not berlin:
        raise SystemExit(f"state lines missing: inner-German {len(inner_german)}, Yemen {len(yemen)}, Berlin {len(berlin)}")
    lines.append((ADDED, "DDR", "BRD", ymd("1949-05-23"), ymd("1990-10-03"), "Inner-German border", join(inner_german)))
    lines.append((ADDED, "YMD", "YAR", ymd("1967-11-30"), ymd("1990-05-22"), "North/South Yemen", join(yemen)))

    chains = join(berlin)
    ring = chains[0]
    if len(chains) != 1 or ring[0] != ring[-1]:
        raise SystemExit(f"the state line around Berlin is not one closed ring ({len(chains)} pieces)")
    with open(extra_path, encoding="utf-8") as f:
        rows = [l.strip() for l in f if l.strip() and not l.startswith("#")]
    for row in rows[1:]:
        name, since, until, snap, points = row.split(";")
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
            # West Berlin's outer boundary: the arc of the state line between the Wall's two ends on the west side.
            i, j = ring.index(polyline[0]), ring.index(polyline[-1])
            i, j = min(i, j), max(i, j)
            arc_a = ring[i:j + 1]
            arc_b = ring[j:] + ring[1:i + 1]
            west = arc_a if sum(p[0] for p in arc_a) / len(arc_a) < sum(p[0] for p in arc_b) / len(arc_b) else arc_b
            polylines.append(west)
        lines.append((ADDED, "DDR", "WBE" if snap == "Berlin" else "VNM", ymd(since), ymd(until), name, polylines))

    pieces = []    # (line, level, tile, points)
    counts = [0] * len(LEVELS)
    for index, line in enumerate(lines):
        for polyline in line[6]:
            for level, tolerance in enumerate(LEVELS):
                simple = simplify(polyline, tolerance)
                for tile, piece in cut(simple):
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
    print(f"{target}: {len(lines)} lines ({skipped} zone edges left out), {len(pieces)} pieces, points per level "
          + ", ".join(f"{t} deg: {n}" for t, n in zip(LEVELS, counts)))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3])
