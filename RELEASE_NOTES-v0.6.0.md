# PlexusX 0.6.0 — Capture-Aware Saturation

**Released:** 2026-07-26

## What changed

The tier-2 system-wide oversaturation (100–200%) is now applied via a DirectX 11
swap-chain overlay at the DWM layer instead of the Windows Magnification API.
The effect is now visible in OBS Desktop Capture, Discord screen share, NVIDIA
ShadowPlay, and Windows Graphics Capture.

## What stayed the same

- The slider still goes 0–200.
- The 100% threshold still pins the NVIDIA driver at its ceiling.
- The 5×5 color matrix math is identical to 0.5.x.
- The "no injection, EaC safe" tagline still holds.
- The VibranceEngine, the UI, the tray app, and the existing tests are unchanged.

## Performance

- -1 to -3 fps in games on mid-range GPUs (RTX 3060, RX 6600, Intel Iris Xe).
- +40–60 MB RAM for the DX11 device + per-monitor capture buffer.
- +150 ms startup time.
- 16 ms (one frame at 60 Hz) latency on the saturated output.

## Fallback behavior

On machines without DX11 or with broken display drivers, PlexusX falls back to
the Magnification API path. The slider still works for live use; capture tools
will not see the effect (this is the old 0.5.x behavior, not a regression).

## Known limitations (unchanged)

- No effect on exclusive-fullscreen games or DRM-protected video.
- Conflicts with Windows Night Light / Color Filters.
