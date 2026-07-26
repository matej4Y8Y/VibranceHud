# PlexusX 0.7.0 — Auto-Apply Game Profiles

**Released:** 2026-07-26

## What changed

PlexusX now applies a saved profile automatically when you launch Rust, CS2, Apex, or
Fortnite. Close the game and your desktop settings come back. No more "did I forget to
enable the right slider before launching?"

- New **"Set Profile"** button in the left nav opens an editor card with a 240ms slide-in
  animation. Pick a game, set its visual sliders (vibrance / saturation / brightness /
  gamma), pick its game-hub options (graphics quality, FPS cap, effect toggles), save.
- The tray's tooltip now reads `PlexusX — auto-apply running` when the watcher is active.
- Profile storage lives at `%LOCALAPPDATA%\PlexusX\profiles.json` — Velopack-safe, your
  profiles survive every auto-update from now on.
- Watcher runs in the tray context only. If PlexusX isn't open, auto-apply doesn't run.
  This is by design, not a bug — and your desktop settings stay where they are.

## What stayed the same

- v0.6.0 capture-aware saturation (still works in OBS / Discord / ShadowPlay)
- Anti-cheat posture (`Process.GetProcessesByName` only; never inject, never read game memory)
- The Vibrance / FPS Tweaks / Crosshair / Games Hub pages are unchanged
- The "no Steam dependency" approach (we watch process names directly, no Steam API)

## Editing a profile

- **From the Games Hub:** click "Edit profile ›" in the bottom-right of any installed game
  card. The editor opens pre-filtered to that game — change the values and Save.
- **From the "Set Profile" nav button:** browse every supported game. Pick the one you want
  to configure, edit its sliders + game-hub options, Save.

## Tested on

Two machines (developer + friend). Auto-apply fires within 2.5-5 seconds of a game launch
(tray icon tooltip confirms the watcher is alive). Restart PlexusX between game launches
or game-close events — your saved profile for Rust is still there.

## Known limitations (unchanged)

- Watcher polls every 2.5 seconds. There's a brief 2.5-second window after game launch where
  your desktop profile is still active. Acceptable for v1. Can be tightened later.
- Last-write-wins if two supported games run at once (rare in practice).
- Fortnite has no portable config surface, so profile includes visual sliders only.
