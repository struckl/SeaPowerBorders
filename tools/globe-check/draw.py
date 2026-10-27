"""Draws the segments globe-check wrote (one view of the globe) as a picture.

    python -I tools/globe-check/draw.py globe.tsv globe.png
"""
import sys

from PIL import Image, ImageDraw

source, target = sys.argv[1], sys.argv[2]
with open(source, encoding="utf-8") as f:
    width, height, radius = f.readline().split("\t")
    width, height, radius = int(width), int(height), float(radius)
    lines = [[tuple(float(v) for v in p.split(",")) for p in row.rstrip("\n").split("\t")] for row in f if row.strip()]

image = Image.new("RGB", (width, height), (24, 34, 52))
draw = ImageDraw.Draw(image)
cx, cy = width / 2, height / 2
draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=(34, 52, 78), outline=(90, 110, 140))
for line in lines:
    if len(line) > 1:
        draw.line(line, fill=(235, 230, 200), width=2, joint="curve")
image.save(target)
print(f"{len(lines)} figures -> {target}")
