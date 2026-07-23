# Vibrance HUD — Product Roadmap & Future Direction

*Written 2026-07-23. A living document — revise as priorities shift.*

## Vision

Vibrance HUD is growing from a single-purpose vibrance tool into a **premium, trustworthy
"game visuals & optimization" utility for PC gamers**. The wedge is system-wide digital
vibrance (0–200%); the expansion is a **Games Hub** that safely optimizes per-game
settings. The whole thing is wrapped in a polished purple UI (animated plexus + frosted
glass) that signals quality.

**Positioning (important):** legit and safe. We only edit games' own config files (never
inject, never touch anti-cheat), we always back up, and we're transparent about what we
change. That's the opposite of the sketchy "undetected"/cheat tools in this space — and
it's our trust advantage when charging money.

## Where we are (shipped)

- System-wide vibrance 0–200% (NVAPI for 0–100, Windows Magnification color matrix for
  100–200 — the latter works on any GPU).
- Big windowed app: left nav (Vibrance / Games / Settings / Account), animated purple
  plexus background, iOS-style frosted-glass panels.
- Games Hub: portable Steam detection (registry + libraryfolders.vdf across drives),
  **Rust** optimizer editing `client.cfg` (graphics quality, FPS limit, effect toggles,
  tools) with automatic backup and a "Rust is running" guard.
- Auto-update via Velopack + GitHub Releases; 29 unit tests on the pure logic.

## Near-term (next few sprints)

1. **Per-game vibrance profiles with auto-apply** — the killer feature that ties vibrance
   and the Games Hub together. Detect when a game launches (watch its process), apply that
   game's saved vibrance level automatically, restore on exit. Turns two features into one
   coherent product.
2. **Product polish before charging** — custom app icon + tray icon + installer branding,
   a first-run onboarding screen, custom hotkey rebinding (deferred earlier),
   start-minimized option, remember-last-page.
3. **Rust depth (finish Phase B for Rust)** — resolution / window-mode (needs the Unity
   registry PlayerPrefs spike), a keybind viewer, more convar tweaks, and curated presets
   ("Competitive / FPS", "Cinematic") that bundle vibrance + Rust config in one click.

## More games (Phase B)

Each new game = a `SupportedGame` entry + a config provider following the Rust pattern.
Config-file editing only; never anything anti-cheat-sensitive.

- **CS2** — `autoexec.cfg` / launch options; low risk, huge audience.
- **Fortnite** — `GameUserSettings.ini`.
- **Apex Legends** — `videoconfig.txt`.
- **Valorant / LoL** — *caution:* Riot Vanguard is aggressive; limit to safe config files
  or defer. Do not risk users' accounts.
- **Generic Steam game** — even without deep config support, offer a vibrance profile +
  Steam launch-options helper for any detected game.

## Trial + licensing (Phase C)

- 30-minute session trial → lockout screen ("trial ended — buy to continue").
- **Don't build a bespoke license server.** Use a provider that does license keys +
  payments together (see below) so activation is a simple API call + local grace period.
- Decide the trial model: 30 min per launch vs. 30 min/day vs. total. Per-session is
  simplest and generous enough to convert.
- Consider a **free tier = vibrance only**, **paid = Games Hub**. A free, genuinely useful
  vibrance tool drives installs and word-of-mouth; the Games Hub is the upsell.

## Website + distribution (Phase D)

- **Landing page**: hero (with the plexus look), feature list, pricing, download, FAQ,
  Discord, changelog, reviews. Host free on Vercel / Netlify / GitHub Pages.
- **Code-signing certificate (~€100–400/yr)** — removes the SmartScreen "unknown
  publisher" warning. Worth it once there's revenue; it materially raises trust for a paid
  download. (Until then, document the "More info → Run anyway" step.)
- Installer already comes from Velopack; add branding and keep GitHub Releases as the
  update feed. Consider the Microsoft Store later for reach.

## Monetization (Phase E)

- ~3 EUR/month. Recommended provider: **LemonSqueezy or Paddle** — both are Merchant of
  Record, so they handle **EU VAT** for you (critical for a solo EU dev) *and* provide
  **license-key management**, covering Phase C in the same tool. (Gumroad is simpler but
  less flexible; Stripe is powerful but leaves VAT + licensing to you.)
- Offer monthly + discounted annual; maybe a lifetime tier for early adopters.

## Bigger bets (later)

- **AMD / Intel vibrance** — today 0–100 vibrance is NVIDIA-only (NVAPI); the 100–200
  software path already works anywhere. Adding AMD (ADL) / Intel saturation would roughly
  triple the addressable market. High-value, medium effort.
- **Opt-in crash reporting** (e.g. Sentry) once shipping to strangers — you can't watch
  every machine; you need to see crashes.
- **Overlay for in-game tweaks** — the popup we retired, reborn as a proper in-game
  overlay for changing vibrance without alt-tabbing (careful with anti-cheat: overlay only,
  no injection).

## Recommended order

1. Per-game auto-apply vibrance profiles (make the product cohere).
2. Branding + onboarding polish (look worth paying for).
3. Trial + licensing via LemonSqueezy/Paddle (turn it on).
4. Website + code-signing (distribute + trust).
5. 2–3 more games (CS2 first).
6. AMD/Intel vibrance (expand the market).

## Guardrails (don't compromise these)

- Config-file edits only. No injection, no memory editing, no anti-cheat risk. Always back
  up before writing.
- Portable detection — must work on every PC, never hardcode paths (already covered by
  tests; keep it that way).
- Keep the pure logic TDD'd as the surface grows.
