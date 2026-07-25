# Custom Image Theme — Design

## Goal

Let the user pick an image to sit behind the UI, and have the app's accent colours
derive from that image — while the floating plexus particle field stays on top exactly
as it does today.

## Scope

**What the image changes:** precisely the five colours that already differ between the
existing themes — `Accent`, `AccentDim`, `PlexusNodeA`, `PlexusNodeB`, `PlexusLine`.

**What it does not change:** `Background`, `Surface`, `SurfaceHover`, `Border`,
`GlassFill`, `GlassEdge`, `Text`, `TextDim`. The matte-black base is shared by every
dark theme today and stays shared. This is what guarantees text contrast survives
whatever image the user picks — the feature cannot produce an unreadable UI.

## The "dominant colour" rule

The user asked for the dominant colour. Taken literally that fails: the most frequent
pixel in a night-time Rust screenshot is near-black, and in a snow clip it's near-white.
Either would produce an accent invisible against `#0A0A0C`.

So the rule is **the most frequent colour that can function as an accent**:

1. Downsample to ~64×64 (fast, and averages out compression noise).
2. Bucket pixels by hue/saturation/value.
3. Discard buckets that cannot serve as an accent: value < 0.25 (too dark) or
   saturation < 0.20 (too grey).
4. The most frequent surviving bucket wins.
5. If it still under-contrasts against the matte-black background, lift its value until
   it passes.
6. If **no** bucket survives (a genuinely greyscale or black image), fall back to the
   default accent rather than emitting an invisible one.

Steps 3 and 6 are the difference between "dominant colour" working and producing a
theme the user cannot see.

## Deriving the other four

The accent alone is extracted; the rest follow the shape the existing palettes already
use (Violet is a violet accent with violet + magenta nodes; Emerald is two greens):

| Derived | Rule |
|---|---|
| `AccentDim` | accent, ~40% darker |
| `PlexusNodeA` | the accent |
| `PlexusNodeB` | accent hue-shifted ~+40° |
| `PlexusLine` | blend of node A and node B |

## Components

### `Theming/ImagePalette.cs` (pure, unit-tested)
Takes downsampled pixel data, returns the extracted accent plus the four derived
colours, and the suggested auto-dim level. No file I/O, no GDI, no window — so every
rule above is testable directly with synthetic pixel arrays.

### `Theming/CustomTheme.cs`
Builds a `ThemePalette` from extracted colours, reusing the same matte-black base
`ThemeCatalog.Dark(...)` already uses. Slots into the existing catalog as a "Custom"
entry so the Settings picker and `Theme.Apply` need no special-casing.

### `GlowPage` (background rendering)
Paint order today is: solid fill → particle field → content. The fill becomes the image
when one is set; particles and content are untouched.

**Performance:** the particle field repaints continuously, so scaling a 4K wallpaper per
frame would stutter. A **pre-scaled, pre-dimmed** bitmap is cached and rebuilt only on
resize or dim change. Per frame this is then a single blit — no more expensive than the
solid fill it replaces.

### Auto-dim
Mean luminance of the downsampled image maps to a starting dim level: bright images get
dimmed harder. A Settings slider lets the user taste-adjust from there, persisted.

### Storage
The chosen image is **copied** into `%AppData%\PlexusX\` rather than referenced where it
sits, so moving or deleting the original cannot break the theme. Derived colours are
cached in `AppSettings` so startup doesn't re-scan the image.

### Settings UI
A "Custom" swatch joins the existing theme swatches, plus a "Choose image…" button and
the dim slider. Selecting another theme leaves the custom image on disk so switching
back is instant.

## Error handling

| Case | Behaviour |
|---|---|
| File unreadable / not an image | Keep the current theme, show a status line. Never crash. |
| Image deleted after being set | Fall back to the default theme on next launch. |
| Greyscale or near-black image | Accent falls back to the default; background image still applies. |
| Very large image | Downsampled for extraction; cached scaled copy for painting. |

## Testing

Written before implementation:

- Dominant colour is found in a synthetic multi-colour image.
- Too-dark and too-desaturated buckets are excluded from the vote.
- A greyscale image falls back to the default accent.
- Contrast lifting raises an accent that would be invisible on matte black.
- The four derived colours are distinct from each other and from the accent.
- Auto-dim returns a higher value for a bright image than a dark one.
- Settings round-trip preserves image path, dim level and cached colours.

## Explicitly out of scope

- Animated / video backgrounds.
- Per-page images.
- Recolouring surfaces or text (rejected: cannot guarantee contrast).
- Letting the image change the light theme.
