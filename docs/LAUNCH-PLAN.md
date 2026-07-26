# Launch plan — phase by phase

Not just "what's on the list" but what order, what unlocks the next phase, and how to know each phase actually worked before moving on.

## Phase 0 — Stop the bleeding

Duration: 1–2 days. No money changes hands.

Before anything, fix the things that already make PlexusX look unfinished:

- Sign the installer with a real EV code-signing certificate. The download "Unknown publisher" warning is the #1 reason free trial users don't convert — they delete the file.
- App icon. Boring icon = hobby-product impression. Spend a day or commission a one-line mark.
- First-run onboarding screen. Three tips, one Continue button, then the app. 30 seconds of friction removed.
- Friendly empty states in places like Games Hub that say "We didn't find any supported games — is Steam installed?"

Gate: a stranger installs and uses PlexusX for 30 seconds without abandoning or asking "is this safe?". If they do, this phase isn't done.

## Phase 1 — Free public beta (no money yet)

Duration: 1–2 weeks. You're gathering signal.

Push 0.8.0 as the **public free build**. Force update from 0.7.x so the existing low-installbase gets the new installer with the cert.

- Public landing page. Free. One page. Hero, screenshot, download button, FAQ, link to Discord. Look at how LosslessScaling's page does it — short scroll, one button, all the trust signals (no SmartScreen, exact install size, working on Win 10/11, latest version on top).
- Discord server. Free. Pin a #bugs channel, #suggestions, #general. You don't need a moderation team for week 1 — the first 20 users set the tone.
- Auto-update channel pointing to GitHub Releases with Velopack semantics. Already mostly done.
- Build-in-public: post release notes to a TikTok or similar short-form channel for once-a-week office-hour vibe. One minute tops, here-are-the-new-pieces. Not a pitch.

Gate: 100 real installs from non-friends. If you can't get 100, the messaging or the targeting is wrong. The product isn't broken — the distribution is.

## Phase 2 — Paid launch (€3/month)

Duration: 3–5 weeks from Phase 1 finish.

- Merchant of Record. LemonSqueezy is the lightest. Paddle if you want more control. They handle EU VAT, sales tax, refunds. The MoR is the only sane way to charge EU users without becoming a tax nexus in 27 countries.
- PlexusX-Pro tier €3/month, PlexusX-Free tier stays 100% free and never expires. The vibrance tool is the freebie that drives installs and word-of-mouth. The games hub / per-game profiles is the upgrade. Don't shrink the free tier in v1.
- License-key flow. On launch, the app checks a signed license file. Local cache so the app works offline for 7 days. If you're charging €3/month, you do NOT need a server yourself — the MoR can issue keys and you ship the activation endpoint as a Cloudflare Worker or simple function.
- Trial window: 30 cumulative minutes of running-time, not wall-clock. So people can leave it installed and forget, and only the time they actually see the app counts.
- 30-day refund window honored through the MoR.
- Privacy + EULA + terms links in the installer AND on the landing page. Not buried in an About dialog.

Gate: 10 paid users. €30/month total revenue. That's the threshold where the MoR's economics start to make sense for ongoing support.

## Phase 3 — First paid cohort feedback

Duration: ongoing, monthly cycles once Phase 2 is in.

Now the work shifts from ship-instrument to keep-instrument. The first 30 paid users are your research lab.

- Track: NPS score on a 5-second survey when Pro users upgrade. Email-style, not in-app.
- Track: actual churn. Who refunds, who keeps paying 6 months? The refund window is 30 days so your real signal is renewals 60+ days in.
- Track: which Pro features get used. If per-game profiles are 90% of session time but crosshair is 0.1%, you know what to invest in next.
- Watch Discord. People who complain about a feature become people who ask "when are you fixing it" become people who pay for Pro when you actually fix it.
- One standing rule: a bug report from a paying user closes in 48 hours or you message them with a status. Free users get 7-day reaction time. This is the difference between hobby and product.

Gate: 50 paying users OR clear evidence the Pro feature mix is wrong. Either way you have signal now.

## Phase 4 — Growth, not features

Duration: 2–6 months from Phase 3.

Stop adding features. The first three growth levers are all about distribution.

- **Referral**: "tell a friend, get a month free" via the MoR. LemonSqueezy has this built-in; Paddle needs a small wrapper.
- **Bundle**: when 3 of your friends are using PlexusX-Pro, each gets one more month free. Network effect at small scale.
- **Content**: one feature tour video per quarter on TikTok / YouTube Shorts. "How to make Rust colors look like Fortnite in 30 seconds with PlexusX." That's the actual demo that goes viral.

What NOT to do here: don't add a feature you can't answer "what problem does this solve for a paying user?". Free-tier feature creep is how products stay small.

Gate: growth-rate compounds. If you're growing 5%/week organically, the loops work. If you need paid ads, your message-market fit is wrong.

## Phase 5 — Stability, AMD/Intel, more games

Now you have paying users + signal + revenue. Phase 5 is about removing the ceiling.

- AMD / Intel vibrance. 40% of the PC market was locked out. Now they're reachable.
- Add 2–3 more games to the Games Hub. CS2 first.
- Steam Deck support. Just making sure it installs and runs without Wine tweaks.
- Telemetry: anonymous usage stats (opt-in, default off). "How many people use feature X per session?" is the question that decides what to build in Phase 7.

What NOT to do: rebuild the UI. The PlexusX UI is working — every design poll, you risk breaking what works.

Gate: 200 paying users or €600/month recurring. Now you're a side business, not a side project.

## Timeline sketch

This is rough. Real timelines depend on your time, how fast you ship, whether the beta teaches you new things.

| Phase | Duration | Cumulative |
|---|---|---|
| 0 stop the bleeding | 1–2 days | day 2 |
| 1 free public beta | 1–2 weeks | week 4 |
| 2 paid launch | 3–5 weeks | week 12 |
| 3 first paid cohort | ongoing | week 12+ |
| 4 growth | 2–6 months | month 6 |
| 5 stability + AMD | 2–3 months | month 9 |

Realistically: from where you are now, this is a **6–9 month arc** to "stable side business at €200–600/month." It is not a week. That is the honest answer.

## What this plan assumes

- PlexusX stays solo. You do it nights/weekends. No co-founder, no team. Phase 5 is the first phase where a part-time contractor makes sense (the AMD/Intel work).
- €3/month stays €3/month. A higher price point requires a different value proposition (an actual server, real-time features) that changes what you're building.
- You stay with LemonSqueezy or Paddle. Don't switch license providers mid-cycle.
- The TikTok channel (your existing 420 followers) is the only marketing channel you need for Phase 1–3. That's because Phase 0–2 is about getting the message out to people who already have one eye on PlexusX.

## What changes the plan

- A user asks for a feature you can't ignore (Phase 1–2 signals will surface these). Specifically: anything that turns PlexusX from "vibrance tool" into "FPS booster" gets priority over AMD support.
- A competitor launches something that pulls ahead. Specifically: BrightRaider adding per-game auto-switch (they already have per-game vibrance auto-apply). Watch them weekly during Phase 1.
- EU law changes. The GDPR + EU 14-day refund rules update occasionally. Re-check once a year.
- A new MOAT exists that I don't know about. Watch for users saying "this is way better than [competitor]" and ask why.
