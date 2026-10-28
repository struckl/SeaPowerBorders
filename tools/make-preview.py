"""Workshop main picture of the mod: the same look as the other Sea Power mods' pictures (Radio, Topographic Map, Pop-Out) - 1000 x 1000,
a dark, slightly see-through band over the bottom fifth with the title in Segoe UI Semibold and a lighter subtitle below it.
Source picture: workshop/preview-source.jpg (the unbanded map). Writes workshop/preview.jpg (Steam takes under 1 MB).

  python tools/make-preview.py [--out FILE]
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
WORKSHOP = os.path.join(HERE, "..", "workshop")
FONTS = r"C:\Windows\Fonts"
TITLE = "Cities and Borders"
SUBTITLE = "Borders, capitals and naval bases of the day"

BAND_TOP = 790                     # the band of the other pictures starts at y = 790 of 1000
BAND_COLOUR = (11, 16, 22)
BAND_ALPHA = 0.90                  # a little more opaque than Radio's band: this map has more detail under it, and the band should read dark
LEFT = 53
TITLE_BASELINE, TITLE_SIZE, TITLE_COLOUR = 908, 90.7, (250, 252, 255)   # Segoe UI Semibold: width and weight fitted to the other pictures
SUB_BASELINE, SUB_SIZE, SUB_COLOUR = 969, 39.6, (214, 218, 223)       # Segoe UI regular

out = os.path.join(WORKSHOP, "preview.jpg")
if "--out" in sys.argv:
    out = sys.argv[sys.argv.index("--out") + 1]

picture = Image.open(os.path.join(WORKSHOP, "preview-source.jpg")).convert("RGB").resize((1000, 1000), Image.LANCZOS)
band = Image.new("RGB", (1000, 1000 - BAND_TOP), BAND_COLOUR)
region = picture.crop((0, BAND_TOP, 1000, 1000))
picture.paste(Image.blend(region, band, BAND_ALPHA), (0, BAND_TOP))

draw = ImageDraw.Draw(picture)
draw.text((LEFT, TITLE_BASELINE), TITLE, font=ImageFont.truetype(os.path.join(FONTS, "seguisb.ttf"), TITLE_SIZE), fill=TITLE_COLOUR, anchor="ls")
draw.text((LEFT, SUB_BASELINE), SUBTITLE, font=ImageFont.truetype(os.path.join(FONTS, "segoeui.ttf"), SUB_SIZE), fill=SUB_COLOUR, anchor="ls")
picture.save(out, "JPEG", quality=90)
print(out, os.path.getsize(out) // 1024, "KB")
