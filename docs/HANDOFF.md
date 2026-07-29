# PlexusX — Project Handoff

Written 2026-07-25. Everything needed to pick this project up without re-deriving
context from scratch: architecture, decisions already made, and how to ship a release.

---

## What this is

**PlexusX** (internal repo/namespace name: `VibranceHud`) is a **public commercial
Windows app**, not a personal project. ~3 EUR/month subscription, 30-minute trial
lockout, its own installer and auto-updater. Target user: PC gamers who want display
vibrance beyond what NVIDIA's control panel allows, plus per-game optimization.

- Repo: `github.com/matej4Y8Y/VibranceHud` (remote already configured, origin set)
- Real installed copy (auto-launches via Windows `Run` registry key, separate from any
  dev build): `%LocalAppData%\Programs\PlexusX\PlexusX.exe`
- Stack: C# / .NET 8, WinForms, self-contained single-file publish, xUnit tests
- Distributed via Inno Setup installer + GitHub Releases (Velopack-style auto-update
  keyed off the release **tag**, not the filename — see Release Ritual below)

**Hard requirement:** because this ships to the public,
**game/Steam detection must work on every PC** — never hardcode paths. Read Steam from
the registry (HKCU/HKLM, including WOW6432Node), parse `libraryfolders.vdf` for all
library drives, degrade gracefully if Steam/the game is absent. Cover with unit tests.

## Core product surface (as of this handoff)

- **Vibrance/Saturation** — two *separate* controls, not one combined slider:
  - **Vibrance** (0–100 driver, continues 100–200 in software once the driver caps out)
    — the NVIDIA driver's Digital Vibrance (NVAPI), non-linear, spares skin tones.
  - **Saturation** (0–200, software colour matrix) — linear, scales all chroma equally,
    works on any GPU, is what breaks past NVIDIA's real 100% ceiling.
  - Both fold into one screen-wide colour matrix (Windows Magnification API fullscreen
    colour effect) alongside Brightness and Eye Care warmth — one cheap pass per frame.
- **Games Hub** — detects installed supported games, shows cards, opens a per-game
  config page. Rust is the first supported game (edits `client.cfg` convars directly —
  no injection, no anti-cheat risk). CS2 support also exists.
- **FPS Tweaks tab** — system-wide, curated by *actual measurable effect* (deliberately
  avoiding the padding/placebo toggles competitor apps ship): Ultimate Performance power
  plan, disable Game DVR, Nagle's algorithm off, network throttling removal, NIC power
  management, NVIDIA driver profile. Status text after applying, not a gamified score.
- **Audio Edge** — a peak limiter (NOT loudness EQ): caps loud sounds (gunshots) at a
  user-set ceiling while leaving quiet sounds (footsteps) untouched, so both end up
  audible at similar volume. Fast attack (0.6), slow release (0.04) to avoid pumping.
  Captures the user's volume on Start, **always restores it on Stop** — critical
  guarantee, tested explicitly.
- **Custom image background theme** — user picks an image; the whole UI shell (accent,
  plexus particle colours, background, cards, borders) derives from the image's
  *dominant usable colour* (dark/desaturated pixels excluded from the vote, or a
  greyscale image would produce an invisible or clashing accent). Blur + dim sliders.
  Particle field always stays on top, unaffected.
- **Crosshair overlay** — a layered, click-through, always-on-top window (no injection,
  no game-process access — same category as a Discord overlay, which is why it's on the
  right side of anti-cheat where other ideas weren't, see "Rejected ideas" below).
  Presets (Cross/Dot/Circle/T), size/thickness/gap sliders, named saved configs.
- **Themes** — Violet/Emerald/Crimson/Light + the image-derived Custom theme.
- Auto-update via GitHub Releases; splash screen checks/installs updates before the main
  window opens.

## Rejected ideas — don't rebuild these without re-litigating

- **A custom "insane zoom" bind for Rust.** Technically achievable (Windows Magnification
  API, same mechanism as the vibrance effect), but explicitly against Facepunch's rules
  (anything giving an advantage or doing what's "otherwise impossible in-game"). Worse:
  shipping it would make the app unable to get an EV code-signing certificate (CAs revoke
  for cheat-tool classification) and contradicts the marketing line "No injection, EaC
  safe." **User agreed to drop it.** A legitimate FOV-toggle-via-Rust's-own-convars
  version was offered instead and never built.
- Generic "RAM cleaner" / service-disabling padding — explicitly what the user wants
  PlexusX to avoid; every FPS Tweaks toggle must have a real, explainable effect.

## Windows Defender / SmartScreen note

The unsigned dev/test builds trigger multiple SmartScreen warnings on other PCs. The real
fix is an **EV code-signing certificate** (~$200–400/yr) — this is the standing
open item, not yet acted on. Reputation also builds over time with a signed, widely
distributed build.

## Working conventions

- **Design before building anything non-trivial.** New feature / new UI surface / new
  subsystem → write a short design note in `docs/design/specs/` first. Small tweaks
  (a colour value, a slider range) — just do them, no ceremony.
- **TDD the pure logic.** Every feature in this codebase separates pure, testable logic
  (colour maths, geometry, settings resolution) from thin I/O/UI wrappers. Write the
  tests first, watch them fail for the right reason, then implement.
- **Verify, don't assume.** Read real file contents before building against them rather
  than guessing. Actually look at the rendered UI to confirm a visual change landed —
  "the command didn't error" is not proof of correctness.

## Notable bugs already fixed (context in case they resurface)

1. **`Bitmap.GetHbitmap(Color background)` discards alpha.** Used in the crosshair
   overlay window — turned the entire transparent canvas into a solid opaque black
   rectangle sized to the crosshair's bounds (grew with the Size slider, looked like it
   was "eating" whatever page was open). Fixed: `Format32bppPArgb` + parameterless
   `GetHbitmap()`, which is the correct pairing for `UpdateLayeredWindow`'s
   `AC_SRC_ALPHA` blend.
2. **Double-nested WinForms control transparency.** `GameCard` (transparent) nested
   inside a `FlowLayoutPanel` (also transparent) over the animated particle-field
   background produced a stale, twitching "ghost" of an old frame. One level of this
   trick works fine everywhere else in the app (`CardPanel` does it successfully); two
   nested levels breaks. Fixed by removing the FlowLayoutPanel and laying cards directly
   on the page in a manual grid.
3. **A child control positioned outside its own parent's bounds is invisible no matter
   what the page's own scroll setting is.** The crosshair page's "Saved" row was placed
   below its CardPanel's bottom edge — grew the card and set `AutoScrollMinSize`
   explicitly.
4. **`SwatchDot : Control` threw "control does not support transparent background
   colours."** `SetStyle(..., SupportsTransparentBackColor, true)` must be called
   *before* assigning `BackColor = Color.Transparent`, not after.
5. **`SettingsStore` used to write settings.json non-atomically.** A crash mid-write
   could leave a truncated file and silently reset every user setting on next launch.
   Fixed: write-to-temp-then-swap + keep one `.bak`, fall back to the backup on a corrupt
   read. This is what makes settings survive both app updates (which only wipe the
   install folder, never `%AppData%`) and crashes.

## Release ritual (how to actually ship a version so auto-update reaches users)

1. Bump `<Version>` in `VibranceHud.csproj` **and** `#define AppVersion` in
   `VibranceHud.iss` to the same `X.Y.Z`.
2. `dotnet publish -c Release -o publish` — self-contained/single-file settings already
   live in the csproj, no extra flags needed. Produces one ~155MB `publish/PlexusX.exe`.
3. `ISCC.exe VibranceHud.iss` (Inno Setup Compiler, at
   `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`) →
   `installer/PlexusX-Setup-X.Y.Z.exe` (~48MB compressed).
4. On GitHub: create a **NEW** release tagged **`vX.Y.Z`** and upload that installer.
   **The updater reads the release tag, not the filename** — reusing an old release's tag
   with a new installer means nobody updates. (As of 0.2.2 it also cross-checks the
   installer filename and takes the newer of the two, so a mistag is no longer fatal, but
   still tag correctly.)
5. The installer force-closes any running PlexusX and wipes the install folder before
   installing (fixes locked-file errors and old loose-DLL cruft from pre-self-contained
   builds). User settings live in `%AppData%\PlexusX`, a different folder, untouched.

## Roadmap

Full roadmap committed at `docs/ROADMAP.md`. Headline order: (1) per-game auto-apply
vibrance profiles — the "killer feature"; (2) branding/onboarding polish (done); (3)
trial + licensing (LemonSqueezy/Paddle — Merchant of Record handles EU VAT + license
keys in one); (4) website + code-signing cert; (5) more games starting with CS2 (partly
done); (6) AMD/Intel vibrance support to expand the addressable market.

## Marketing side (TikTok videos)

Not code, but part of this project: TikTok demo videos are edited with ffmpeg, following
an approved house style — matte black background, single accent colour matched to
whichever app theme is on screen, two-line captions (white setup line + bigger accent
payload line, low in frame), before/after comparisons stacked top/bottom (not
side-by-side — too narrow to read in 9:16), a talking starburst logo for brand
beats/CTAs. Voiceovers are ALWAYS transcribed with faster-whisper before cutting — never
built against an assumed/guessed script (this caused a full rebuild once already). FPS
comparison shots need a large glowing on-screen number overlay matching the real in-game
counter's steady-state reading (not its first-frame value, which is often an outlier) —
the in-game counter alone is too small/low-contrast to read on a phone.

## Hard-won lessons worth keeping

Distilled knowledge from building this, worth carrying into any similar project:

- **Portable Windows detection**: never hardcode install paths. Read Steam via registry
  (HKCU/HKLM + WOW6432Node), parse `libraryfolders.vdf`, degrade gracefully if absent.
- **Safe config-file editing**: backup before writing, lossless line-by-line round-trip
  (don't reformat/reorder lines you don't understand), never write to a config file while
  the game that owns it is running.
- **WinForms rendering gotchas** (all bit this project at least once):
  - `SetStyle(..., SupportsTransparentBackColor, true)` before, never after, assigning
    `BackColor = Color.Transparent`.
  - True control transparency over an *animated* custom-painted parent is fragile and
    breaks down entirely once nested two levels deep — one level (a card directly on the
    animated page) works; a transparent control inside another transparent control does
    not.
  - Jagged/aliased rounded corners → use `TransparencyKey`, not `Region`.
  - Never allocate GDI objects (brushes/pens/bitmaps) inside a per-frame `OnPaint` without
    disposal — animated UI repaints ~30x/sec.
  - `Invalidate(true)` (not plain `Invalidate()`) when a shared animated backdrop changes
    and multiple child panels need to repaint in sync.
  - A layered window (`WS_EX_LAYERED`) must suppress its own default `OnPaint`/
    `OnPaintBackground` entirely — `UpdateLayeredWindow` is the only thing that should
    ever paint it, or a stray default-background flash can show through.
  - `Bitmap.GetHbitmap(Color)` (the overload with a background colour) **destroys the
    alpha channel** — never use it for anything headed into `UpdateLayeredWindow`; use
    the parameterless overload with a `Format32bppPArgb` source bitmap instead.
- **Verifying inputs/outputs before trusting them**: exit code 0 doesn't mean the output
  is *correct* — check duration/mtime/format for renders, extract and look at frames for
  anything visual, transcribe audio before cutting to it rather than guessing the script.
- **Premium desktop UI look**: layered near-blacks, a single accent colour per theme
  (never rainbow), glassmorphism via a manually-painted translucent fill + grey rim (not
  relying on real OS transparency/blur), letter-spaced uppercase section captions,
  particle/plexus animated backgrounds, palette-as-roles so themes swap cleanly, hand-
  drawn vector icons rather than font glyphs/emoji.

## Key files

- Remote: `https://github.com/matej4Y8Y/VibranceHud`
- This file: `docs/HANDOFF.md` (committed, so it travels with the repo)
- Specs: `docs/design/specs/*.md`
- Roadmap: `docs/ROADMAP.md`
