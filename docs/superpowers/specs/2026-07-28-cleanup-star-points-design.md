# PlexusX Cleanup + Star/Points + Bug Hunt — Design

Date: 2026-07-28
Version target: 0.9.0 (minor bump — multiple structural changes)
Author: brainstormed with matej4Y8Y (PlexusX CEO)

---

## Context

The PlexusX codebase has accumulated dead/broken features over the v0.7.x releases:

- The **NVIDIA Tweaks card** on the Rust Settings page (NVAPI driver-level settings for Per-Application Profile) doesn't work on the user's NVIDIA card. Every toggle reports "Driver didn't accept this setting" even with the v0.8.2 tri-state fix — the gap between NVAPI's expected environment and the user's driver version is unbridgeable, and DRS writes require admin.
- The **Potato (NVIDIA Experience)** tweak writes `TargetPowerMode=0` to NVIDIA Experience's per-application JSON, but the slider in NVIDIA Experience's UI doesn't move because value 0 is NVIDIA Experience's "Auto/Recommended" sentinel, not Max Performance. The correct value would require trial-and-error against the user's specific NVIDIA App build, and the user has decided the whole direction doesn't work.

The user wants a clear, scannable cleanup that will ship as a single release, then a points/scoring feature, then a pass to find any remaining bugs.

---

## Sequence (locked in brainstorm)

1. **Cleanup** — remove broken features + reorganize misplaced tweaks
2. **Add** — star + points + PlexusX logo in bottom-left
3. **Bug hunt** — review all UI/code, fix anything broken, fix scroll jitter

---

## Sub-project 1: Cleanup

### 1.1 Remove NVIDIA Tweaks card from Rust Settings page

**Files affected:**

- `Pages/RustSettingsPage.cs`:
  - Remove field declarations `_nvCard`, `_scanButton`, `_scanLastLabel`, `_nvCardY` (lines ~52-58)
  - Remove the `BuildNvidiaCard(int y, INvidiaDriverSettings gpu)` call site in the Rust Settings page constructor (the `// ---------- NVIDIA driver tweaks ----------` section)
  - Remove the `BuildNvidiaCard` method (~lines 415-525)
  - Remove the `RunScan` method (~lines 527-560)
  - Keep imports of `Nvidia` namespace if other features need them — verify

- `Nvidia/NvidiaTweakElevationService.cs` — keep (the elevated-helper pattern could be useful elsewhere later). Mark it `[Obsolete]` with a comment pointing to the removed card.
- `Nvidia/NvidiaTweakCatalog.cs` — keep, but no longer referenced from Rust page.
- `AppSettings.cs` — remove `RustNvidiaTweaks` and `RustNvidiaTweaksNeedsAdmin` fields since the UI is gone (settings would otherwise persist unused data). Migration: if fields exist on load, drop them silently.

### 1.2 Remove Potato (NVIDIA Experience) feature

**The Potato tweak currently lives in two places:**

- The "Potato (NVIDIA Experience)" row in `Pages/FpsTweaksPage.cs` — surfaced via the catalog
- The `_nvAppTweak ?? new NvAppRustProfileTweak()` entry in `SystemTweaks/SystemTweakCatalog.cs` line 61

**Files affected:**

- `SystemTweaks/SystemTweakCatalog.cs`:
  - Remove the `_nvAppTweak` field, the second ctor that takes `NvAppRustProfileTweak?`, and the catalog entry that materializes the NvAppRustProfileTweak
  - Keep the single-arg ctor `(IRegistryAccess reg)`
- `Nvidia/NvAppRustProfileTweak.cs` — delete the file
- `tests/VibranceHud.Tests/NvAppRustProfileTweakTests.cs` — delete the file

### 1.3 Reorganize misplaced tweaks

**Current placement vs. right placement:**

| Tweak | Currently in | Should be in | Reason |
|---|---|---|---|
| Game DVR off | FPS Tweaks | FPS Tweaks | ✓ correct — system-wide |
| Network Throttling | FPS Tweaks | FPS Tweaks | ✓ correct — system-wide |
| Foreground priority | FPS Tweaks | FPS Tweaks | ✓ correct — system-wide |
| Game scheduling boost | FPS Tweaks | FPS Tweaks | ✓ correct — system-wide |
| Disable Game Mode | FPS Tweaks | FPS Tweaks | ✓ correct — system-wide (situational) |
| Rust priority | Rust page | Rust page | ✓ correct — game-specific |
| Rust RAM trim | Rust page | Rust page | ✓ correct — game-specific |
| Potato (NvApp) | FPS Tweaks (via catalog) | **REMOVED** | see 1.2 |
| Rust NVIDIA Tweaks card | Rust page | **REMOVED** | see 1.1 |

**Verdict:** After 1.1 and 1.2, the placement is correct. No further moves needed.

### 1.4 Version bump

`0.8.2 → 0.9.0` because two non-trivial features are being removed (semver minor-bump convention for breaking change in a 0.x release is a minor bump).

---

## Sub-project 2: Star + Points + Logo in bottom-left

### 2.1 Visual design

```
┌──────────────────────────────────────┐
│ [logo] PlexusX                v0.9.0 − │ ← top header (existing)
│                                      │
│           ... rest of UI ...         │
│                                      │
│                                      │
│ ★ 124    ◇    ⊟    v0.9.0            │ ← new footer bar
└──────────────────────────────────────┘
```

**The bottom-left footer, in order from left:**

1. **PlexusX logo icon** — the existing asterisk-style "✻ PlexusX" mark from the top header, **but smaller** (a 12-14px glyph rather than the 36px header version), themed to the current ThemeAccent color. Pure-graphics (no text). Acts as a brand mark.
2. **Points counter** — a small star character `★` (Unicode star) followed by the integer point total e.g. "★ 124", themed in `ThemeAccent`. Only calculated from FPS Tweaks (current Safe recommendations applied).
3. **Version text** — `v0.9.0` as before, dim text.

### 2.2 Where it lives

The current version text sits in `MainWindow.cs` somewhere. Need to read the file to find the exact location. The footer is constructed once when the window loads and refreshed when points change.

**Look for:** the existing placement of the version text in `MainWindow.cs`; replace it with a 3-element horizontal row of controls (logo glyph + star+counter + version), positioned 16px from bottom and left edge.

### 2.3 Points calculation

Each FPS Tweaks SAFE toggle is worth points equal to its impact-class score. The user said "add to every optimization a points niot like every every but like in the fps tab" — meaning the points counter ONLY reflects FPS Tweaks tabs, not other tabs.

**Proposed point values per FPS Tweaks SAFE toggle:**

| Tweak | Points |
|---|---|
| Disable Game DVR | 20 |
| Remove Network Throttling | 25 |
| Prioritise Foreground Game | 30 |
| Boost Games Scheduling | 25 |
| Disable Windows Game Mode (Advanced) | 10 |
| (Max) | **110** |

**Behavior:**

- Points = sum of point values for each FPS Tweaks SAFE toggle that is currently `IsApplied() == true`
- Counter refreshes every time an FPS Tweaks toggle changes state (the existing Toggle.CheckedChanged already runs on the UI thread; we hook into the same point)
- Counter also refreshes once on MainWindow load
- Advanced tier counts toward the total when its toggle is on (no special exclusion rule)

### 2.4 Data flow

```
[FPS Tweaks toggle change] ──┐
                              ├─► SystemTweakService.Toggle returns success
[MainWindow loads] ──────────┘                  ▼
                                         PointsCalculator.ComputePoints()
                                              ▼
                                         (sum of IsApplied per SAFE tweak)
                                              ▼
                                         FooterPointsLabel.Text = "★ " + total
```

**Counter refresh wiring:** The cleanest hook is on `SystemTweakService.Toggle` itself, raising an event `PointsChanged` that the footer subscribes to. Avoids coupling FPS Tweaks page to MainWindow; survives navigations.

### 2.5 Implementation sketch

- New file `SystemTweaks/PointsCalculator.cs`:
  - `public int ComputePoints(IReadOnlyList<ISystemTweak> all)` — sums SAFE-tier values for each tweak whose `IsApplied()` returns true.
  - Point values live in a static `Dictionary<string,int>` keyed by tweak id; populated in the calculator's ctor.
- New file `SystemTweaks/PointsChangedEventArgs.cs` (or just inline):
  - Simple `event EventHandler<PointsChangedEventArgs>` on `SystemTweakService`.
- MainWindow gets a footer panel with the 3 elements.

### 2.6 Tests

- `PointsCalculator.ComputePoints`:
  - All 4 SAFE off → 0
  - All 4 SAFE on → 110
  - One on (Boost Games Scheduling only) → 25
  - Advanced tier (Game Mode) on counts toward the total when its toggle is True (consistent with "any applied tweak counts")
- `IsApplied()` check is mocked via fake tweaks (returns Canned for the 4 ids)

The fake engine pattern (`FakeEngine` from prior tests) is available — `FakeVibranceEngine implements` etc. Should make `FakeSystemTweak : ISystemTweak` work.

### 2.7 Files affected (Sub-project 2)

- **NEW** `SystemTweaks/PointsCalculator.cs`
- **MODIFIED** `SystemTweaks/SystemTweakService.cs` — raise `PointsChanged` after `Toggle()`
- **MODIFIED** `MainWindow.cs` — replace version label with footer panel (logo glyph + points + version)
- **MODIFIED** `Pages/FpsTweaksPage.cs` — refresh points on navigate-into + after toggle (via service event)
- **NEW** `tests/VibranceHud.Tests/PointsCalculatorTests.cs`

---

## Sub-project 3: Code review pass + bug hunt

This is the "look at all the code, find anything broken or off" pass. It's the open-ended one.

### 3.1 Concrete things to investigate

1. **Scroll jitter** the user reported: when scrolling, the background image twitches for a few ms. Probable cause: background redraw is happening on scroll without `DoubleBuffered = true`, OR `ParticleField` (the animated background) is rendering one extra frame per scroll tick. The fix is likely setting `DoubleBuffered = true` + `OptimizedDoubleBuffer` + `AllPaintingInWmPaint` on every control that hosts the background. Specifically look at:
   - `Controls/ParticleField.cs` (or wherever the animated background lives)
   - `GlowPage.cs` (the panel that hosts pages)
   - `MainWindow.cs` (the form)
2. **Things that "do nothing"**: walk through every toggle and check there is a real side-effect when enabled. Anything that doesn't change anything when toggled is removed.
3. **UI imperfections**: identifiers already listed by the user (rust tweaks in fps tweaks) addressed by Sub-project 1.
4. **Bug class scan**: NullReferenceException edge cases, exception swallowing that hides real bugs, race conditions on file IO, settings.json migration issues (e.g., old fields with new code).

### 3.2 Approach

This is the open-ended phase. Concrete steps:

1. Read every source file (`Pages/*.cs`, `Nvidia/*.cs`, `SystemTweaks/*.cs`, `Controls/*.cs`, `MainWindow.cs`, `TrayApplicationContext.cs`).
2. Note every bug or non-working behavior.
3. Group findings into fix categories: scroll/jitter, dead-code-removal, exception handling, race conditions, missing migrations.
4. Estimate: many small bugs. Probably 3-8 hours of work.
5. Fix incrementally, test each.

This sub-project needs its own brainstorm because the bugs are discovered by exploration.

---

## Risks

- **Risk A:** points counter requires every FPS Tweaks toggle to set a unique id, keyed correctly in `PointsCalculator`. Mitigation: the existing tweak ids (`game-dvr`, `network-throttling`, etc.) get used directly.
- **Risk B:** removing `RustNvidiaTweaks` from settings.json means upgraded users carry stale data. Mitigation: silent drop in migration.
- **Risk C:** scroll jitter fix requires profiling — could take multiple iterations.

## Out of scope

- Visual redesign of the main app
- Adding new games to the games hub
- New themes / theme catalog work

## Ship plan

Single release: **v0.9.0**. All three sub-projects land together because they each modify MainWindow / shared settings / shared catalog. A single release avoids partial-state installs where some users get the star without the points or vice versa.
