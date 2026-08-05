# PlexusX 1.0 — the systems, not the features

Written 2026-08-05.

Goal: a version that can be sold to strangers. Not "works on the developer's PC" — works on
theirs, and where it can't, says so plainly instead of looking broken.

## The diagnosis

Every defect found in this session was the same defect wearing a different hat.

| Symptom | Underlying cause |
|---|---|
| App told users recordings can't show their colours — while OBS showed them | Claim was hardcoded, never measured |
| Gamma refusal is detected and never surfaced | Nothing reads the flag |
| HDR never checked anywhere | Capability never considered |
| Blurry at 125%/150% | One scale factor assumed |
| Share codes built, then hidden in Settings | Feature shipped without a route to it |

**The app does not know what is true on the user's machine, and does not report what it does
know.** That is one missing system, observed five times — not five bugs.

The commercial cost is concrete: a customer whose advanced colour silently does nothing has
no way to tell a broken product from an unsupported PC, and refunds.

## The five systems

### 1. Know the machine

A probe at startup that **tests rather than assumes**. It answers:

- **Is the gamma ramp actually writable?** Apply a known non-identity ramp, read it back,
  compare, restore. A read-back is the only honest test: `SetDeviceGammaRamp` returns success
  while Windows silently clamps how far a ramp may deviate, so "it returned true" does not
  mean "it applied".
- **Is HDR on?** HDR is a common reason ramps are ignored, and it is never checked today.
- **GPU vendor, and is driver vibrance available?**
- **How many monitors, and do they share a scale factor?**
- **Are we elevated?** (FPS tweaks need it.)
- **Which overlay path came up?** (Already known; folded in so there is one source.)

Re-probed every launch. Hardware, drivers and HDR all change between runs, and a cached
answer is how an app starts lying again.

### 2. Never lie

Every claim in the UI traces to something measured. No hardcoded verdicts about what a PC can
do. Where the app cannot know, it says it does not know.

This is already half-done: `CaptureStatus` was rewritten this session after two users
disproved its central claim. The rule generalises — a claim with no measurement behind it is
a bug, whether or not anyone has caught it yet.

### 3. Never fail quietly

If a control did not take effect, it says so **where the user set it**, not in a log.

The concrete case: advanced colour is built entirely on the gamma ramp. On a machine that
refuses or clamps ramps, every one of those sliders moves, updates its number, and changes
nothing. The user sees a broken product. With the probe, that section states the reason and
offers what to do about it.

### 4. Spread

Three mechanisms, none needing a server:

- **Share codes** — done; moved to Display this session.
- **Before/after share card** — one click produces a branded image with the code burned in.
  Posted anywhere, it advertises and it is self-serve: the code is legible in the picture.
- **Invite tag** — a short, non-reversible tag derived from the user's licence.

### 5. Learn

The capture bug survived because 8 of 20 testers reported it and it was recorded as a
mystery rather than treated as evidence. The diagnostic report already exists and is already
privacy-clean (no name, no machine id, no key, no paths). What is missing is that the probe
results belong in it, and that sending it should be one click.

## Scope and order

This is more than one work-week. It decomposes into three, in this order:

**A. Truth and capability (systems 1–3).** First, because until it exists any further feature
work risks shipping something that silently dies on a customer's PC. Highest commercial value:
it is the difference between a product and a demo.

**B. Finish the polish.** Advanced colour UI, focus rings, keyboard navigation, tooltips,
pages that reflow to a wide window. Tracked in the existing 1.0 plan.

**C. Growth and feedback (systems 4–5).** Cheapest of the three, and worth more once the
product actually works everywhere.

Each part produces working, sellable software on its own. A is specified below; B and C keep
their existing plan entries.

## Part A design

### MachineCapabilities

A record of what was measured. Immutable, serialisable into the diagnostic report.

```
GammaRamp      : Working | Clamped | Refused | Untested
HdrActive      : bool
GpuVendor      : Nvidia | Amd | Intel | Other | Unknown
DriverVibrance : bool
MonitorCount   : int
MixedDpi       : bool
Elevated       : bool
OverlayPath    : Dx | Mag
```

`Clamped` is a distinct state from `Refused` on purpose. Refused means nothing happened;
clamped means part of the curve applied. They need different wording, and collapsing them
would tell a user with a working-but-limited screen that their hardware is unsupported.

### CapabilityProbe

One class, one public method, returning `MachineCapabilities`. Every individual test is
independently testable through an injected seam, following the pattern `DisplayGammaRamp`
already uses for `SetDeviceGammaRamp`.

Runs once at startup, after settings load and before the main window is built, so the window
can style affected controls correctly the first time rather than correcting itself.

Never throws. A probe that crashes the app it is meant to make robust is worse than no probe;
every test degrades to `Unknown` on failure.

### Surfacing

Two places, no more:

1. **Inline, on the affected control.** The advanced colour section states it when the ramp is
   not usable. This is where it matters, because it is where the user is when they notice.
2. **One "System check" card in Settings**, listing what was measured and what it means.

Deliberately not a startup dialog. A modal on launch telling someone their PC is partly
unsupported is how a first run gets uninstalled.

### Testing

The gamma read-back, the clamp detection and the state mapping are pure logic behind seams and
are unit-tested. HDR detection and monitor enumeration are thin wrappers over Win32 and are
verified by hand — they are the kind of code where a test would only assert that the mock was
called.

The existing 962 tests stay green.

## Out of scope

**Windows Graphics Capture probing.** The right fix for the capture diagnostic's blind spot,
but it needs WinRT projections and a target-framework change. Recorded in the plan; not part
of A.

**AMD/Intel driver vibrance.** The real answer for AMD users whose colours miss Discord screen
share. A genuine feature, on the roadmap, not a system.

**Referral attribution.** Needs a backend. The invite tag in C is app-side only.
