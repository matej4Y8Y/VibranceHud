"""PlexusX Discord invite splash — 1920x1080.

Different job to the server banner. Discord renders this full-width behind the invite
card, and that card sits DEAD CENTRE (server icon, name, member count, Accept button).
So the centre has to stay quiet and the brand goes left.

It is also the one surface seen by people who do not have the app yet, so it carries
the tagline. The plexus runs muted on the left and vivid on the right - a quiet nod to
what the product actually does, without turning the page into a billboard.
"""
import math, random
from PIL import Image, ImageDraw, ImageFilter, ImageFont

W, H = 1920, 1080
BG = (10, 10, 12)
VIOLET = (167, 139, 250)
MAGENTA = (232, 96, 214)
LINE = (150, 130, 240)
MUTED = (86, 84, 104)

TAGLINE = "Sharper colors. Smoother games."

random.seed(11)


def lerp(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def font(size, bold=False):
    for name in (("seguisb.ttf", "segoeuib.ttf") if bold else ("segoeui.ttf",)):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


img = Image.new("RGB", (W, H), BG)

# ---- ambient bloom, weighted left where the brand sits ----
glow = Image.new("RGB", (W, H), BG)
gd = ImageDraw.Draw(glow)
for cx, cy, rad, col, strength in [
    (int(W * 0.26), int(H * 0.46), 620, (62, 36, 104), 0.62),
    (int(W * 0.86), int(H * 0.24), 480, (78, 30, 92), 0.42),
    (int(W * 0.72), int(H * 0.88), 420, (40, 30, 88), 0.35),
]:
    for r in range(rad, 0, -14):
        t = 1 - r / rad
        gd.ellipse([cx - r, cy - r * 0.72, cx + r, cy + r * 0.72],
                   fill=lerp(BG, col, t * strength))
glow = glow.filter(ImageFilter.GaussianBlur(110))
img = Image.blend(img, glow, 0.92)

# ---- plexus ----
nodes = []
for _ in range(150):
    x = random.uniform(-60, W + 60)
    y = random.uniform(-60, H + 60)
    # Push density away from the centre so Discord's invite card lands on calm pixels.
    cd = math.hypot((x - W / 2) / (W * 0.5), (y - H / 2) / (H * 0.5))
    weight = min(1.0, 0.22 + cd * 1.05)
    nodes.append((x, y, weight))

layer = Image.new("RGB", (W, H), (0, 0, 0))
ld = ImageDraw.Draw(layer)

for i, (x1, y1, w1) in enumerate(nodes):
    for x2, y2, w2 in nodes[i + 1:]:
        dist = math.hypot(x2 - x1, y2 - y1)
        if dist > 210:
            continue
        fade = (1 - dist / 210) * min(w1, w2)
        if fade <= 0.03:
            continue
        # Colour drifts across the frame: muted on the left, vivid on the right.
        mx = ((x1 + x2) / 2) / W
        col = lerp(MUTED, lerp(VIOLET, MAGENTA, mx), 0.25 + mx * 0.75)
        ld.line([x1, y1, x2, y2], fill=lerp((0, 0, 0), col, fade * 0.5), width=1)

for x, y, w in nodes:
    r = 1.8 + w * 3.0
    col = lerp(MUTED, lerp(VIOLET, MAGENTA, x / W), 0.3 + (x / W) * 0.7)
    ld.ellipse([x - r, y - r, x + r, y + r], fill=lerp((0, 0, 0), col, min(1, w * 1.15)))

base_px, layer_px = img.load(), layer.load()
for y in range(H):
    for x in range(W):
        b, l = base_px[x, y], layer_px[x, y]
        base_px[x, y] = tuple(255 - (255 - b[i]) * (255 - l[i]) // 255 for i in range(3))

# ---- centre-safe vignette: darken exactly where the invite card lands ----
mask = Image.new("L", (W, H), 0)
md = ImageDraw.Draw(mask)
ccx, ccy = W // 2, int(H * 0.50)
for r in range(560, 0, -8):
    t = 1 - r / 560
    md.ellipse([ccx - r, ccy - r * 0.86, ccx + r, ccy + r * 0.86], fill=int(190 * (t ** 1.6)))
mask = mask.filter(ImageFilter.GaussianBlur(90))
img = Image.composite(Image.new("RGB", (W, H), BG), img, mask)

# ---- brand block, left third ----
logo = Image.open(
    r"C:\Users\MR.UltraSexymale\Downloads\VibranceHud\brand\png\logo-horizontal-white.png"
).convert("RGBA")
target_w = int(W * 0.26)
logo = logo.resize((target_w, int(logo.height * target_w / logo.width)), Image.LANCZOS)

lx = int(W * 0.085)
ly = int(H * 0.42) - logo.height // 2
img.paste(logo, (lx, ly), logo)

d = ImageDraw.Draw(img)

# Accent rule
rule_w = int(target_w * 0.28)
ry = ly + logo.height + 34
for i in range(rule_w):
    d.line([lx + i, ry, lx + i, ry + 4], fill=lerp(VIOLET, MAGENTA, i / rule_w))

# Tagline
tf = font(40)
d.text((lx, ry + 40), TAGLINE, font=tf, fill=(196, 194, 210))

img.save(
    r"C:\Users\MR389C~1.ULT\AppData\Local\Temp\claude\C--Users-MR-UltraSexymale-Downloads-VibranceHud\b503681b-bb23-475d-966d-b9c651716454\scratchpad\plexusx-invite-splash.png",
    "PNG")
print(f"wrote invite splash {img.size[0]}x{img.size[1]}")
