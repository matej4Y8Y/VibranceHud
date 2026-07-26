# Roadmap

LIVING DOC. Changed 2026-07-26.

## Now
- System-wide vibrance 0-200% works in OBS / Discord / screen recorders (DX11 overlay at the DWM layer).
- Auto-apply per-game profiles — pick your settings once, PlexusX swaps them when the game opens.
- Games Hub knows about Rust / CS2 / Apex / Fortnite — portable Steam detection via registry + libraryfolders.vdf.

## Next
- Polish: app icon, installer branding, first-run onboarding, custom hotkey, start-minimized option, remember-last-page.
- Add 2-3 more games (CS2 first via `autoexec.cfg`; Fortnite, Apex).
- Trial + licensing via a Merchant-of-Record provider (LemonSqueezy / Paddle handle EU VAT and license keys together).
- Website (landing page, pricing, FAQ). Code-signing cert later — for now document the SmartScreen "Run anyway" click.

## Later
- AMD / Intel vibrance. The 100-200% software path already works on any GPU; adding hardware-accelerated 0-100% on AMD (ADL) and Intel would roughly triple the market.
- Crash reporting (Sentry) once ships to a wider audience.
- In-game overlay for changing vibrance without alt-tabbing — overlay only, no injection.

## What I'm not building
- Anything that risks anti-cheat accounts. No injection, no memory writes.
- Adobe-style paid subscriptions with bespoke license servers. There's a provider that does this for €3/month.
- RAM cleaner / Windows service disabling toggles. Placebo, damages credibility.

## Order
1. Branding + onboarding polish (looks worth paying for).
2. Trial + licensing.
3. Website + code-signing.
4. More games.
5. AMD / Intel.
