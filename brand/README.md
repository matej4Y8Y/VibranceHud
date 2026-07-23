# PlexusX Brand Kit

The mark is a 12-point starburst — deliberately echoing the plexus/particle background
in the app, so the logo and the product mean the same thing. Pure black and white, which
matches both app themes.

## What to use when

| File | Use for |
|---|---|
| `icon.svg` / `icon-white.svg` | The star on its own. **SVG is the master** — scales to any size, recolors by editing one hex value. |
| `logo-horizontal.svg` / `-white.svg` | Star + "PlexusX" side by side. Website header, README, Discord. |
| `png/logo-horizontal-black.png` / `-white.png` | Same lockup as a transparent PNG when SVG isn't accepted. |
| `png/logo-vertical-black.png` / `-white.png` | Stacked star + "PlexusX" + "One For All". Posters, splash, store listing. |
| `png/icon-black-*.png` (16–1024) | Transparent star at fixed sizes. Favicons, docs, small UI. |
| `png/icon-white-*.png` | Same for dark backgrounds. |
| `PlexusX.ico` | **The Windows app icon** — multi-resolution (16–256). Used by the app exe, the window/taskbar, the tray, and the installer. |

**Black vs white:** use the black mark on light backgrounds, the white mark on dark ones.
The app icon is intentionally a black star on a white rounded tile so it stays visible on
Windows' dark taskbar (a plain black star would disappear there).

## Colors & type

- Ink: `#111114` (near-black, matches the app's `Theme.Text` family)
- Paper: `#FFFFFF`
- Wordmark: bold grotesque sans (rendered here with Arial Bold; swap in your licensed
  brand font when you have one — edit `FONT_BOLD` in `make_brand.py`)

## Regenerating

Everything here is generated from one script, so the kit stays consistent:

```
python brand/make_brand.py
```

Edit `POINTS` (star point count), `INNER_RATIO` (spikiness), or the fonts at the top of
that script and re-run to regenerate every asset, including the `.ico`.
