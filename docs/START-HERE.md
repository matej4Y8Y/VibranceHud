# PlexusX — start here

A Windows display/performance tool for gamers. C# / .NET 8 / WinForms. Everything is
owner-drawn, so it doesn't look like a stock Windows app.

## Build it

```
dotnet build VibranceHud.csproj          # debug build
dotnet test tests/VibranceHud.Tests      # 519 tests, all green
dotnet publish VibranceHud.csproj -c Release -o publish
```

Installer needs Inno Setup 6:
```
"%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" VibranceHud.iss
```

Version lives in **two** places and they must match: `<Version>` in `VibranceHud.csproj`
and `#define AppVersion` in `VibranceHud.iss`.

## Layout

| Area | Files |
|---|---|
| Colour engine | `VibranceEngine.cs`, `ColorAdjust.cs`, `SaturationMatrix.cs` |
| Display output | `MagOverlay.cs` (active), `DxOverlay.cs` + `DxDevice.cs` + `DxCapture.cs` (disabled) |
| Driver vibrance | `VibranceController.cs` (NVAPI via NvAPIWrapper) |
| UI | `MainWindow.cs`, `Pages/*`, `Controls/*` |
| Licensing | `License/*`, generator in `Tools/KeyGenerator` |
| FPS tweaks | `SystemTweaks/*` |
| Game configs | `Rust/*`, `Games/*` |

Two overlay paths exist. **Only MagOverlay runs.** DX11 is deliberately disabled — see below.

## The open problem: colours don't appear in recordings

This is the big one and it is unsolved.

**Symptom:** the colour effect is visible on the user's own monitor but missing from OBS,
Discord screen share and Medal.

**Cause:** everything the app currently uses applies colour *after* the frame is composed —
the Magnification API effect and NVIDIA Digital Vibrance both land at scanout. Capture APIs
(DXGI Desktop Duplication, Windows.Graphics.Capture) read the composed desktop, which is
earlier in the pipeline. So the tint is added after the copy is taken.

**Measured, not assumed.** Desktop Duplication average chroma:

| | chroma |
|---|---|
| baseline, app closed | 9.4 |
| DX overlay, saturation 200% | 6.8 (no change) |
| driver vibrance 50 → 97 | 18.7 (doubled — but this reading is confounded, screen content differed) |

**What's been tried:**

1. `DxDevice.cs` requested `AlphaMode.Premultiplied` on an HWND swap chain. DXGI only allows
   that on a *composition* swap chain, so `SwapChain1` failed with `DXGI_ERROR_INVALID_CALL`
   on every machine — DX11 had never once initialised in the field.
2. Switching it to `AlphaMode.Ignore` makes DX11 initialise, but the overlay then **renders
   nothing visible** and takes over from MagOverlay, so the app does nothing at all. Reverted.
   The enum is left wrong on purpose with a comment explaining why — read it before "fixing" it.
3. `CompositionKeeper.cs` holds a 1×1 topmost window to stop Windows using Independent Flip
   (which bypasses DWM entirely, so capture misses it). Correct mechanism, no effect on its
   own, because the Magnification effect isn't in composition to begin with. Confirmed no
   change by a user on a 3060 Ti.

**Two blockers for whoever picks this up:**

- A borderless fullscreen flip-model swap chain gets promoted to Independent Flip and skips
  DWM. `CompositionKeeper` prevents that, and becomes necessary once the overlay works.
- **The feedback loop.** To tint the screen the app reads the screen. If its output is in the
  composed desktop, the next read includes its own output, and saturation compounds each frame.

**Most promising idea, untried:** capture the *game window* rather than the whole desktop
(`Windows.Graphics.Capture` on a specific HWND). The overlay's own output is then not in what
it reads, which breaks the loop, while the overlay stays composited and therefore recordable.

## Other things worth knowing

- **Vibrance 0–100 is NVIDIA-only** through NVAPI. AMD/Intel fall back to the software colour
  matrix. Adding AMD ADL / Intel driver support is the open item there.
- **Licence keys are forgeable.** The signing secret is symmetric and ships inside the binary
  (`License/LicenseKeyDerivation.cs`), so anyone with the installer can mint valid keys — this
  was demonstrated, not theorised. The fix is asymmetric signing (Ed25519): private key stays
  with the developer, only a verify key ships.
- **`AlphaMode` in `DxDevice.cs` is wrong on purpose.** There is a long comment saying so.
- Design notes and history: `docs/design/specs/`, `docs/HANDOFF.md`, `docs/ROADMAP.md`. Commit
  messages carry the reasoning for most decisions.

## House rules

- Tests first for anything with logic. Pure logic is separated from I/O precisely so it can be
  tested without a GPU — follow that split.
- Don't add tweaks that only look impressive. Every entry in `SystemTweaks` has a documented,
  measurable effect, and that's the bar.
- No injection into game processes, no memory writes. The product's whole positioning is
  anti-cheat safety, and an EV code-signing certificate depends on not being classified as a
  cheat tool.
