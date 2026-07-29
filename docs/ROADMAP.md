# Roadmap

LIVING DOC. Changed 2026-07-26.

## Before launch (must-do, blocks charging money)

These are blocking. Don't take a single cent until they're done.

**Trust + safety**
- EV code-signing certificate (~€200–400/yr). Without it users see "Unknown publisher" and won't run the installer.
- Privacy policy on a public URL (GDPR if you ever serve an EU user, which you will).
- Terms of service / EULA shown by the installer.
- Refund / cancellation policy linked from the product page (EU 14-day right of withdrawal).
- `LICENSE` file in the repo root (NVAPIWrapper is LGPL; SharpDX is MIT — both need declaring).
- 3rd-party licenses list somewhere visible.

**Selling infrastructure**
- Merchant-of-Record account (LemonSqueezy or Paddle). They handle EU VAT, sales tax, refund disputes — you just get paid.
- Product listed at the MoR with a price (€3/month was the original target).
- License-key activation flow in PlexusX. Without this, anyone who downloads the paid version uses it forever.
- In-app "Go Pro" button that deep-links to checkout.
- Trial logic (30-min cumulative lockout, per the original HANDOFF spec).
- Webhook so paid users get a license key without manual intervention.

**First-impression polish**
- App icon for installer, start menu, tray, splash — one designer day. A placeholder icon = hobby-product impression.
- First-run onboarding screen. Trial → paid conversion happens in 30 seconds.
- Friendly empty states ("No games detected — is Steam installed?").
- No raw exception dialogs. Friendly "something broke" dialog with a report link.

**Distribution surface**
- Landing page (Vercel/Netlify free tier). Plain. Hero, feature list, pricing, download, FAQ.
- Discord community invite linked from the app + page.
- Bug report channel (GitHub issues are fine for v1).

**Pre-launch smoke**
- Clean-install QA run (Win 10 fresh, no dev tools).
- Upgrade path: 0.7.2 installed → install 0.8.0 → settings preserved.
- Uninstall + reinstall: trial returns to 0 min, license re-arms correctly.
- 5–10 friend beta. People who aren't me catch what I can't see.

## Now
- System-wide vibrance 0-200% works in OBS / Discord / screen recorders (DX11 overlay at the DWM layer).
- Auto-apply per-game profiles — pick your settings once, PlexusX swaps them when the game opens.
- Games Hub knows about Rust / CS2 / Apex / Fortnite — portable Steam detection via registry + libraryfolders.vdf.

## Next
- Polish: app icon, installer branding, first-run onboarding, custom hotkey, start-minimized option, remember-last-page.
- Add 2-3 more games (CS2 first via `autoexec.cfg`; Fortnite, Apex).
- AMD / Intel vibrance. The 100-200% software path already works on any GPU; adding hardware-accelerated 0-100% on AMD (ADL) and Intel would roughly triple the market.
- Crash reporting (Sentry) once ships to a wider audience.
- In-game overlay for changing vibrance without alt-tabbing — overlay only, no injection.

## What I'm not building
- Anything that risks anti-cheat accounts. No injection, no memory writes.
- Bespoke paid subscriptions with our own license server. There's a provider that does this for €3/month.
- RAM cleaner / Windows service disabling toggles. Placebo, damages credibility.

## Recommended order

1. Code-signing certificate (block SmartScreen).
2. Privacy + EULA + LICENSE files (compliance).
3. MoR account (LemonSqueezy).
4. Trial + license activation in PlexusX.
5. App icon + first-run onboarding.
6. Landing page + Discord.
7. Beta with 5–10 friends.
8. Public launch.

Roughly 50–80 hours across the items, mostly the legal/business side.
