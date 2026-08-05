"""PlexusX Discord server banner.

Matches the app exactly: matte black base (10,10,12), violet accent (167,139,250),
magenta second node colour (232,96,214), plexus line (150,130,240) - the same values
ThemeCatalog.cs hands to the particle field.

Discord shows the server banner at the TOP OF THE CHANNEL LIST, roughly 240px wide,
with the server name overlaid across the bottom on a dark scrim. So: nothing small,
nothing important in the bottom third, and the mark has to survive being shrunk to
a quarter of its size.
"""
import math, random
from PIL import Image, ImageDraw, ImageFilter

W, H = 960, 540
BG = (10, 10, 12)
VIOLET = (167, 139, 250)
MAGENTA = (232, 96, 214)
LINE = (150, 130, 240)

random.seed(7)  # deterministic, so re-running gives the same banner


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def build(variant, logo_path, out_path):
    img = Image.new("RGB", (W, H), BG)

    # ---- violet bloom behind the mark, drawn big then blurred ----
    glow = Image.new("RGB", (W, H), BG)
    gd = ImageDraw.Draw(glow)
    cx, cy = (W // 2, int(H * 0.42)) if variant != "left" else (int(W * 0.30), int(H * 0.44))
    for r in range(360, 0, -12):
        t = 1 - r / 360
        gd.ellipse([cx - r, cy - r * 0.62, cx + r, cy + r * 0.62],
                   fill=lerp(BG, (58, 34, 96), t * 0.55))
    glow = glow.filter(ImageFilter.GaussianBlur(70))
    img = Image.blend(img, glow, 0.9)

    # ---- the plexus itself ----
    nodes = []
    count = 74 if variant != "dense" else 110
    for _ in range(count):
        x = random.uniform(-40, W + 40)
        y = random.uniform(-40, H + 40)
        # Bias size and brightness toward the centre so the edges stay quiet.
        d = math.hypot((x - cx) / W, (y - cy) / H)
        nodes.append((x, y, max(0.15, 1 - d * 1.9)))

    layer = Image.new("RGB", (W, H), (0, 0, 0))
    ld = ImageDraw.Draw(layer)

    # Lines first, so nodes sit on top of them.
    for i, (x1, y1, w1) in enumerate(nodes):
        for x2, y2, w2 in nodes[i + 1:]:
            dist = math.hypot(x2 - x1, y2 - y1)
            if dist > 165:
                continue
            fade = (1 - dist / 165) * min(w1, w2)
            if fade <= 0.02:
                continue
            ld.line([x1, y1, x2, y2], fill=lerp((0, 0, 0), LINE, fade * 0.55), width=1)

    for x, y, w in nodes:
        r = 1.6 + w * 2.6
        col = lerp(VIOLET, MAGENTA, min(1, (x / W) * 0.9))
        ld.ellipse([x - r, y - r, x + r, y + r], fill=lerp((0, 0, 0), col, min(1, w * 1.25)))

    # Screen-blend the plexus over the background so it glows instead of flattening it.
    img = Image.eval(
        Image.merge("RGB", [
            Image.eval(Image.merge("L", [img.getchannel(c)]), lambda v: v) for c in "RGB"
        ]), lambda v: v)
    base_px, layer_px = img.load(), layer.load()
    for y in range(H):
        for x in range(W):
            b, l = base_px[x, y], layer_px[x, y]
            base_px[x, y] = tuple(255 - (255 - b[i]) * (255 - l[i]) // 255 for i in range(3))

    # ---- bottom scrim: Discord paints the server name across here ----
    scrim = Image.new("L", (W, H), 0)
    sd = ImageDraw.Draw(scrim)
    for y in range(int(H * 0.55), H):
        t = (y - H * 0.55) / (H * 0.45)
        sd.line([0, y, W, y], fill=int(215 * (t ** 1.5)))
    img = Image.composite(Image.new("RGB", (W, H), BG), img, scrim)

    # ---- the mark ----
    logo = Image.open(logo_path).convert("RGBA")
    target_w = int(W * (0.46 if variant != "left" else 0.40))
    logo = logo.resize((target_w, int(logo.height * target_w / logo.width)), Image.LANCZOS)

    lx = (W - logo.width) // 2 if variant != "left" else int(W * 0.09)
    ly = int(H * 0.38) - logo.height // 2
    img.paste(logo, (lx, ly), logo)

    # ---- accent rule under the mark: reads as intent even at 240px wide ----
    rule_w = int(target_w * 0.30)
    rx = (W - rule_w) // 2 if variant != "left" else int(W * 0.09)
    ry = ly + logo.height + 26
    d = ImageDraw.Draw(img)
    for i in range(rule_w):
        d.line([rx + i, ry, rx + i, ry + 3], fill=lerp(VIOLET, MAGENTA, i / rule_w))

    img.save(out_path, "PNG")
    print(f"wrote {out_path}  {img.size[0]}x{img.size[1]}")


LOGO = r"C:\Users\MR.UltraSexymale\Downloads\VibranceHud\brand\png\logo-horizontal-white.png"
OUT = r"C:\Users\MR389C~1.ULT\AppData\Local\Temp\claude\C--Users-MR-UltraSexymale-Downloads-VibranceHud\b503681b-bb23-475d-966d-b9c651716454\scratchpad"

build("centred", LOGO, OUT + r"\plexusx-discord-banner-centred.png")
build("left", LOGO, OUT + r"\plexusx-discord-banner-left.png")
build("dense", LOGO, OUT + r"\plexusx-discord-banner-dense.png")
