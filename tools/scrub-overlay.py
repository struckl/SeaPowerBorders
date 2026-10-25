"""Paints over the Discord voice overlay (other people's names and avatars) in a screenshot, from the ORIGINAL.

Fills the rectangle from its outside inwards, in rounds: each round the pixels that already have enough known map
around them (Gaussian-weighted, small radius) take its average and count as known for the next round. No detail is
smeared across the rectangle and the middle is reached as well. A little noise hides the flatness.

    python -I tools/scrub-overlay.py <original.jpg> <output.jpg>
"""
import random
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

X0, Y0, X1, Y1 = 8, 340, 200, 482      # in the 1920 x 1057 pictures; the widget is about x 22-150, y 355-460
RADIUS = 9
MARGIN = 40
NEEDED = 70                             # of 255: how much known map a pixel needs around it to be filled


def scrub(src, dst):
    im = Image.open(src).convert("RGB")
    cx0, cy0 = max(0, X0 - MARGIN), max(0, Y0 - MARGIN)
    cx1, cy1 = min(im.width, X1 + MARGIN), min(im.height, Y1 + MARGIN)
    cur = im.crop((cx0, cy0, cx1, cy1))
    known = Image.new("L", cur.size, 255)
    ImageDraw.Draw(known).rectangle((X0 - cx0, Y0 - cy0, X1 - cx0 - 1, Y1 - cy0 - 1), fill=0)
    # The picture's own dark 1-2 px frame at the left edge is no map: keep it out of the average.
    ImageDraw.Draw(known).rectangle((0, 0, 3 - cx0, cur.height), fill=0) if cx0 < 4 else None
    box = [(x - cx0, y - cy0) for y in range(Y0, Y1) for x in range(X0, X1)]
    unknown = set(box)
    for _ in range(80):
        if not unknown:
            break
        masked = ImageChops.multiply(cur, Image.merge("RGB", (known, known, known)))
        b1 = masked.filter(ImageFilter.GaussianBlur(RADIUS)).load()
        b2 = known.filter(ImageFilter.GaussianBlur(RADIUS)).load()
        cp, kp = cur.load(), known.load()
        done = []
        for p in unknown:
            w = b2[p]
            if w >= NEEDED:
                f = 255.0 / w
                cp[p] = tuple(max(0, min(255, int(round(b1[p][c] * f)))) for c in range(3))
                done.append(p)
        for p in done:
            kp[p] = 255
            unknown.discard(p)
    random.seed(11)
    cp = cur.load()
    for p in box:
        cp[p] = tuple(max(0, min(255, cp[p][c] + random.randint(-2, 2))) for c in range(3))
    im.paste(cur.crop((X0 - cx0, Y0 - cy0, X1 - cx0, Y1 - cy0)), (X0, Y0))
    im.save(dst, quality=90)


if __name__ == "__main__":
    scrub(sys.argv[1], sys.argv[2])
    print("scrubbed", sys.argv[2].rsplit("/", 1)[-1])
