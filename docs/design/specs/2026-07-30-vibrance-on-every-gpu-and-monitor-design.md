# Vibrance that actually applies on every GPU and every monitor

Written 2026-07-30.

## The problem

Reported as "only my PC shows the vibrance, and one friend's — the other ~7 see nothing."
Investigation found this is not one bug but three independent ones. Two of them stop
vibrance working at all; the third only stops it being visible in screen capture.

The reporter runs a GTX 1660 with vibrance applied to the first NVIDIA display, which is
the one configuration where everything happens to work. That is why it looked machine-
specific rather than systemic.

### 1. Vibrance 0–100 is a no-op on AMD and Intel

The 0–100 range is driver-only, through NVAPI. On a non-NVIDIA GPU the app substitutes
`NullVibranceController`, whose `SetLevel` is an empty method, and `VibranceEngine`
simultaneously holds the software path neutral below 100:

```csharp
float vibrance = _vibrance > DriverVibranceCeiling
    ? _vibrance / (float)DriverVibranceCeiling
    : 1f;   // neutral - nothing happens below 100
```

Both paths are inert at once, so dragging Vibrance anywhere in 0–100 changes nothing on
screen, in capture, or anywhere else. `docs/ROADMAP.md` already lists AMD/Intel vibrance as
a known gap, but frames the fix as needing AMD's ADL SDK. It does not: `ColorAdjust.Build`
already computes a software vibrance term, it is merely gated above 100.

### 2. Driver vibrance reaches only the first NVIDIA display

`VibranceController` resolves one handle and keeps it forever:

```csharp
_display = displays[0];
```

`SetLevel` therefore writes DVC to that display only. On a multi-monitor rig the effect
does apply — just possibly to a monitor the user is not looking at, which is
indistinguishable from "it does nothing". `EnumNvidiaDisplayHandle()` ordering is not
guaranteed to match the Windows primary display, so which monitor wins is effectively
arbitrary. Multi-monitor is common among the target users.

### 3. DX11 never initialises, so nothing is capture-visible (out of scope here)

`DxDevice` requests `AlphaMode.Premultiplied` on an HWND swap chain. DXGI only permits that
on a composition swap chain, so `new SwapChain1(...)` fails with `DXGI_ERROR_INVALID_CALL`
(0x887A0001) on every machine, every launch — verified by probing all three alpha modes on
real hardware: `Premultiplied` fails, `Ignore` and `Unspecified` succeed. Every install has
therefore always run on the Magnification fallback, which no capture tool can see.

Deliberately **not** fixed in this spec. It is orthogonal (it governs capture visibility,
not whether vibrance works), and it carries a real risk that needs visual verification: the
overlay draws an opaque colour-corrected copy of the desktop, and desktop duplication
captures the composed desktop including that overlay, which can feed back and compound
saturation every frame. Bundling it here would mean debugging three things at once.

## Scope

Fix 1 and 2. Goal: moving the Vibrance slider produces a visible change on every monitor,
on any GPU vendor. Capture visibility is a separate piece of work.

## Design

### Fix 1 — software vibrance when no driver is available

`VibranceEngine` already knows whether the driver is usable, via
`_controller.IsAvailable` (surfaced as `DriverAvailable`). Use it to decide how the 0–100
range is realised, keeping 100–200 exactly as it is today:

- **Driver available:** unchanged. 0–100 goes to NVAPI DVC; above 100 the driver pins at its
  ceiling and the software term carries the rest. Existing NVIDIA users see no difference.
- **No driver:** the whole 0–200 range goes through the software matrix, so 50 renders as a
  genuine desaturation and 150 as a boost.

The conversion belongs in one pure function so it is testable without a GPU and cannot
drift between the two call sites that need it (`ApplyOverlay` and `IsIdentity`):

```
SoftwareVibranceFactor(vibrance, driverAvailable) -> float
```

Contract:
- driver available, vibrance <= 100  -> 1.0 (driver owns this range)
- driver available, vibrance  > 100  -> vibrance / 100
- no driver                          -> vibrance / 100 across the whole range

Accepted trade-off: NVIDIA's DVC is non-linear and spares skin tones; the software matrix
scales chroma linearly. AMD/Intel users therefore get a slightly different look at the same
number than an NVIDIA user does. That is worth it — the alternative is a control that does
nothing. Not worth hiding behind a vendor check or a second calibration curve; that is
tuning, and it can follow once the thing works at all.

### Fix 2 — apply DVC to every NVIDIA display

`VibranceController` keeps every handle from `EnumNvidiaDisplayHandle()` instead of the
first, and `SetLevel` writes to all of them. `CurrentLevel`/`DefaultLevel` continue to read
from the first handle — they exist to seed the UI with one number, and a rig whose monitors
disagree is not a case worth modelling.

One display failing must not stop the others: a handle can go stale when a monitor sleeps or
is unplugged. Per-display writes are attempted independently and failures skipped, matching
the tolerance introduced for per-monitor DX resources.

## Testing

Fix 1 is pure and gets direct unit tests: each branch of the contract above, plus the
neutral-at-100 boundary in both modes, plus `IsIdentity` agreeing with the factor so a
no-driver machine at vibrance 100 still counts as neutral and does not leave the overlay
running for nothing.

Engine-level tests with a fake controller reporting `IsAvailable = false` assert that
changing vibrance below 100 now produces an overlay write, where previously it produced
none. That test is the regression guard: it fails against the current code.

Fix 2 is tested through a fake NVAPI seam covering multiple displays, one display throwing,
and confirming every display is written rather than just the first.

## Verification

`dotnet test` green, then the real check, which no test can stand in for: run the app and
confirm on a live screen that vibrance below 100 visibly changes the display on a machine
with no NVIDIA driver, and that both monitors change on a multi-monitor NVIDIA machine.
The reporter's own machine can only verify the NVIDIA multi-monitor half; the AMD half needs
one of the friends on AMD to confirm.
