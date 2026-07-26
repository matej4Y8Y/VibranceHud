# PlexusX QA — Test Plan

**How to run:** tell the agent "run QA" (the `plexusx-qa` skill drives this), or run
`scripts/qa-check.sh` alone for the mechanical half. A full QA run writes a verdict to
`docs/qa/QA-REPORT-<date>.md`.

**When it runs:** mandatory before every release build/tag, and on demand after any
risky change (rendering, settings, detection, updater).

---

## 1. Mechanical (scripts/qa-check.sh — always first)

- Release build: 0 errors, 0 warnings
- Full unit suite green (record the count)
- Release runs only: `<Version>` in csproj == `AppVersion` in .iss == release notes file exists
- GDI heuristic: no undisposed `new SolidBrush/Pen/Bitmap` in OnPaint paths (per-frame leak trap)
- `publish/PlexusX.exe` newer than last commit

## 2. Visual sweep (agent + screenshots, cheap submodel inspects)

Launch the **dev build** (never the installed copy), visit every page, screenshot each,
and inspect for the project's known bug classes:

| Page | Must be true |
|---|---|
| Vibrance | Separate Vibrance + Saturation sliders; brightness/gamma/eye-care; presets; readouts aligned; nothing clipped |
| Games Hub | Cards clean over the particle background — no ghosting/double frames; grid scrolls if >6 games |
| Rust / CS2 / Apex / Fortnite | Toggles reflect real config; running-game warning shows when the game is open; Apply writes config + backup |
| FPS Tweaks | Status text after Apply — never a fake score |
| Crosshair | Preview renders on checkerboard; **no opaque square artifact anywhere on screen**; shape chips switch; SAVED row reachable |
| Settings | Theme swatches incl. Custom image; image pick recolours accent/particles; dim/blur work |
| Account | Page renders, trial state shown |
| Window chrome | Rounded corners smooth, glass panels, particles animate on every page; resize leaves no stale frames |

**Bug classes from project history (always check):** opaque overlay squares (alpha
flattening), nested-transparency ghosting, bottom rows clipped by parent bounds, stale
repaint after theme switch, misaligned custom-drawn text.

## 3. Functional spot checks (agent drives the app)

- **Nav round-trip (regression: 0.5.0 crosshair bug):** visit EVERY page, then every page
  AGAIN — a persistent page disposed on leave throws ObjectDisposedException on re-entry
  and leaves the content host layout-suspended (flicker + dead nav). Check every nav
  button highlights when active and un-highlights when left.
- Drag vibrance/saturation → screen visibly changes (before/after screenshot delta)
- Crosshair ON → cross at screen centre, clicks pass through to the window beneath
- Theme switch → accent changes everywhere at once
- Change settings → close → relaunch → values kept; corrupt `settings.json` → app recovers from `.bak`
- Per game: Apply → config file contains the change + `.vibrancebak` exists → Restore Backup reverts

## 4. Performance budgets

| Metric | Budget | Measure |
|---|---|---|
| Cold start → main window | < 3 s | stopwatch |
| Idle CPU (particles running) | < 3 % | process sampling, 30 s |
| RAM after 60 s idle | < 250 MB | working set |
| Slider drag | no visible stutter | watch + CPU spike check |

## 5. Exploit / abuse checks

- Config providers write **only** inside the game's own config path or `%AppData%\PlexusX` — review every `Path.Combine` fed by user input (saved-config names, prompt text) for traversal (`..`, `:` , `/`)
- Backup/restore targets only `config path + ".vibrancebak"` — never user-supplied paths
- Malformed `settings.json`, deleted theme image, corrupt config → app degrades, never crashes
- Updater: version read from release **tag** (+ filename cross-check) — can't be tricked into a downgrade
- Crosshair window keeps `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE` — click-through, no focus theft
- `Process.Start` call sites: no unsanitized user input as target
- No secrets/keys in the repo (scan added lines)

## 6. Report

`docs/qa/QA-REPORT-<date>.md`: PASS / FAIL / SKIP per item, screenshot filenames as
evidence, perf numbers, exploit findings, and a final verdict: **SHIP** or **NO-SHIP**
with the blocking items listed.
