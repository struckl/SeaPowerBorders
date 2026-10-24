"""Draws the labels picked by places-check over the game's own world grid (land/water), for checking density.

    python -I tools/places-check/draw.py labels.tsv "E:/Games/steamapps/common/Sea Power" out.png
"""
import struct
import sys

from PIL import Image, ImageDraw, ImageFont

COLOURS = {"West": (31, 95, 208), "East": (200, 38, 30), "Other": (100, 100, 100), "None": (110, 110, 110)}
SIZES = [13, 12, 11, 10]


def main(labels, game, target):
    rows = [l.rstrip("\n").split("\t") for l in open(labels, encoding="utf-8")]
    _, west, north, dpp, width, height = rows[0]
    west, north, dpp, width, height = float(west), float(north), float(dpp), int(width), int(height)
    grid = open(f"{game}/Sea Power_Data/StreamingAssets/original/terrain/data/lowres.raw", "rb").read()
    img = Image.new("RGB", (width, height))
    px = img.load()
    for y in range(height):
        ly = min(3599, max(0, int((90 - (north - (y + 0.5) * dpp)) / 0.05)))
        for x in range(width):
            lx = int(((west + (x + 0.5) * dpp) + 180) / 0.05) % 7200
            h = struct.unpack_from("<h", grid, (ly * 7200 + lx) * 2)[0]
            px[x, y] = (196, 186, 170) if h >= 0 else (144, 163, 182)
    d = ImageDraw.Draw(img)
    for label, x, y, kind, side, rank, place in rows[1:]:
        x, y, size = float(x), float(y), SIZES[int(rank)]
        font = ImageFont.truetype("arialbd.ttf" if kind == "capital" or rank == "0" else "arial.ttf", size)
        if kind == "naval":
            d.text((x - 6, y - 8), "⚓", fill=COLOURS[side], font=ImageFont.truetype("seguisym.ttf", 12))
        elif kind in ("air", "airport"):
            d.text((x - 5, y - 7), "✈", fill=COLOURS[side], font=ImageFont.truetype("seguisym.ttf", 11))
        elif kind == "capital":
            d.ellipse((x - 4.5, y - 4.5, x + 4.5, y + 4.5), outline=(34, 40, 49), width=2, fill=(242, 238, 228))
            d.ellipse((x - 2, y - 2, x + 2, y + 2), fill=(34, 40, 49))
        else:
            r = 3 if rank in ("0", "1") else 2.5 if rank == "2" else 2
            d.ellipse((x - r, y - r, x + r, y + r), fill=(34, 40, 49))
        tx = x + 8 if place == "right" else x - 8 - d.textlength(label, font=font)
        d.text((tx, y - size * 0.62), label, fill=(34, 40, 49), font=font, stroke_width=2, stroke_fill=(242, 238, 228))
    img.save(target)
    print(target, len(rows) - 1, "labels")


if __name__ == "__main__":
    main(*sys.argv[1:4])
