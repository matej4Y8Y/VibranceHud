# PlexusX 0.9.0-rc1 — Demo launch prep (2026-07-28)

First public-ready build. Heading into the demo launch with 100 fresh users.

## What changed

### Headline fix: alt-tab saturation disappears

The single most reported bug from internal testing. Setting saturation via
the popup, alt-tabbing back to the game, and watching the tint drop back to
neutral — every time.

The root cause was twofold: `MainWindow.OnDeactivate` called
`VibranceEngine.SuspendOverlay()` on every focus loss (which immediately
called `Clear()` on the overlay), AND `VibranceEngine.ScheduleOverlayApply`
didn't check the `_overlaySuspended` flag — so a slider drag while the
overlay was "suspended" would silently re-enable it under the user's back.

This build removes the auto-suspend wiring (the engine method stays for
future opt-in use) and adds the missing suspend-respect guard to
`ScheduleOverlayApply`. The popup, the main window, and the auto-apply
path all keep the saturation on the user's screen across focus changes
exactly as expected.

Locked in by two regression tests:

- `SuspendOverlay_StopsSubsequentValueWrites_UntilResume`
- `VibranceSlider_Drag_EngineUpdates_ValueDuringDrag_FlushOnEndDrag`

### Settings page: one-click DX11 retry

100 fresh users will hit the silent DX11 → Magnification fallback on at
least some of their machines (no DX11 GPU, locked session, broken driver).
The fallback still saturates the screen, but it's invisible to OBS / Discord /
NVIDIA ShadowPlay — the whole reason streamers would use PlexusX.

The Settings page now shows a `Retry display engine` button next to the
`Fallback mode` warning. Clicking it relaunches PlexusX so the DX11 init
runs again with no reinstall. Covers the most common cause: a transient
init failure (fullscreen app on launch, UAC prompt active, DXGI not yet
ready) that clears itself a second later.

### Layout fix: Vibrance page no longer clips

The second global hotkey picker ("Open main window", default Ctrl+Shift+M,
opt-in via the picker) was clipped at the bottom of the default 1040x680
window. Now `AutoScroll = true` with `AutoScrollMinSize = (0, 720)` so the
page is usable at any window height — laptop screens, external monitors,
and the default 680-tall window all work.

### Friendly crash dialog (no more silent exits)

The previous `Program.cs` only caught what `Application.Run` rethrew.
Constructor exceptions and background-thread exceptions could kill the
process with no user-visible feedback — the single worst first-launch
experience for a new user.

Now `Application.ThreadException` and `AppDomain.UnhandledException` are
wired to a `ShowFatal()` helper that produces a real dialog with the
exception message, so a bug report can include the actual error.

## What stayed the same

- Game library detection: Steam + Epic, no hardcoded paths, returns
  empty list (never throws) when absent.
- Per-game Apply / backup flow (`Restore` reverts via `.vibrancebak`).
- NVIDIA driver tweaks (Scan, Apply as admin, tri-state `NeedsAdmin`).
- Update flow: download → defer to next launch → installer runs silent
  with `/FORCECLOSEAPPLICATIONS`. Already hardened against corrupt PE
  headers (WinError 216).
- Settings durability: atomic write + `.bak` for `settings.json`.
- Auto-apply game profiles (Rust + others): opt-in via profile editor,
  `ManualOverrideActive` short-circuits when the popup is the last word.

## What 100 fresh users will hit (and what to expect)

| Scenario | What happens | Action |
|---|---|---|
| Fresh install, no games | Vibrance works; crosshair works; Games Hub shows the four supported games as "not installed" | Use Vibrance + crosshair immediately |
| Fresh install, Rust | Games Hub shows Rust (green) | Open Vibrance, drag saturation, see it on Rust |
| No DX11 GPU | Settings → Fallback warning + Retry button | Click Retry once after closing any fullscreen apps |
| Alt-tab during saturation tweak | Saturation stays applied after returning to the previous app | (this was the headline bug — now fixed) |
| Drag saturation in fullscreen game | Saturation applies, autosaves to disk (250ms debounce), no manual Save needed | (expected) |
| Crash (any cause) | A real dialog with the error message — never silent exit | Copy the message into the bug report |
| Update available | Splash downloads installer; install happens on next launch (silent) | Wait one cycle |

## Numbers

- 332/332 unit tests pass.
- Build clean.
- No undisposed per-frame GDI allocations.
- `publish/PlexusX.exe` fresh.

## What's NOT in this build (the Phase 2 paid-launch blockers)

These are tracked in `docs/ROADMAP.md` and `docs/DEMO-LAUNCH-CHECKLIST.md`
and are unchanged by v0.9.0-rc1:

1. EV code-signing certificate (~€200-400/yr). Without it the installer
   shows "Unknown publisher" and demo users may delete it.
2. Privacy policy on a public URL (GDPR).
3. Terms of service / EULA shown by the installer.
4. Merchant-of-Record (LemonSqueezy / Paddle).
5. License-key activation flow.
6. In-app "Go Pro" button.
7. Landing page + Discord server.

The PlexusX code is fit for the **free public beta (Phase 1)**. Items 1-7
gate the **paid launch (Phase 2)**.