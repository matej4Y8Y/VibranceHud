"""
Generates the PlexusX brand asset kit from the 12-point starburst mark.
Run:  python brand/make_brand.py
Produces SVG masters, transparent PNGs (black + white), lockups, and a
multi-resolution Windows .ico app icon, all under brand/.
"""
import math, os, struct, io
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
PNG = os.path.join(HERE, "png")
os.makedirs(PNG, exist_ok=True)

BLACK = (17, 17, 20, 255)
WHITE = (255, 255, 255, 255)
POINTS = 12
INNER_RATIO = 0.44          # spiky look
FONT_BOLD = r"C:\Windows\Fonts\arialbd.ttf"
FONT_REG = r"C:\Windows\Fonts\arial.ttf"

def star_points(cx, cy, R, r, n=POINTS, rot_deg=-90):
    pts = []
    for i in range(2 * n):
        ang = math.radians(rot_deg + i * (360.0 / (2 * n)))
        rad = R if i % 2 == 0 else r
        pts.append((cx + rad * math.cos(ang), cy + rad * math.sin(ang)))
    return pts

# ---------- PNG rendering (supersampled for clean edges) ----------
def render_icon(size, fill, bg=None, bg_round=None, pad_ratio=0.10, ss=4):
    S = size * ss
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if bg is not None:
        if bg_round is None:
            d.rectangle([0, 0, S, S], fill=bg)
        else:
            d.rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * bg_round), fill=bg)
    c = S / 2
    R = S * (0.5 - pad_ratio)
    d.polygon(star_points(c, c, R, R * INNER_RATIO), fill=fill)
    return img.resize((size, size), Image.LANCZOS)

def save(img, name):
    p = os.path.join(PNG, name)
    img.save(p)
    return p

# Transparent star, black + white, multiple sizes
for s in (16, 32, 48, 64, 128, 256, 512, 1024):
    save(render_icon(s, BLACK), f"icon-black-{s}.png")
for s in (256, 512, 1024):
    save(render_icon(s, WHITE), f"icon-white-{s}.png")

# App icon source: black star on a white rounded square (pops on dark taskbars)
appicon = render_icon(1024, BLACK, bg=WHITE, bg_round=0.22, pad_ratio=0.20)
save(appicon, "appicon-1024.png")

# Multi-resolution .ico
ico_path = os.path.join(HERE, "PlexusX.ico")
appicon.save(ico_path, format="ICO",
             sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

# ---------- Lockups ----------
def horizontal(fill, name, scale=1024):
    icon_h = int(scale * 0.42)
    gap = int(scale * 0.05)
    font = ImageFont.truetype(FONT_BOLD, int(icon_h * 0.92))
    tmp = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    tb = tmp.textbbox((0, 0), "PlexusX", font=font)
    tw, th = tb[2] - tb[0], tb[3] - tb[1]
    W = icon_h + gap + tw + int(scale * 0.04)
    H = icon_h
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    icon = render_icon(icon_h, fill, pad_ratio=0.02)
    img.alpha_composite(icon, (0, 0))
    d = ImageDraw.Draw(img)
    d.text((icon_h + gap, (H - th) // 2 - tb[1]), "PlexusX", font=font, fill=fill)
    img.save(os.path.join(PNG, name))

def vertical(fill, name, scale=1024):
    icon_h = int(scale * 0.42)
    font = ImageFont.truetype(FONT_BOLD, int(scale * 0.16))
    tfont = ImageFont.truetype(FONT_REG if os.path.exists(FONT_REG) else FONT_BOLD, int(scale * 0.075))
    img = Image.new("RGBA", (scale, int(scale * 0.82)), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    icon = render_icon(icon_h, fill, pad_ratio=0.02)
    img.alpha_composite(icon, ((scale - icon_h) // 2, 0))
    def centered(text, font, y):
        b = d.textbbox((0, 0), text, font=font)
        d.text(((scale - (b[2] - b[0])) // 2 - b[0], y), text, font=font, fill=fill)
    centered("PlexusX", font, int(icon_h * 1.05))
    centered("One For All", tfont, int(icon_h * 1.05 + scale * 0.18))
    img.save(os.path.join(PNG, name))

horizontal(BLACK, "logo-horizontal-black.png")
horizontal(WHITE, "logo-horizontal-white.png")
vertical(BLACK, "logo-vertical-black.png")
vertical(WHITE, "logo-vertical-white.png")

# ---------- SVG masters (vector, infinitely scalable) ----------
def star_svg_points(R=200, r=None, c=256):
    r = r or R * INNER_RATIO
    return " ".join(f"{x:.2f},{y:.2f}" for x, y in star_points(c, c, R, r))

def write_svg(name, body, w=512, h=512):
    with open(os.path.join(HERE, name), "w", encoding="utf-8") as f:
        f.write(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}">\n{body}\n</svg>\n')

pts = star_svg_points()
write_svg("icon.svg", f'  <polygon points="{pts}" fill="#111114"/>')
write_svg("icon-white.svg", f'  <polygon points="{pts}" fill="#ffffff"/>')

def lockup_svg(name, color):
    icon = star_svg_points(R=150, c=170)  # icon centered at x=170, y=256
    body = (f'  <polygon points="{icon}" transform="translate(0,86)" fill="{color}"/>\n'
            f'  <text x="360" y="360" font-family="Arial, Helvetica, sans-serif" '
            f'font-weight="700" font-size="210" fill="{color}">PlexusX</text>')
    write_svg(name, body, w=1180, h=512)

lockup_svg("logo-horizontal.svg", "#111114")
lockup_svg("logo-horizontal-white.svg", "#ffffff")

print("done ->", HERE)
print("ico  ->", ico_path)
