# Vibrance HUD — Tier 2: System-Wide Oversaturation (up to 200%)

**Date:** 2026-07-23
**Status:** Approved, in implementation

## Goal

Let the vibrance slider go past the NVIDIA driver's hard 100% cap, up to 200%,
applied system-wide (whole desktop + borderless-windowed games). Tier-1 behavior
(0–100%) is preserved exactly.

## Mechanism

The NVAPI Digital Vibrance control is hard-capped at 0–100 by the driver; there is
no flag to exceed it. To go higher we do the saturation math ourselves via the
Windows Magnification API:

- `Magnification.dll` → `MagInitialize`, `MagUninitialize`,
  `MagSetFullscreenColorEffect(ref MAGCOLOREFFECT)`.
- `MAGCOLOREFFECT` is a 5×5 float matrix applied to every pixel on screen.
- A luminance-preserving saturation matrix (Rec. 709 luma weights
  0.2126 / 0.7152 / 0.0722) with factor `s` produces oversaturation for `s > 1`.
- No elevation, signing, or uiAccess required (same approach as NegativeScreen).
- Windows removes the effect automatically when the owning process exits; we also
  clear it explicitly on graceful exit.

## Components

| Unit | Role | Status |
|------|------|--------|
| `VibranceController` | Pure NVAPI wrapper, 0–100 | unchanged |
| `SaturationOverlay` | Pure Magnification.dll wrapper: `SetSaturation(float)`, `Clear()`, init/uninit | new |
| `VibranceEngine` | Coordinator. `Max=200`, `SetLevel(int)`, `CurrentLevel`, `DefaultLevel`, `Reset()`. Owns the 100 threshold | new |
| `VibrancePopup` | Slider 0–200 with tick marker at 100; talks only to `VibranceEngine` | modified |
| `TrayApplicationContext` | Wires engine; clears overlay + uninits on exit | modified |

## Core logic — `VibranceEngine.SetLevel(n)`

- `n <= 100` → `controller.SetLevel(n)`, `overlay.Clear()`. Identical to today.
- `n > 100`  → `controller.SetLevel(100)` (driver pinned at max),
  `overlay.SetSaturation(n / 100f)` (150 → 1.5×, 200 → 2.0× extra saturation).

The popup never knows about the threshold — it moves a 0–200 slider and calls
`SetLevel`.

## Saturation matrix (row-vector convention: newColor = oldColor · M)

For luma weights (lr, lg, lb) and factor s, output-channel columns:

```
R' col: lr*(1-s)+s,  lr*(1-s),     lr*(1-s)
G' col: lg*(1-s),    lg*(1-s)+s,   lg*(1-s)
B' col: lb*(1-s),    lb*(1-s),     lb*(1-s)+s
A' , w : identity
```

`s = 1.0` yields identity (no change); `SetSaturation(1.0)` is equivalent to `Clear()`.

## Error handling

- If `MagInitialize` fails, the app still runs but the slider caps at 100
  (tier-1-only fallback) instead of crashing.
- `ExitThreadCore` and "Reset to default" both `Clear()` the matrix so the screen
  is never left oversaturated.

## Testing

- P/Invoke plumbing (NVAPI, Magnification) needs real hardware/OS → manual
  verification on the user's machine, same as tier 1.
- Pure logic gets unit tests (xUnit, new `VibranceHud.Tests` project):
  - Saturation matrix values for known factors (s=1 → identity; s=2 → expected).
  - Threshold/mapping: `SetLevel(80)` → driver 80, overlay cleared;
    `SetLevel(150)` → driver 100, factor 1.5; clamping at 0 and 200.
  - `VibranceController` / `SaturationOverlay` are abstracted behind interfaces so
    the engine can be tested with fakes (no GPU needed).

## Known limitations (document in README)

- No effect on exclusive-fullscreen games or DRM-protected video (Netflix etc.).
  Borderless-windowed games work.
- Conflicts with Windows Night Light / Color Filters (same pipeline) — recommend
  disabling Windows Color Filters.
- Software full-frame color pass; negligible on modern GPUs.
```
