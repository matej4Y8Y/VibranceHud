# PlexusX 0.7.0 — Auto-Apply Game Profiles

**Date:** 2026-07-26
**Status:** Approved, in implementation
**Builds on:** v0.6.0 capture-aware saturation

## Goal

When the user launches a supported game (Rust, CS2, ...), PlexusX automatically
applies the saved game-specific profile (visual sliders + game-hub options for
that game). When the game closes, the desktop defaults return. PlexusX must
be running in the tray for auto-apply to work.

## Why now

- The Games Hub already detects games (Steam registry + libraryfolders.vdf
  scan) and stores per-game state. This feature glues that detection to the
  existing vibrance/saturation state machine.
- "Set it once, forget it" is the retention pattern that converts a download
  into a habit. Without it, PlexusX is a settings panel users open once.
- The TikTok audience (Rust / CS2 / FPS community) expects one-click
  behavior. A "watch a process and react" feature is a clean 5-second
  before/after video.

## What a profile contains

A `GameProfile` is a plain JSON object stored per game id (steam appid for
Steam games, or a synthetic id for non-Steam):

```json
{
  "gameId": "rust",
  "displayName": "Rust",
  "vibrance": 100,
  "saturation": 150,
  "brightness": 100,
  "gamma": 100,
  "gameHub": {
    "graphicsQuality": "low",
    "fpsCap": 144,
    "effectToggles": ["disable-muzzle-flash", "disable-shell-casings"],
    "tools": []
  },
  "lastUpdated": "2026-07-26T17:30:00Z"
}
```

**In scope:** the four visual sliders + the game-hub launch options for the
picked game.

**Out of scope (stays global, NOT in profile):** FPS Tweaks toggle, Crosshair
preset, the DX11 saturation overlay mode. These are user preferences that
should not change based on which game is running.

## Components

| Unit | Role | Status |
|------|------|--------|
| `GameProfile` | Data model — see JSON above | new |
| `GameProfileStore` | Reads/writes `%LOCALAPPDATA%\PlexusX\profiles.json`. Migrates schema on version bump. | new |
| `ProfileApplyEngine` | Snapshot-then-apply logic. Holds a `BeforeProfile` of the current state, calls `VibranceEngine.SetSaturation(saturation)` and `IVibranceController.SetLevel(vibrance)`, writes the game-hub options to the game's config via the existing GameCard launcher. | new |
| `GameProcessWatcher` | Background `Task` polling `Process.GetProcessesByName("RustClient")` and similar every 2.5s for the supported EXE list. Emits events on launch/close. | new |
| `ProfileEditorCard` | The "Set Profile" UI panel — game picker, settings, save. 240ms scale-in animation on open. | new |
| `ProfileEngineCoordinator` | Wires the watcher to the apply engine. Owns the lifecycle (start on tray open, stop on exit). | new |
| `TrayApplicationContext` | Holds the coordinator. Tray icon now shows a green/gray dot indicating whether the watcher is running. | modified |
| `MainWindow` | "Set Profile" button in the left nav (the user-specified location) opens the editor card as a slide-in panel. | modified |
| `GameCard` (Games Hub) | When a game card is shown, an "Edit profile" button opens the editor pre-filtered to that game. | modified |

## Core logic — `ProfileApplyEngine.ApplyAsync(gameId)`

1. Load the `GameProfile` for the gameId from `GameProfileStore`. If missing,
   no-op (silent — the user just hasn't saved a profile for this game).
2. Capture the current `VibranceEngine.Vibrance`, `Saturation`, `Brightness`,
   `Gamma` into a `BeforeProfile`.
3. Apply: `engine.Vibrance = profile.Vibrance; engine.Saturation =
   profile.Saturation; ...` The existing setters call `ApplyAll()` and
   propagate to the DX11 overlay / NVAPI.
4. Apply the game-hub options via the same `GameCard.ApplyToLaunch()` path
   the existing Games Hub uses. This writes to the game's own config file
   (e.g. `%APPDATA%\..\Rust\client.cfg`).
5. Record `(gameId, BeforeProfile, At = now)` in the engine state for
   restoration.

## Core logic — `ProfileApplyEngine.RestoreAsync()`

1. Look up the current `(gameId, BeforeProfile, At)` state.
2. If `BeforeProfile` is null (no profile was applied), no-op.
3. Apply `BeforeProfile.Vibrance / Saturation / Brightness / Gamma` back to
   the engine. The DX11 overlay / NVAPI update automatically.
4. The game-hub config: we do NOT revert the config file. Reasoning: if the
   user launched Rust with graphics=low, then closed Rust, they probably
   want their desktop Rust profile to stay "low" — they're going to launch
   it again. Reverting would surprise them. The profile is applied, not the
   in-game config mutation.

## `GameProcessWatcher` algorithm

```
supportedExes = { "RustClient", "cs2", "csgo", ... }  // per supported game
pollInterval = 2500ms
knownRunning: Set<string> = {}

loop:
  currentRunning = supportedExes.Where(exe =>
    Process.GetProcessesByName(exe).Any(p => !string.IsNullOrEmpty(p.MainWindowTitle))
  )
  newlyLaunched = currentRunning - knownRunning
  newlyClosed   = knownRunning - currentRunning

  for gameId in newlyLaunched:
    fire OnGameLaunched(gameId)
  for gameId in newlyClosed:
    fire OnGameClosed(gameId)

  knownRunning = currentRunning
  await Task.Delay(pollInterval)
```

Polling is cheap (`Process.GetProcessesByName` is a single kernel call per
EXE name per 2.5s). No P/Invoke, no Steam API, no anti-cheat surface.

The watcher runs as a `Task.Run(...)` started by
`ProfileEngineCoordinator.Start()` when the tray icon opens, and
`Stop()`-cancelled on shutdown. Cancellation is cooperative (`CancellationToken`).

## What happens when two supported games run at once

`ProfileApplyEngine` holds a single `(gameId, BeforeProfile)` state. If Rust
launches (apply Rust profile), then CS2 launches (apply CS2 profile — the
`BeforeProfile` becomes the Rust-modified state, which is wrong), then CS2
closes (restore to Rust-modified state, not desktop), then Rust closes
(restore to desktop) — the user sees flicker.

**Decision:** last-write-wins, no nesting. If a second supported game
launches while one is already running, just apply that game's profile on top.
On close, restore to whatever the previous state was. Documented in the UI as
"only one auto-managed game at a time."

## UX — "Set Profile" button

Per user spec: a **"Set Profile"** button in the left nav. Tapping it slides
in an editor card from the left, with this animation:

- 240ms scale-in: `ScaleTransform` 0.95 → 1.00 (ease-out)
- 180ms opacity: 0.0 → 1.0 (ease-out)
- 8px slide: `TranslateTransform` (-8px, 0) → (0, 0)
- Reverses on close (180ms ease-in)

Implementation: a `Panel` with `DoubleBuffered = true`, animated via a
`System.Windows.Forms.Timer` ticking every 16ms (60 fps), or via
`System.Windows.Media.Animation.Timeline` if WinForms is rendering on
WPF-hosted. No new package dependency.

Card contents (top to bottom):
1. **Game picker** — dropdown listing every supported game from the Games
   Hub. Defaults to whatever game the user picked last (per-user memory).
2. **Visual sliders** — Vibrance / Saturation / Brightness / Gamma. Same
   sliders as the main Vibrance page, scoped to the picked game.
3. **Game-Hub options** — for the picked game, the same controls the Games
   Hub card shows (graphics quality dropdown, FPS cap, effect toggles,
   tools). Hidden for games that don't have hub options (e.g. CS2 has
   limited config surface).
4. **Save button** — persists the current slider + hub values as the
   profile for the picked game.
5. **Status indicator** — small green/gray dot bottom-left of the card. Green
   if the watcher is running, gray if not. Hovering shows "Auto-apply
   running" or "Auto-apply paused (PlexusX not in tray)".

The card lives in `MainWindow` as a child `Panel` docked to the left edge,
revealed via the "Set Profile" nav button.

## Persistence — Velopack-safe

`GameProfileStore` writes to `%LOCALAPPDATA%\PlexusX\profiles.json`. Velopack
updates leave `%LOCALAPPDATA%` untouched (the spec from the 2026-07-23
release-design doc explicitly excludes that folder from the install wipe).
The store uses a versioned schema (`{ "version": 1, "profiles": { ... } }`)
and migrates on load. v1 is the initial release; future schema changes
follow the same upgrade path.

The theme picker mentioned by the user already lives in `SettingsStore`,
which writes to the same `%LOCALAPPDATA%\PlexusX\` folder. The profile card
reads the current theme from there. Both survive updates.

## Failure modes (from brainstorm)

| Failure | Handling |
|---|---|
| PlexusX not running | No auto-apply. Documented in UI (gray dot). |
| User kills game from Task Manager | Process disappears, watcher notices within 5s, restores defaults |
| Two supported games open at once | Last-write-wins. UI shows "one at a time" hint. |
| Game-hub config locked (game running with cfg file open in another process) | Same exception handling as the existing Games Hub UI. Toast: "Game config is busy — try again" |
| Profile file corrupted | `GameProfileStore.Load()` catches `JsonException`, returns an empty profile set, logs to telemetry. User keeps current settings, can re-save profiles. |
| Process.GetProcessesByName throws (rare, mostly on Windows shutdown) | Watcher catches, logs, continues. Last-known state is preserved. |

## What stays the same

- `ISaturationOverlay` interface (unchanged from v0.6.0)
- `VibranceEngine` API (we only call existing setters)
- `IVibranceController` (NVAPI 0-100, unchanged)
- `DxOverlay` capture-aware saturation (no change)
- `MagOverlay` fallback (no change)
- Anti-cheat posture — `Process.GetProcessesByName` is a standard, non-invasive
  query. PlexusX never opens or injects into the game process.
- Existing Games Hub launch flow — auto-apply uses the same config-write
  path the user can trigger manually from the Games Hub card.

## Out of scope

- Steam API integration (deliberate — Approach A from brainstorm)
- Multi-monitor per-game behavior
- Cloud sync of profiles across devices
- Scheduling ("apply Rust profile every Tuesday at 8pm")
- Profile import/export (could be added later, simple JSON)
- Anti-cheat-protected games (Valorant / Vanguard, etc. — see brainstorm
  anti-list)