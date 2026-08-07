# Overnight log — 2026-08-07

Direction: **PlexusX owns your monitor. Everything you look at, nothing under the hood.**
Spec: `docs/superpowers/specs/2026-08-07-plexusx-monitor-philosophy-design.md`
Plan: `docs/superpowers/plans/2026-08-07-plexusx-1.0-app-complete.md`

## Authorised by the user

- Cut the Games Hub entirely, including all four per-game pages.
- Cut FPS Tweaks and the system-tweak engine.
- Cut launch options, the RAM cleaner, and Auto High CPU Priority.
- **Cut the Keybinds tab.**
- **Move Audio Edge into Settings**, renamed to something plain — "Loud footsteps" or similar.
- Add crosshair share codes.
- Move Shortcuts → (Keybinds is gone, so → Settings), game-launch rules → Game tab (gone, so → Resolution or cut), Recording card → Display.
- Advanced display colour channels, because per-game presets need them to differ.

## Deferred to the user

- `[[LEGAL_ENTITY]]` — the legal name and address for `LICENSE`, `PRIVACY.md`, `EULA.md`. Placeholder used throughout.
- Pricing, plan names, trial length — untouched.
- Any destination URL not already in `AppInfo` — none added.
- The GitHub release itself is not created; the build is left ready.

## Worklist captured by Task 0.1

**Stock Win32 controls — 8 sites.** None of these are removed by the Phase 2 cuts.

```
OnboardingForm.cs:159                    LinkLabel
OnboardingForm.cs:186                    LinkLabel
WhatsNewWindow.cs:47                     TextBox   (multiline notes body)
Pages/SettingsPage.cs:164                Button
Tools/PlexusXKeys/ActivateDialog.cs:59   Button
Tools/PlexusXKeys/ActivateDialog.cs:80   Button
Tools/PlexusXKeys/ActivateDialog.cs:86   Button
Tools/PlexusXKeys/MainForm.cs:394        Button
```

**Ad-hoc fonts — 89 sites.** Far more than the ~23 estimated. A large share sit in
`ApexSettingsPage`, `Cs2SettingsPage`, `FortniteSettingsPage`, `FpsTweaksPage`, `GamesHubPage`
and `KeybindsPage` — all of which Phase 2 deletes. Re-count after the cuts before doing any
migration work by hand.

## DDC/CI verdict — the night's key fact

**This machine's monitor supports DDC/CI on all three channels.** Phase 6 is buildable.

```
monitors found : 1
  name       : Generic PnP Monitor
  brightness : True  (0..0..100)
  contrast   : True
  rgb gain   : True
  refusal    : (none - it answered)
```

**Caution carried into Task 6.3:** brightness reports current = **0** while the screen is
plainly lit, so that read is either stale or the panel answers lazily on first contact. S6 says
store the original before the first write and always offer a revert — but reverting to a
falsely-zero original would black the panel out. Task 6.3 must re-read immediately before
storing, and refuse to treat a bottom-of-range first read as the original without a confirming
second read.

## Surprises

- The font violation count is 89, not the ~23 estimated from an earlier grep. The earlier grep
  matched a narrower pattern. Sequencing the cuts before the migration turns most of this into
  deletions rather than edits.
- `WhatsNewWindow.cs:47` was previously judged "acceptable, leave it" because the notes body is
  already borderless on a themed fill. The contract test disagrees, and the contract wins —
  `GlassTextBox` gained a `Multiline` mode precisely for this case.

## Skipped and why

_(nothing yet)_

## Test count history

| Point | Passing | Skipped |
|---|---|---|
| Baseline before tonight | 1204 | 4 |
