"""Social profile-picture variants: the starburst on a solid tile, sized so a
circle crop never clips the spikes. Run: python brand/make_pfp.py"""
import math, os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
PNG = os.path.join(HERE, "png")
os.makedirs(PNG, exist_ok=True)

SIZE = 1000
POINTS = 12
INNER = 0.44

def star(cx, cy, R, r):
    pts = []
    for i in range(POINTS * 2):
        a = math.radians(-90 + i * (360 / (POINTS * 2)))
        rad = R if i % 2 == 0 else r
        pts.append((cx + rad * math.cos(a), cy + rad * math.sin(a)))
    return pts

def make(bg, fg, name, rounded=False):
    ss = 4
    S = SIZE * ss
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if rounded:
        d.rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), fill=bg)
    else:
        d.rectangle([0, 0, S, S], fill=bg)
    # outer radius 0.36 of the tile -> tips well inside the 0.5 circle crop
    d.polygon(star(S / 2, S / 2, S * 0.36, S * 0.36 * INNER), fill=fg)
    img.resize((SIZE, SIZE), Image.LANCZOS).save(os.path.join(PNG, name))

BLACK = (12, 12, 14, 255)
WHITE = (255, 255, 255, 255)

make(BLACK, WHITE, "pfp-dark.png")            # white star on black (recommended)
make(WHITE, BLACK, "pfp-light.png")           # black star on white
make(BLACK, WHITE, "pfp-dark-rounded.png", rounded=True)   # for square avatars (Discord servers, etc.)
print("done ->", PNG)
