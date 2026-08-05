# PlexusX — brand, audience and growth

Companion to `LAUNCH-PLAN.md`. That one is *what to ship and when*. This one is *who we are,
who buys it, and how it spreads*.

---

## 1. The spine

One sentence, already written in `1.0-from-community.md` without being named as the brand:

> **We change your display, never the game.**

Keep it. It is doing four jobs at once:

- **It kills the ban fear.** This audience's first question about any overlay is "will this get
  me banned". The sentence answers it before they ask.
- **It explains the tech** to someone who doesn't care about the tech.
- **It decides features for us.** It is exactly the reasoning used to reject "bright night" —
  raising the monitor's real brightness is something anyone can do with the buttons on their
  screen; remapping black to green is not. The line held, and it will hold again.
- **It is a promise a competitor can't casually copy**, because most of them *do* touch the game.

Everything — site, video, Discord, the app's own copy — ladders up to that sentence.

## 2. The enemy is a belief, not a company

From the community log, and it is the single most important marketing fact we have:

> "many people believe nvidia is the same"

We are not fighting BrightRaider. We are fighting the belief that the NVIDIA Control Panel
already does this. Every piece of marketing has one job: break that sentence.

The only thing that breaks it is **showing, not telling**:

- Side-by-side, same scene, NVIDIA at max vs PlexusX. If we can't beat it visibly, we don't
  have a product — and we do, because DVC stops at 100 and saturation doesn't.
- "NVIDIA's slider stops here. Ours doesn't." with the notch on the slider doing the talking.
- Per-game auto-switching. NVIDIA cannot do this at all. This is the argument that actually
  wins, because it isn't a matter of degree.

**Never argue with words what a five-second clip can settle.**

## 3. Who actually buys this

Not "gamers". Specifically:

| | |
|---|---|
| Age | 16–26, overwhelmingly male |
| Games | Rust, CS2, Apex, Fortnite, Valorant, Marvel Rivals |
| Spends on | Skins, peripherals, Discord Nitro, small utilities. €3–15 is nothing to them |
| Trusts | Streamers, their own friend group, before/after clips. Not ads, not press |
| Fears | Getting banned. Viruses. Looking like they need help to be good |
| Finds things via | TikTok/Shorts, YouTube "pro settings" videos, Discord, Steam |

Two consequences that most people get wrong:

1. **They do not read.** They watch. A 12-second before/after outperforms any landing page copy
   we will ever write.
2. **They buy in groups.** One person in a friend group finds a thing, and within a week the
   whole squad has it. Optimise for the *group*, not the individual — see §5.

## 4. Pricing: go one-time, and go on Steam

This is the biggest recommendation in this document, and it contradicts the current plan.

**The comparable is almost exact.** [Lossless Scaling](https://store.steampowered.com/app/993090/Lossless_Scaling/)
is a solo-developed display/overlay utility for gamers, sold on Steam for $6.99 one-time.
It has [2–5 million owners](https://steamspy.com/app/993090) and has taken roughly
[$7.5M gross / $2.2M net to the developer](https://steam-revenue-calculator.com/app/993090/lossless-scaling).
Same audience, same shape of product, same one-person team.

Why one-time beats €3/month here:

- **Set-and-forget utilities churn badly on subscription.** People configure it once and forget
  it is running. Month four, they see €3 on a statement and cancel — not because it stopped
  working, but because it stopped being visible. The industry read is that
  [one-time purchase is the rational model for local tools](https://www.strayspark.studio/blog/one-time-purchase-vs-subscriptions-indie-studios),
  and that subscriptions depress willingness to pay.
- **Churn management is a job.** A solo dev doing nights and weekends does not have a
  retention function. One-time removes the entire problem.
- **Steam solves the trust problem for free.** The launch plan's Phase 0 item #1 is the
  "Unknown publisher" SmartScreen warning killing conversion. On Steam that problem does not
  exist. That alone is worth the 30% cut.
- **Steam is a discovery engine**, not just a shop. Wishlists, "players also bought", reviews as
  social proof, seasonal sale spikes. None of that exists on a self-hosted landing page.
- **Steam handles EU VAT, refunds and payments**, which is the entire reason the plan reaches
  for a merchant of record anyway.

**Recommended shape:**

- **Free forever:** vibrance + saturation. This is the install driver and the word-of-mouth
  engine. Never shrink it.
- **PlexusX Pro — one-time, €11.99 on Steam** (€7.99 launch week). Games Hub, per-game auto
  profiles, crosshair, FPS tweaks.
- **Keep direct sales** on the site for people who don't want Steam, same price. Steam is the
  front door, not the only door.
- **Fix the price mismatch first.** The channel says €3/mo and €8–12 lifetime; the site says $4
  and $12. People screenshot that. Pick one and change everything on the same day.

The counter-argument: €3/mo × 12 = €36/year beats €11.99 once. True *only* if they stay twelve
months, and for a utility like this most won't. One-time at scale beats subscription at a
trickle — and scale is what Steam gives us.

## 5. The growth engine

Four loops, ranked by leverage for a solo dev.

### Loop 1 — Profile codes (the one they asked for)

**This is already built.** `PX-XXXXXXXXX` encodes a whole look. It is the strongest asset in
the product and it is being treated as a footnote.

The loop:

1. Someone tunes a look they love.
2. They post the code — in Discord, in a TikTok comment, in a video description.
3. A friend pastes it and gets that exact look in one second.
4. **The code is useless without the app.** Installing is the only way in.

That is the "friend without it is left out" mechanic, and it is honest — the exclusion is real
utility, not artificial scarcity. Do not build fake scarcity on top of it. This audience can
smell it, and our whole brand is honesty (§7).

To make it much stronger:

- **Put the code in the screenshot.** Any screenshot or capture taken from the app carries its
  code in the corner. Every share becomes an ad with a working install path attached.
- **Named creator codes.** `PX-SHROUD-RUST`. Give creators a vanity code. Now their audience
  types *their* name into our product.
- **A public gallery** — "Rust looks", sorted by uses this week. Codes become content, and the
  gallery is a page Google can index for "best rust colour settings".
- **Show usage in-app:** "4,812 players are using this profile." Social proof at the moment of
  decision.

### Loop 2 — Short-form video

Already the channel (420 followers). The format is solved and it is the *only* format:
**before/after, same scene, under 15 seconds, no talking.**

- One clip per game. Rust first — it is the flagship and the ugliest by default, so the
  delta is biggest.
- Title with the search term, not the brand: "Rust actually looks like this", not "PlexusX v1.0".
- Pin the profile code in the first comment. Loop 1 and Loop 2 feed each other.
- Post the same clip to Shorts and Reels. It costs nothing and the algorithms don't overlap.

### Loop 3 — Creator seeding

Free lifetime keys, no strings, to streamers with 500–5,000 viewers playing our games. Not the
big ones — they ignore you and their audience is too broad. Mid-tier streamers of *one* game
have exactly our audience and will show a tool on stream because it is genuinely interesting
to watch someone tune colours live.

Ask for nothing. If it's good they'll mention it, and chat will ask "what's that".

### Loop 4 — The Discord

Not a support desk, a place people want to be. Three things that matter:

- **A #looks channel** that is nothing but profile codes and screenshots. This is Loop 1 with a
  home.
- **A visible owner role.** People who bought Pro get a coloured role. Status is the product.
- **Public roadmap and public failures.** See §7.

## 6. Community: build a scene, not a userbase

The goal is that PlexusX becomes *the thing your squad uses*, the way a crosshair config or a
sens number is. That happens when the product has social surface area:

- **Weekly look contest.** Best code posted each week gets pinned and put in the app's gallery.
  Costs nothing, generates content forever.
- **Squad unlock.** When three friends own Pro, all three get something — a badge, an exclusive
  look pack, early access to a new game profile. The launch plan already gestures at this;
  make it visible and social rather than a billing discount.
- **Name the testers.** Nine real testimonials already exist. Put their names and their codes
  on the site. People who are credited become people who recruit.
- **Ship what they asked for, and say who asked.** "Added because @x asked for it in #suggestions"
  in the release notes. Nothing else makes a community feel ownership that cheaply.

## 7. The moat: be the one that tells the truth

This market is full of "FPS boosters" whose toggles do nothing. Our differentiator is already
in the codebase and nobody has noticed it is a *brand*:

- The app currently says, out loud, that colours don't show in recordings yet.
- A tweak that turned out to be backwards was removed rather than quietly left in.
- The planned benchmark feature is explicitly **allowed to report that a tweak did nothing.**

That is a positioning nobody in this category can copy without exposing themselves. Lean on it
hard:

> **Every other optimiser shows you a number going up. We show you the frametimes, before and
> after, and we let it say the tweak did nothing.**

Practical rules:
- Never claim a feature works on hardware we haven't tested it on. The site currently
  overclaims on GPUs — saturation is any GPU, vibrance is NVIDIA only. Fix it. An AMD buyer who
  finds out after paying is a refund *and* a bad post.
- When something breaks, post about it before users do.
- Publish the "doesn't work yet" list on the site. It will win more trust than the feature list.

## 8. What not to do

- **No fake scarcity, no fake countdowns, no fake user counts.** One screenshot of a lie
  undoes everything in §7.
- **No feature creep in the free tier.** Free is vibrance. That's the hook.
- **Don't rebuild the UI.** It works. Every redesign risks what already converts.
- **Don't chase the big streamers** before the product is boringly stable. One bad public
  crash at 30k viewers is worse than no exposure.
- **Don't add anything that reads as an advantage** rather than a preference. The moment a
  moderator sees us as a cheat, the brand is finished. The §1 line is the test.

## 9. First 30 days

| Week | Do |
|---|---|
| 1 | Pick one price and fix it everywhere. Fix the GPU overclaim on the site. Get the 9 testimonials with real names. |
| 2 | Start the Steam page. Wishlists accumulate while you finish. Screenshots + a 20s before/after trailer. |
| 3 | Ship the code-in-screenshot + #looks channel. Make Loop 1 real. |
| 4 | Seed 20 mid-tier streamers of Rust/CS2. One clip per game on TikTok. |

Success signal at day 30: **profile codes being posted by people you don't know.** That is the
loop turning on its own. Installs and revenue follow it; they do not lead it.

---

## Sources

- [Lossless Scaling on Steam](https://store.steampowered.com/app/993090/Lossless_Scaling/) — the comparable
- [SteamSpy: Lossless Scaling owners](https://steamspy.com/app/993090)
- [Steam revenue estimate](https://steam-revenue-calculator.com/app/993090/lossless-scaling)
- [Why one-time purchase tools outperform subscriptions for indie studios](https://www.strayspark.studio/blog/one-time-purchase-vs-subscriptions-indie-studios)
- [Indie monetization in 2026: premium, DLC or hybrid](https://www.strayspark.studio/blog/indie-game-monetization-2026-pricing-strategy)
- [RevenueCat — State of Subscription Apps 2026](https://www.revenuecat.com/state-of-subscription-apps)
