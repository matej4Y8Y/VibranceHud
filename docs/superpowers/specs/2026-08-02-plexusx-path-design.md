# PlexusX — the path to being the name

**Goal:** when someone talks about colour in games, they say PlexusX. The way people say
ReShade. Best in the world at one thing, with a few finished extras around it.

**Pricing (settled):** 4-day trial, $4/month, $12 lifetime.

**Approval (settled):** Facepunch and Valve have cleared the app to be sold. Not through
Steam — selling stays on our own site. Their condition: **Eye Care must go before we sell.**

---

## The tension we're accepting

ReShade became the word for post-processing because it was free. Free meant everyone tried
it, everyone shared presets, and every thumbnail said its name.

We're charging from day one. That's decided. So fame has to come from somewhere other than
free downloads, and this whole plan is built around that: **if we can't give the software
away, we make the result visible everywhere instead.**

---

## 1. Be undeniably the best at colour

Everything else is support. Two things stop that being true today.

**The laptop bug.** On a gaming laptop the built-in screen usually runs off the Intel or AMD
chip, not the NVIDIA one. We ask NVIDIA which screens it drives, get nothing, and tell the
user they have no NVIDIA GPU. They do — it just isn't wired to that screen.

Fix: detect the card separately from the screens it drives. Then the message is "your laptop
screen runs off the integrated graphics, so use Saturation" instead of something false.

**AMD and Intel are second-class.** Without the NVIDIA path, the Vibrance slider changes
meaning: 50 makes the picture *washed out* and you don't reach normal until 100. So half our
users sit at the default seeing a worse picture and conclude it's broken.

Fix: make 50 mean normal on every machine. This changes the numbers for anyone already on
AMD or Intel, so their saved profiles shift — that has to be handled on upgrade rather than
silently.

## 2. Make the effect visible outside our own screen

**This is the growth engine, not a feature.**

Today a streamer uses PlexusX and his chat sees the game exactly as it always looked. The one
moment thousands of people could notice the product is the moment it doesn't appear.

The cause is known and precise. Two paths do the work:

- Saturation, Brightness, and Vibrance above 100 go through the software colour matrix, which
  is applied during desktop composition — so OBS **Display Capture** picks it up
- Vibrance 0–100 goes to the NVIDIA driver, and Gamma goes through the gamma ramp. Both are
  applied after composition, on the way out to the cable. Nothing can capture those, ever.

That's why it works for some people and not others — it depends which slider they used.

**Streaming Mode:** a switch that rebuilds the whole look inside the software matrix, so all
of it is capture-visible. Cap driver vibrance at zero and carry the full range in the matrix.
Gamma can't come along — it's non-linear — so substitute a contrast term and label it an
approximation rather than silently giving people something different.

Offer it when OBS or Discord is running, don't force it. Tell the user the one thing that
actually solves it: **Display Capture, not Game Capture.** Warn if the game is in exclusive
fullscreen, which bypasses composition entirely.

Side benefit: the software path is GPU-agnostic, so Streaming Mode also puts AMD and Intel on
equal footing.

## 3. Zero friction to first colour

Someone sees a TikTok, joins Discord, downloads, installs. Every step between that and
"whoa" loses people.

- **The trial starts silently on first launch.** No key, no account, no dialog.
- The first thing on screen is the slider that does the thing.
- Windows will still warn on install — that's covered on the site and in Discord already, and
  it's a one-time cost since in-app updates don't trigger it.

## 4. Make settings shareable

A short code carrying vibrance, saturation, brightness, gamma, and per-game profile. Paste
someone's code, get their setup.

When people ask "what are your settings", the answer becomes a PlexusX code. The code travels
further than the app does and carries the name with it. This is the closest thing a paid app
has to ReShade's preset culture.

---

## The rest stays small

Crosshair, Lock In, monitor control, FPS tweaks. These are what make it all-in-one rather
than a single-trick tool. **They get finished, not extended.** No new features in any of them
until the four things above are done.

**Eye Care is removed** — Facepunch's condition. Before deleting the code, ask them whether
the objection is the feature or the name. "Eye Care" is a health claim, and that's the kind
of thing a company's lawyers refuse rather than their designers. If it's the words, it ships
as Warmth. If it's the feature, it goes.

---

## Order

1. Laptop fix — small, safe, someone is complaining now
2. Streaming Mode — the growth engine
3. Trial starts silently
4. Vibrance means the same thing on every GPU
5. Share codes
6. GUI polish pass
7. Eye Care removed or renamed

---

## Not doing

- **Recording and clipping.** Medal is free and funded. We'd lose.
- **Night brightness / "bright night".** It's what got other colour tools banned, and it's the
  one feature that would make "we never touch the game" untrue.
- **More supported games as a feature.** Vibrance is already global. The game pages are for
  per-game configs, which have to be written per game and can't be generated.
- **Anything new in the extras** until the four above are done.

---

## Still open, and not a code problem

Stripe and Paddle both require an account holder over 18. Nothing in this plan can ship as a
paid product until that's settled — who takes the money, in whose name, and whose tax
details. It gates everything else and no amount of building moves it.
