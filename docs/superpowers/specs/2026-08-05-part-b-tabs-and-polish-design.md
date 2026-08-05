# PlexusX 1.0 part B — tabs, crosshair, monitor, polish

Written 2026-08-05. Follows `2026-08-05-plexusx-systems-design.md` (part A, capability and
truth systems, now built).

Four pieces, in order: fold the Profile Editor into Game, expand Crosshair, expand Monitor,
then sweep the visual defects. Each ships independently.

## 1. Profile Editor folds into Game

### The problem

The Profile Editor is the only page that duplicates another page's controls. To give a game
its own look you tune Display, then tune the same four values again in the editor. Nobody does
that twice, which is why the feature is unused — recorded in
`2026-08-03-road-to-1.0-structure.md` as the highest-value cleanup remaining.

Relocating it to Game without changing it would move the duplication rather than remove it.

### The design

The Game page gains a **PROFILE** section with no sliders of its own:

- **"Use my current Display look for Rust"** — one button. Whatever is on screen becomes that
  game's profile, applied when it launches and restored when it closes.
- Underneath, one line of what is stored: *"Saturation 140, contrast 106, warm 20 — saved
  today."* Plus **Forget**.

The Profile Editor page and its nav row are deleted. `ProfileApplyEngine` and
`ProfileEngineCoordinator` are untouched — the auto-apply machinery already works and this
only changes how a profile is created.

Where a game has no profile the section states that plainly and the button is the only action.

**Scope limit.** Auto-apply keys off what actually launches, never off the game chooser.
Picking a game from the nav must not change what is on the user's monitor. That rule is from
the road-to-1.0 doc and does not change here.

## 2. Crosshair

### 2a. Sub-integer precision

`Size`, `Thickness` and `Gap` are `int`, so the sliders step 1 → 2 → 3. At small sizes that is
the difference between a usable crosshair and an unusable one.

Each gains a tenths-based property, with the old whole-number one kept for migration:

```
Size       (int, legacy)  →  SizeTenths       (int, 1 unit = 0.1)
Thickness  (int, legacy)  →  ThicknessTenths
Gap        (int, legacy)  →  GapTenths
```

Resolved properties migrate a saved config on first read (`SizeTenths ?? Size * 10`), exactly
as `ResolvedVibrance` and `ResolvedTemperature` already do. Nobody's saved crosshair changes
shape on upgrade.

Sliders stay integer internally — they operate on tenths — and format as `1.2`. Rendering
divides by ten at the point of drawing.

### 2b. Presets

A chip row above the sliders, following the Display page's scene presets. Every preset is
**white**, so colour stays the user's own choice and picking a shape never overwrites it.

| Preset | Shape |
|---|---|
| Classic Cross | four lines, medium gap |
| Small Cross | short lines, tight gap |
| Wide Cross | long lines, wide gap |
| T-Shape | no top line |
| Dot Only | centre dot, no lines |
| Cross + Dot | classic plus centre dot |
| Circle | ring, no lines |
| Open Cross | long lines, large gap |

**Named descriptively on purpose.** These shapes are universal across every shooter, but the
preset *names* used by commercial crosshair overlays are that product's branding. Descriptive
names give users the same result without borrowing somebody else's labels.

Applying a preset sets shape, size, thickness, gap and dot — and deliberately leaves colour
and outline alone.

## 3. Monitor

The page currently does resolution now, and resolution on game launch. Three additions:

**Refresh rate.** Changing resolution but not refresh rate misses the setting this audience
actually cares about. Offered as chips of the rates the selected mode genuinely supports,
enumerated from the display rather than assumed.

**Which monitor.** Everything on the page currently assumes one screen. A selector appears
only when there is more than one, so single-monitor users see no extra control.

**HDR on/off.** Part A established that HDR is the most likely reason a user's advanced colour
does nothing, and the app now detects it. A switch here turns that dead end into a fix: the
Display page can say "HDR is on" and Monitor is where it gets turned off.

HDR is toggled through the same CCD API used to detect it. If the call fails the switch
reports it rather than silently flipping back — the same rule as everywhere else.

**Not included:** GPU scaling mode and colour depth. Both are driver-level rather than Windows
display settings, and would need per-vendor APIs the app does not have.

## 4. Visual defect sweep

Last, because the three items above change layout and fixing polish first would mean doing it
twice.

- **Pages do not reflow to a wide window.** Now that the window resizes, cards stay at a fixed
  ~620px and leave a dead zone. Display is converted; every other page is not.
- **Focus rings.** Three controls out of roughly twenty draw one.
- **Keyboard navigation.** No Escape, no Ctrl+Tab, no tab order through the shell.
- **Tooltips.** Two in the whole app.
- **Accessible names and roles.** None.
- Whatever `DpiPageLayoutTests` reports once its coverage extends past the Display page.

The DPI harness generalises to each page as it is converted, so overlaps and clipped text are
caught by tests rather than by eye.

## Order and risk

1. **Profile Editor fold** — deletes a page and a nav row; touches profile creation but not
   auto-apply.
2. **Crosshair** — self-contained. The migration is the only risk and it is unit-testable.
3. **Monitor** — HDR toggle is the one item that changes a system display setting, so it
   confirms before acting and reports failure.
4. **Polish sweep** — mechanical, wide, low risk, done last.

## Testing

Crosshair migration, preset application and tenths formatting are pure logic and are unit
tested. Profile save/forget is tested against a fake store. Refresh-rate and HDR calls are thin
Win32 wrappers verified by hand.

The existing 1007 tests stay green.
