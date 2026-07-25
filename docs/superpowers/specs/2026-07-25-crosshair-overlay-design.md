# Crosshair Overlay — Design

## Why this feature

Crosshair X has 3M+ users as a paid product, and its headline feature is per-game
profiles. PlexusX already has game detection, per-game settings, and overlay experience,
so most of the surrounding work is done.

Critically, this is the rare high-demand feature that *strengthens* the "no injection,
EaC safe" position rather than undermining it: Facepunch explicitly permits third-party
crosshairs, and the overlay never touches the game process.

## How it draws

A layered, click-through, topmost window:

```
WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
```

- `WS_EX_TRANSPARENT` — clicks pass straight through to the game.
- `WS_EX_TOOLWINDOW` — stays out of Alt-Tab.
- `WS_EX_NOACTIVATE` — never steals focus.

No injection, no hooks, no memory access. Same category as the Discord overlay.

## Components

### `Crosshair/CrosshairConfig.cs`
Plain data: `Name`, `Shape` (Dot / Cross / Circle / T), `Colour`, `Size`, `Thickness`,
`Gap`, `Outline`, `CentreDot`.

### `Crosshair/CrosshairGeometry.cs`
Pure maths — given a config, returns the rectangles to draw. No GDI and no window, so
every rule is unit-testable on its own. This is where the real logic lives; the window is
deliberately dumb.

### `Crosshair/CrosshairWindow.cs`
The Win32 overlay. Positions on the active monitor's centre and repaints when the config
changes. Thin by design.

### `Crosshair/CrosshairService.cs`
Owns the window; handles enable/disable and the global toggle hotkey.

## Fullscreen handling

A drawn overlay cannot appear over a game in true exclusive fullscreen — a Windows
limitation every crosshair app shares.

`SHQueryUserNotificationState` returns `QUNS_RUNNING_D3D_FULL_SCREEN` when an
exclusive-fullscreen D3D app is running: one call, no polling of window rectangles.

When that fires and the detected game is Rust, the page offers a one-click **"Switch Rust
to borderless"** that writes the setting through the existing Rust config writer. No
competitor can do this, because none of them already own the game's config file.

## Positioning

The crosshair centres on the monitor, not on the game window. Confirmed acceptable: the
target audience does not play windowed. Per-window tracking is explicitly out of scope.

## UI — new "Crosshair" nav item

- Live preview over a neutral checkerboard.
- Four shape presets: dot, cross, circle, T.
- Size, thickness, gap sliders; colour picker.
- Saved-config list: pick, save, rename, delete.
- Enable toggle plus a global hotkey to flick it on/off mid-game.

Configs are stored in `AppSettings` as a list plus the active config's name. Switching is
manual — no game binding, so there is no auto-switch logic to reason about.

## Testing

Geometry is tested before any window exists:

- The gap opens symmetrically around the centre.
- Opposing arms are equal length.
- Thickness and size scale as expected.
- The centre dot appears only when enabled.
- Odd and even sizes both stay truly centred — the off-by-one that makes a crosshair look
  subtly wrong.
- Config round-trips through settings unchanged.

## Out of scope for v1

- Per-game auto-switching (chosen: manual switching only).
- PNG import.
- Animated or hit-reactive crosshairs.
- Per-arm lengths, rotation, drop shadow.
- Per-window position tracking.
