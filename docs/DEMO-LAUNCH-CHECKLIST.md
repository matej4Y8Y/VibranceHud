# DEMO-LAUNCH-CHECKLIST — v0.9.0-rc1 (2026-07-28)

Target: 100 fresh users in 48 hours. This doc is what gets shipped with the
installer, posted to Discord, and linked from the landing page so the demo
cohort knows what to expect.

## TL;DR

PlexusX v0.9.0-rc1 ships ready for the public demo. The alt-tab saturation-
disappears bug is fixed, the screen-overlay settings page surfaces a one-click
DX11 retry, the Vibrance page layout no longer clips at default window size,
and any unhandled exception now shows a real dialog instead of killing the
process silently. 332 unit tests pass, visual sweep across every page PASS.

## What's new since v0.8.1

### Fixes
- **Alt-tab saturation disappears** — the screen overlay now follows the
  game the same way before and after alt-tab. Previously, focus loss
  triggered a hard `Clear()` of the colour matrix; that's gone, and the
  engine's suspend-flag respect is now a regression-tested invariant.
- **"Open main window" picker was clipped** — the second global hotkey
  picker is now reachable via the page's scrollbar at every window height.
- **Uncaught exceptions** — `Application.ThreadException` and
  `AppDomain.UnhandledException` are now wired to a friendly dialog. The
  previous "app just disappeared" behaviour is the single worst first-launch
  experience for a new user; this kills that class of bug.
- **Silent DX11 fallback** — the Settings page now has a `Retry display
  engine` button next to the `Fallback mode (not visible in screen
  capture)` warning. 100 fresh users will hit fallback on at least some of
  their machines (no DX11 GPU, locked session, broken driver); the button
  restarts PlexusX so the DX11 init runs again with no reinstall.

### Verified mechanical
- Release build: clean.
- 332/332 unit tests pass.
- No undisposed per-frame GDI allocations.
- `publish/PlexusX.exe` present and fresh.

### Verified visually (every page)
- Vibrance: PASS
- Games Hub: PASS
- FPS Tweaks: PASS
- Crosshair: PASS (no opaque square artifact)
- Settings: PASS (with Retry button visible)
- Set Profile: PASS
- Account: PASS

## What the demo cohort will hit

| Scenario | What happens | Expected user action |
|---|---|---|
| Fresh install, no games | Vibrance page works; Games Hub shows CS2/Apex/Fortnite "not installed" | Use Vibrance + crosshair right away |
| Fresh install, Rust only | Games Hub shows Rust installed (green) | Open Vibrance, drag saturation, see it on Rust |
| Fresh install, no DX11 GPU | Settings → "Fallback mode" with Retry button | Click Retry once after closing any fullscreen apps |
| Alt-tab during saturation tweak | Saturation stays applied after returning to the previous app | (this was the headline bug — now fixed) |
| Drag saturation during fullscreen Rust | Saturation applies, persists on disk (autosave debounce) | No action needed |
| Quit PlexusX | Tray icon disappears; settings persist | (expected) |
| Re-launch PlexusX | Last settings restore exactly | (expected) |
| Update available | Splash downloads installer, runs at next launch | Wait one cycle, no user action |
| Crash (any cause) | A real dialog with the error message — not silent exit | Copy the message into a bug report |

## What the demo cohort might find that we don't know about

These are the most likely first-day bug categories from a 100-user cohort.
We can't predict them; we can only make them reportable.

- **Antivirus blocks Process.Start** for the auto-update installer. Mitigation
  in the installer is `/VERYSILENT /NORESTART` already, but some AVs still
  flag unsigned installers — this is the EV-cert blocker on the roadmap.
- **Multi-monitor with different GPU adapters** for the DX11 overlay.
  Already handled in `DxDevice` per-output device creation; if it fails
  for a specific monitor combo we'll see it in the first batch of reports.
- **DPI scaling > 100%**. The app uses `HighDpiMode.SystemAware`; some
  scaling scenarios break the manually-laid-out VibrancePage. We're not
  explicitly testing 125/150/175% on launch.
- **A keyboard layout where the user's hotkey letters are remapped**. The
  picker uses VK codes so this is mostly OK, but worth noting.
- **NVIDIA driver version too old or too new**. NVAPI exceptions land in
  the catch around `VibranceController()` constructor; the user sees the
  app work without driver-level vibrance (software path only).

## Known non-code blockers for paid launch (Phase 2)

These are tracked in `docs/ROADMAP.md` and are unchanged by v0.9.0-rc1:

1. EV code-signing certificate (~€200-400/yr).
2. Privacy policy URL + EULA + LICENSE files in repo.
3. Merchant-of-Record (LemonSqueezy / Paddle).
4. License-key activation flow in-app.
5. Landing page + Discord server.

The PlexusX code is fit for the **free public beta (Phase 1)** launch
now. The 5 items above gate the **paid launch (Phase 2)**.

## Rollback plan

If v0.9.0-rc1 has a regression on day 1:

1. Stop the auto-update channel from releasing `v0.9.0-rc1` to anyone
   past the demo cohort (settings.json `PendingUpdateVersion` field,
   unhook `LatestReleaseUrl`).
2. Re-tag `v0.8.1` as the recommended version.
3. Pin a hotfix as `v0.8.2` from `main` if the regression is small.

The auto-update flow already handles the "user is on v0.9.0-rc1, downgrade
to v0.8.2" case via `IsNewer` — only newer releases are pushed.

## How to report a bug from the demo cohort

Demo users have one path: open the app, click Settings, and the bug-report
template is on the landing page. Discourage them from posting screenshots
of full exception dialogs in public — those stack traces leak paths and
GPU/driver info that should go through Discord's #bugs channel first.