# Crosshair tab

Written 2026-08-05. First of a tab-by-tab review of the whole app.

## What the tab is for

One job: give somebody a crosshair they can aim with, in games that do not offer one or offer
a bad one. Everything on the page should serve picking a crosshair and then nudging it.

## What is there now

An on/off switch and live preview, eight shape presets, size / thickness / gap sliders with
decimal precision, six colour swatches, an outline toggle, and named saved crosshairs.

## The blocker

`CrosshairShape` is an enum of four mutually-exclusive values — Cross, Dot, Circle, T. That is
the whole shape model, and it is why the presets came out as eight shapes rather than a
gallery.

Real crosshairs are combinations: a cross *with* a centre dot, a circle *with* a dot, a plus
inside an outline box, a cross with the top arm removed. None of those are expressible as one
of four options, so no catalogue built on this enum can be more than a handful of entries.

**Everything else in this spec depends on replacing that model first.**

## Design

### 1. A composable crosshair

Replace the single enum with independent parts:

```
Arms        top / bottom / left / right, each on or off
ArmLength   tenths of a pixel   (existing)
Thickness   tenths of a pixel   (existing)
Gap         tenths of a pixel   (existing)
CentreDot   on/off + its own size
Circle      on/off + its own radius
Outline     on/off              (existing)
Colour      ARGB                (existing)
Opacity     0-100               (new)
```

Four independent arms give cross, T (no top), inverted-T, side-only and single-arm shapes from
one mechanism. Adding the dot and circle as independent parts rather than alternatives gives
every combination in between.

**Migration.** `CrosshairShape` stays on the config as a legacy field and maps forward on
first read, exactly as `Size` → `SizeTenths` already does:

| Legacy shape | Arms | Dot | Circle |
|---|---|---|---|
| Cross | all four | as saved | off |
| T | bottom, left, right | as saved | off |
| Dot | none | on | off |
| Circle | none | as saved | on |

Nobody's saved crosshair changes shape.

### 2. The gallery

Thirty crosshairs, in a scrollable grid. Each cell draws **its own live preview** using the
real render path, so a cell can never show something the overlay would not.

Clicking one applies it and leaves the user's colour alone — same rule the current presets
follow, and for the same reason: colour is the most personal setting on the page.

A **heart** on each cell marks a favourite. Favourites sort to the top. Stored in settings
alongside the saved crosshairs.

**On the thirty.** These are built from the crosshair families competitive players actually
use — the same handful of shapes that recur across Counter-Strike, Valorant and Apex, at the
sizes and gaps people run them. They are **not** a copy of any commercial overlay's preset
list: that product's usage data is not something this project can see, and claiming to have
replicated their top thirty would be inventing a fact. Descriptive names, as with the existing
presets.

The families, roughly five variants each: thin cross, medium cross, thick cross, cross with
centre dot, dot only, T-shape, circle, circle with dot, and small "pixel" crosshairs.

### 3. Colour and opacity

Six fixed swatches become the six swatches plus a **custom colour** entry, and an **opacity**
slider. A large share of real crosshairs are semi-transparent, and the current model has no way
to express that at all.

Opacity multiplies the colour's alpha at render time rather than being baked into `ColourArgb`,
so changing colour does not silently reset it.

### 4. Page layout after the change

```
CROSSHAIR      on/off, live preview, status
GALLERY        30 previews, favourites first
FINE TUNE      size, thickness, gap, arms, dot, circle
COLOUR         swatches, custom, opacity, outline
SAVED          named crosshairs
```

The gallery goes directly under the preview because picking is the common path; the sliders
are for the minority who then adjust.

## Out of scope

**Crosshair share codes.** Declined for now.

**Per-game crosshairs.** A real want — a different crosshair for Rust and CS2 — but it needs
the game-selection plumbing, and the shape model has to be right first. Its own pass.

**Importing crosshairs from other tools.** Every one uses its own format; supporting them means
tracking formats this project does not control.

## Testing

The shape model, the legacy migration and every gallery entry are pure data and are unit
tested: each of the thirty must render something visible, stay inside the slider ranges, and
survive a round trip through save and load. `ControlPaintTests` already renders every control
in every state, and the gallery cells go through it — a preview that throws while painting
would otherwise take the app down the way the focus ring did.

The existing 1057 tests stay green.
