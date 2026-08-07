# PlexusX — "We own the monitor" — Design

Date: 2026-08-07. Supersedes the Games Hub and system-tweaks direction.

## The philosophy

> **PlexusX owns your monitor. Everything you look at, nothing under the hood.**

One sentence, and it resolves every open argument in the product:

- If a feature changes **what you see** — colour, resolution, refresh rate, HDR, the crosshair, the monitor's own settings — it belongs.
- If a feature changes **what your PC is doing** — registry tweaks, game config files, process priority, RAM, launch options — it does not.

The pitch is *"it makes your monitor look right."*

### Why this is the right call

- **It is the only category PlexusX can own.** Colour works in every game, like CrosshairX. NVIDIA's version is buried three menus deep and NVIDIA-only; PlexusX already works on AMD through the software path.
- **The competition in "optimizer" is a race to the bottom.** The reference tool the user shared gives every toggle the same meaningless "+24 Score", and ships genuinely harmful ones — *Disable Virtualization-Based Security*, *Disable Windows Update*. Entering that category costs trust that colour earns for free.
- **It is visible.** Colour sells itself in a screenshot. A registry tweak cannot be demonstrated.
- **It never rots.** A game update can rename a convar; it cannot change what a gamma ramp does.
- **It has no anti-cheat surface.** Nothing in this direction reads or writes a game process or its files.

### The promise

Aesthetic first: **"your game looks way better."** Not "you can see people in bushes." The competitive benefit still happens; it is simply not the headline. This also retires the shadow-lift fairness argument — the product is not selling an edge.

## The shape of the app

Three tabs that work through each other, then support:

| Tab | Owns | Layer |
|---|---|---|
| **Display** | Colour: vibrance, saturation, brightness, gamma, contrast, temperature. Presets, share codes, A/B compare. | Software (gamma ramp + NVAPI) |
| **Resolution** | Resolution, refresh rate, PVP stretched presets, HDR. | Windows display API |
| **Monitor** | The physical panel: brightness, contrast, RGB gain, low blue light, colour presets. | DDC/CI over the cable |
| Crosshair | On-screen overlay. | Overlay window |
| Settings / Account | App itself, licence. | — |

The three top tabs are a stack: Display changes the signal, Resolution changes the mode, Monitor changes the panel. Same picture, three layers of control.

## What gets cut

Authorised by the user directly:

- **Games Hub, entirely** — `GamesHubPage`, `RustSettingsPage`, `Cs2SettingsPage`, `ApexSettingsPage`, `FortniteSettingsPage`, `UnsupportedGamePage`, and the per-game config writers behind them.
- **FPS Tweaks, entirely** — `FpsTweaksPage`, `SystemTweakService`, `SystemTweakCatalog`, `RegistryTweak`.
- **Launch options** — the CS2 card, gone with its page.
- **RAM cleaner and Auto High CPU Priority** — `RustSystemBoost`, gone with the Rust page. This also removes the only code that opened a handle into a game process.

Everything above is "under the hood" and therefore out by definition.

## What gets built

### 1. Monitor capability probe (startup)

Extends the existing `Capabilities/CapabilityProbe` pattern. At launch, alongside the update check, PlexusX identifies the connected monitor(s) and tests DDC/CI support, caching the result.

Windows exposes this through `dxva2.dll`:

- `GetPhysicalMonitorsFromHMONITOR` → handles per monitor
- `GetMonitorCapabilities` → what the panel actually supports
- `GetMonitorBrightness` / `SetMonitorBrightness` (VCP 0x10)
- `GetMonitorContrast` / `SetMonitorContrast` (VCP 0x12)
- `GetMonitorRedGreenOrBlueGain` / `SetMonitorRedGreenOrBlueGain` (VCP 0x16 / 0x18 / 0x1A)

**Support varies enormously** — many panels either do not implement DDC/CI or implement it badly, and laptop internal panels usually not at all. The probe is therefore not optional: the Monitor tab must know, before it draws itself, whether it can offer hardware control or must say so plainly. This is the same honesty rule the capture probe already follows.

**Low blue light** is implemented as a blue-gain reduction (VCP 0x1A), because that is standard MCCS and works across vendors. Vendor-specific "reader mode" VCP codes are not used — they differ per manufacturer and would half-work.

### 2. Game-keyed colour presets, with hover preview

Replaces the Games Hub's reason for existing, in the Display tab.

- Pick a game (CS2, Rust, …) → roughly six colour presets appear for it, each tuned to that game's palette.
- **Hovering a preset shows an enlarged preview** of how it looks, in a framed panel, not a tooltip.
- Presets apply to the whole screen — they are colour, not config, so nothing is written to any game.

### 3. Custom presets

The user's own presets live in the same section as the game presets but **visually separated** — a divider and its own caption — so a custom preset can never be mistaken for a game one or interfere with that selection.

### 4. A/B compare toggle

One control that flips between the active preset and neutral, so the difference is visible instantly. **Rate-limited** (a short cooldown) because the gamma ramp misbehaves when hammered — rapid toggling is the known failure mode.

### 5. Crosshair share codes

The crosshair becomes shareable the way Display colours already are, encoding shape, sizes, gap, colour, opacity and outline into a `PX-` code. Reuses `ProfileCode`.

## Open — decide before touching

Not authorised by the user, so **not** to be cut without a decision:

- **Keybinds tab.** Writing binds into game config files is "under the hood" by this philosophy, so it is a candidate. But the two global app hotkeys need a home if the tab goes — Settings.
- **Audio Edge.** A working peak limiter, but audio is not the monitor. It is the one feature that survives on merit while failing the philosophy.

## Risks

| Risk | Handling |
|---|---|
| DDC/CI unsupported on the user's monitor | Probe first, before any UI work. If unsupported, the Monitor tab says so honestly and the night's remaining effort moves to Display presets. |
| Cutting pages breaks the shell | The nav, `MainWindow` page switching, and `AllPagesLayoutAuditTests` all reference pages by name. Remove in one commit per page with the full suite green. |
| Cut features are missed later | Deletions are in git; nothing is lost, and the roadmap records why. |
| Setting monitor values leaves the panel wrong | Read and store the original value before the first write, and always offer a revert — the same contract the registry tweaks used. |
