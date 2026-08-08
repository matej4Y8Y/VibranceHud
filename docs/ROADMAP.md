# Roadmap

LIVING DOC. Rewritten 2026-08-08, when the direction changed.

## The philosophy

> **PlexusX owns your monitor. Everything you look at, nothing under the hood.**

If a feature changes **what you see** — colour, resolution, refresh rate, HDR, the
crosshair, the panel's own settings — it belongs. If it changes **what the PC is doing**
— registry keys, game config files, process priority, RAM — it does not.

The pitch is *"it makes your monitor look right."*

Full reasoning: `docs/superpowers/specs/2026-08-07-plexusx-monitor-philosophy-design.md`.
The rules that follow from it: `docs/UI-CONTRACT.md`.

## What this replaced, and why

The app used to be a gamer utility suite: a Games Hub that wrote to `client.cfg`, an FPS
Tweaks page of registry edits, launch options, a RAM cleaner, per-game keybinds.

All of it is gone, for reasons that were true before they were acted on:

- **It competed with free.** Rust has a settings menu; config tweaks are all over YouTube.
- **You could not see it work.** Colour is instant and obvious. A registry edit is invisible.
- **It rotted.** A game update can rename a convar. It cannot change what a gamma ramp does.
- **It was the only part that touched game files.** Everything else is display-layer.
- **The category is a race to the bottom.** The tools in it ship "+24 Score" toggles and
  things like *Disable Virtualization-Based Security*. Entering that market costs trust
  that colour earns for free.

The RAM cleaner in particular was already listed here under *"what I'm not building —
placebo, damages credibility"* while shipping in the app. That contradiction is resolved.

## The shape now

| Tab | Owns | Layer |
|---|---|---|
| **Display** | Colour: vibrance, saturation, brightness, gamma, contrast, temperature, and the advanced channels. Presets, share codes, A/B compare. | Software — gamma ramp + NVAPI |
| **Resolution** | Resolution, refresh rate, stretched PvP presets, HDR. | Windows display API |
| **Monitor** | The physical panel: brightness, contrast, RGB gain, low blue light. | DDC/CI over the cable |
| Crosshair | The overlay, its gallery, colour and share codes. | Overlay window |
| Settings / Account | The app itself, loud footsteps, legal, licence. | — |

Display changes the signal, Resolution changes the mode, Monitor changes the panel. Same
picture, three layers of control.

## Done

- Design contract enforced by tests — stock controls, hardcoded colours, culture-dependent
  numbers and mojibake are build failures, not messages.
- DDC/CI capability probe. **Verified working on the dev machine** — brightness, contrast
  and RGB gain all answer.
- The cuts above, and the ~99 tests that went with them.
- Every stock Win32 control replaced in the app, plus the key manager themed.
- `LICENSE.md`, `THIRD-PARTY-NOTICES.md`, `PRIVACY.md`, `EULA.md`, readable in-app and
  shipped by the installer. The EULA is the installer's licence page.
- Licensing: trial policy, plan catalog, key issue/validate, revocation, beta gate.

## Next

- **Advanced colour channels** — lift / gamma / gain and tint, so per-game presets can
  actually differ from one another. Six sliders cannot express Rust's brown-grey against
  CS2's concrete-orange.
- **Per-game colour presets** with hover preview, and custom presets kept visually
  separate from them.
- **A/B compare**, rate-limited — the gamma ramp misbehaves when hammered.
- **Monitor tab** on the verified DDC/CI probe: brightness, contrast, low blue light as a
  blue-gain reduction, all revertible.
- **Crosshair share codes**, the same `PX-` scheme Display already uses.

## Not building

- Anything under the hood. That is the whole point.
- Anything that risks a game account: no injection, no memory access, no game files.
- Hue-selective colour. The gamma ramp is a per-channel curve — it does three-way
  correction well and cannot do "make only the greens duller". The DX11 path that could is
  disabled. **Do not promise HSL.**

## Before charging money

- EV code-signing certificate. Without it the installer says "Unknown publisher".
- Merchant-of-record account (LemonSqueezy or Paddle) — handles EU VAT and refunds.
- Landing page, and the privacy policy on a public URL.
- The legal entity name, which is `[[LEGAL_ENTITY]]` in all four documents until it is known.
- Clean-install QA on a fresh Windows 10, and an upgrade-over-old-version run.
