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

- **`GameProfile.GameHub` is now dead data.** The Games Hub is gone, so nothing reads or applies
  those fields, but they are still in the persisted profile format. Removing them changes a
  saved-file format for no user-visible benefit, so they stay for 1.0 and should be dropped in
  a migration later.

## Deliberately deprioritised

- **The 55 remaining ad-hoc `new Font(Theme.FontFamily, …)` sites.** The guard test stays
  skipped. This is a performance-hygiene rule — those fonts are allocated inside `OnPaint`,
  which runs ~30×/second per control — and it is worth doing, but it is invisible to a user
  and costs 55 hand edits. Product work (advanced colour, the Monitor tab, share codes) moves
  the app further tonight. Un-skip `FontsComeFromTheDesignLayer` when the sites are migrated.

## Known contract exceptions

- **`Tools/PlexusXKeys/MainForm.cs` keeps a stock `ListView`** for the ledger grid. Replacing it
  means writing a sortable multi-column grid, and this tool is never distributed (its own class
  comment says so). It is themed by colour. This is the one place in the repo where U1 is
  knowingly not met, and it is recorded rather than hidden.
- **The U1 test does not catch target-typed `new()`.** A field written `private readonly ListView
  _list = new();` slips past the regex, which only matches `new ListView(` / `new ListView {`.
  Worth tightening if a stock control ever reappears that way.

## Notes to self

- **Never round-trip a source file through `Get-Content | Set-Content`.** Windows PowerShell 5.1
  reads as ANSI and writes UTF-8, which corrupted every non-ASCII character in
  `TrayApplicationContext.cs` — em-dashes became `â€”`. The mojibake contract test caught it
  within one run. Repair is to re-encode the mojibake chars back through CP1252 and decode as
  UTF-8. Use the Edit tool on source files instead.

## Test count history

| Point | Passing | Skipped |
|---|---|---|
| Baseline before tonight | 1204 | 4 |
| Phase 0 — contract tests | 1246 | 6 |
| Phase 1 — DDC/CI probe | 1252 | 6 |
| Phase 2 — after the cuts | 1153 | 6 |
| Phase 3 — theme complete | 1158 | 6 |
| Phase 4 — legal + viewer | 1176 | 6 |
| Phase 5 — advanced grade | 1176 | 6 |

The Phase 2 drop is ~99 tests deleted with the features they covered, not a regression.

## Where the night stopped

Done: Phase 0 (rules as tests), Phase 1 (DDC/CI probe — **the panel answers**), Phase 2 (the
cuts + Audio Edge relocated), Phase 3 (theme complete except fonts), Phase 4 (legal + in-app
viewer), Phase 5.0 (advanced grade re-exposed).

Not started, in priority order:

1. **Per-game colour presets** with hover preview, and custom presets kept visually separate.
   The advanced channels they need now exist, which was the blocker.
2. **A/B compare** with a cooldown.
3. **The Monitor tab** — the probe is built and verified, the UI is not. Carry the brightness
   caution above into it.
4. **Crosshair share codes.**
5. The 55 font sites, and un-skipping the last two contract guards.
