# PlexusX 0.4.0

Your games, your colours, your crosshair — the biggest PlexusX update yet.

## Features

- **Vibrance and Saturation are now separate controls** — Digital Vibrance (0–100,
  NVIDIA driver, gentle on skin tones) and Saturation (0–200, software) are two
  independent sliders. Stack them or dial them separately.
- **Saturation works on every GPU** — the 0–200 software path doesn't care what
  graphics card you have. AMD and Intel included.
- **Crosshair overlay** — a click-through, always-on-top crosshair for any game.
  Cross, Dot, Circle and T shapes; size, thickness and gap sliders; colours with
  optional outline; named saved configs. No injection, no game-process access —
  same category as a Discord overlay, safe with anti-cheat.
- **Custom image themes** — pick any image as the app background and the whole UI
  recolours itself from it: accent, plexus particles, cards. Dim and blur sliders
  to taste.
- **Games Hub: Counter-Strike 2** — CS2 joins Rust with its own tuned config
  support and launch options.
- **FPS Tweaks** — a curated tab of system tweaks that each have a real, measurable
  effect: Ultimate Performance power plan, Game DVR off, Nagle's algorithm off,
  network throttling removal, NIC power management, NVIDIA driver profile.
  No placebo toggles.
- **Audio Edge** — a peak limiter that caps loud sounds (gunshots) at your ceiling
  while leaving quiet ones (footsteps) untouched. Your volume is always restored
  when you stop it.
- **Settings that can't corrupt** — atomic saves with an automatic backup. A crash
  mid-save or an app update can never reset your settings again.

## Fixes

- Crosshair overlay rendered as an opaque square on some systems — it now draws
  only the crosshair, with a fully transparent background.
- Crosshair outline no longer hides the picked colour at low thickness.
- Games Hub cards no longer ghost over the animated background.

## Install

Download **`PlexusX-Setup-0.4.0.exe`** below and run it. It installs in seconds,
closes any running PlexusX, and keeps all your settings.

Open PlexusX with **Ctrl+Alt+V**, from the tray icon, or from the Start Menu.

## Requirements

- Windows 10 or 11
- An **NVIDIA GPU** for driver Vibrance (0–100). Saturation, crosshair, Games Hub
  and everything else work on any GPU.

## Note on the Windows warning

PlexusX isn't code-signed yet, so Windows may show **"Windows protected your PC"**.
Click **More info → Run anyway**. This is normal for independent apps and goes away
once the app is signed.
