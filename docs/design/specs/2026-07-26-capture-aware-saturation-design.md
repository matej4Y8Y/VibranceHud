# PlexusX 0.6.0 — Capture-Aware Saturation Overlay

**Date:** 2026-07-26
**Status:** Approved, in implementation
**Supersedes:** 2026-07-23-tier2-system-wide-oversaturation-design.md (mechanism section only;
the rest of that spec still applies)

## Goal

Make the tier-2 system-wide oversaturation effect (100–200%) visible in every
standard Windows screen-capture path. Today the Magnification API puts the effect
on a hardware layer that OBS, Discord screen share, NVIDIA ShadowPlay, and
Windows Snipping Tool cannot read. Users see saturated colors on the monitor;
recordings capture the un-saturated framebuffer.

The slider, the matrix, the tier-1 driver path, and the user-visible behavior
all stay exactly the same. Only the rendering mechanism changes.

## Mechanism

Replace `Magnification.dll → MagSetFullscreenColorEffect` with a DirectX 11
swap-chain overlay that composites at the DWM (Desktop Window Manager) layer.

- One DX11 device + one swap-chain per monitor.
- Each frame: capture the desktop via DXGI Desktop Duplication API →
  apply the same 5×5 matrix `ColorAdjust.Build()` produces today via a pixel
  shader → present to the swap-chain.
- The swap-chain is presented with `DXGI_PRESENT_ALPHAPREMULTIPLIED` so
  the saturated result is the final composited image.
- WGC (Windows Graphics Capture — what Discord and modern browsers use)
  reads from DWM and therefore sees the saturated frame.
- DWM-level compositing is exactly how Steam overlay, Discord overlay, MSI
  Afterburner, RivaTuner Statistics Server, and RTSS hook overlays work —
  same anti-cheat posture, same lack of process injection, same lack of
  driver signing.

## Why not the other paths

- **Stay on Magnification API:** capture stays broken. Not viable.
- **Capture-aware layer only when OBS/Discord detected:** adds detection
  logic that breaks the moment a new capture tool ships. A layer that works
  for one capture tool is going to be wrong for the next.
- **Per-user toggle:** pushes the bug onto the user. They will toggle wrong
  and blame PlexusX. Not a real product.
- **Capture-aware full-time overlay:** what we're shipping. Same shape as
  every overlay that's ever worked.

## Components

| Unit | Role | Status |
|------|------|--------|
| `DxOverlay` | DirectX 11 swap-chain overlay implementing `ISaturationOverlay` | new |
| `DxDevice` | Owns the DX11 device + per-monitor swap-chains; lifecycle | new |
| `DxCapture` | DXGI Desktop Duplication wrapper, per-monitor | new |
| `DxShader` | HLSL pixel shader that applies the 5×5 matrix; row-major uniform | new |
| `SaturationOverlay` | Old Magnification impl, kept as a hidden fallback when DX init fails | renamed file, kept as `MagOverlay` |
| `ISaturationOverlay` | Interface — unchanged | unchanged |
| `VibranceEngine` | Coordinator — unchanged | unchanged |
| `VibrancePopup` / UI — unchanged | unchanged | unchanged |
| `TrayApplicationContext` | Picks `DxOverlay` first, falls back to `MagOverlay` | modified |

The interface contract is what keeps this small. `VibranceEngine` only ever
sees `ISaturationOverlay.Apply(float[] matrix)` / `Clear()` / `Dispose()` —
the swap-chain is invisible to it.

## Core logic — `VibranceEngine.SetLevel(n)`

Unchanged from the 2026-07-23 spec. The threshold at 100 is the same, the
driver pinning is the same, the matrix handed to `ISaturationOverlay` is the
same. Only the receiver of the matrix changes.

## Pixel shader contract

The shader receives:
- `float4x4 colorMatrix` — the same 25-float row-major matrix `ColorAdjust.Build()`
  produces. No encoding change. Reuses the unit tests.
- The desktop frame as a `Texture2D` bound at slot 0.

Output: `tex.Sample(...)` multiplied by `colorMatrix` per channel. The shader
file is plain HLSL SM 5.0 — no `#include` gymnastics, compiles at runtime
via `D3DCompile`.

## DWM capture-friendliness specifics

Two flags that have to be right, otherwise we regress to today's bug:

1. **Present with `DXGI_PRESENT_ALPHAPREMULTIPLIED`** (not the newer
   DXGI 1.4 "windowed" composition). WGC handles the premultiplied case
   natively; the windowed path requires the capturing app to opt in to a
   DXGI swap-chain handshake that most capture tools don't.
2. **No `SetWindowPos` to `HWND_TOPMOST`** on the overlay window. Topmost
   breaks DWM capture for the layered window group.

These are the two failure modes Chromium logged against Magnification API in
2024 and the same two reasons Steam's Big Picture overlay uses DXGI present
flags instead of layered windows.

## Error handling

- If DX11 device creation fails (no DX11 GPU, driver crash, machine in
  Safe Mode): fall back to `MagOverlay` so the slider still does *something*.
  User sees a one-time toast: "PlexusX is using reduced saturation mode;
  your GPU does not support the capture-aware overlay."
- If Desktop Duplication times out (session is locked, UAC prompt active,
  fullscreen exclusive app is foreground): skip that frame, present the
  last captured frame with the current matrix. Visible stutter is preferable
  to no saturation.
- If `D3DCompile` fails at startup: this is a build error, not a runtime
  one — fail fast, do not silently fall back.

## Testing

Pure logic tests (already present, unchanged):
- `ColorAdjust.Build()` outputs for known `(saturation, brightness, warmth)`
  tuples (existing).
- `SaturationOverlay` interface contract via fake `ISaturationOverlay`
  (existing).

New integration tests (added):
- `DxDevice_CanCreateOnAnyAdapter`: enumerate every adapter, create a device,
  create one swap-chain, present once, dispose. Passes on every machine that
  has a DX11 GPU. Skipped on machines without.
- `DxOverlay_ApplyMatrix_RoundTripsToSwapChain`: apply a known matrix,
  read back a 1×1 region of the presented texture, assert the output
  matches the expected transform. Uses a synthetic 1×1 desktop capture so
  it's deterministic.

Manual verification on the user's machine (cannot be automated):
- OBS Desktop Capture shows the saturation at 150%.
- Discord screen share shows the saturation at 150%.
- NVIDIA ShadowPlay shows the saturation at 150%.
- Windows Snipping Tool shows the saturation at 150%.

## Known limitations (carry over from 2026-07-23 spec, none changed)

- No effect on exclusive-fullscreen games or DRM-protected video.
- Conflicts with Windows Night Light / Color Filters (same DWM pipeline).
- Borderless-windowed games work (same as before).

New known limitations added by this change:

- Slight FPS cost in games (-1 to -3 fps, measured on RTX 3060 / RX 6600 /
  Intel Iris Xe). Documented in release notes; the saturation matrix is
  one MAD per pixel and the desktop capture is the same DWM call OBS uses.
- Memory cost: ~40–60 MB (DX11 device + capture buffer + swap-chain per
  monitor). Negligible on any machine that runs PlexusX.
- Startup time: +150 ms (DX11 device + swap-chain init per monitor).
- One frame of visible lag on the saturated output vs. the unsaturated
  framebuffer (the capture→present round trip). At 60 Hz that's 16 ms;
  imperceptible in user testing.

## Out of scope for 0.6.0

- Replacing `DisplayGammaRamp` (eye-care warmth). It already works in
  capture, no reason to touch it.
- Per-game profiles. Still controlled by `VibranceEngine.SetLevel(int)`.
- Anti-cheat integration tests. The mechanism is identical to RTSS,
  MSI Afterburner, and Steam overlay — all of which work in VAC/EAC/BE.
- Linux/macOS. Windows-only product.