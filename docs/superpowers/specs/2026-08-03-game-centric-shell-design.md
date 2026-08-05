# Game-centric shell

Written 2026-08-03.

## The idea

PlexusX becomes an app you point at one game. You pick it once, bottom-left next to the
version, and the parts of the app that are about a game follow it.

## Why this first

Three features were asked for at the same time — a Resolution/Monitor tab, a keybinds page
with a keyboard UI, and a more capable launcher. Every one of them has to answer "which game
am I configuring?" before it can show anything. Today that question is answered by a dropdown
buried inside the Profile Editor, and not at all anywhere else.

So this is the foundation, and it is also the smallest of the four: nothing here is new
machinery, it is rewiring what already exists.

Order for the whole programme: **shell → resolution tab → launcher → keybinds**. Keybinds is
last because it is the only one with a real research burden (a command catalogue per game).

## Scope

In:

- One selected game, app-wide, remembered between runs
- A chooser in the left nav above the version
- Nav item "Games" becomes "Game", and shows the selected game's page directly
- Profile Editor drops its own game dropdown and follows the selection
- A short transition when the selection changes

Out (their own specs later): resolution tab, keybinds page, launcher work.

**Display stays global.** Saturation, vibrance, contrast and temperature are not scoped to a
game and do not change when the selection changes. This was decided explicitly: the Display
page is the live desktop-wide state, the per-game colour profile is a separate thing that the
Profile Editor owns and auto-apply installs on launch. Merging them was considered and
rejected — it would have made "what am I looking at?" ambiguous on the app's home page.

## Design

### `GameSelection`

A small service holding the current selection and raising an event when it changes. Owned by
`TrayApplicationContext`, passed to `MainWindow`.

```
string?  CurrentId          // null = Desktop (no game)
SupportedGame? Current      // resolved, null when Desktop
DetectedGame?  Detected     // null when Desktop or not installed
IReadOnlyList<DetectedGame> Installed
event EventHandler Changed
void Select(string? gameId)
void Refresh()              // re-run detection
```

Persisted as `AppSettings.CurrentGameId` (empty string = Desktop).

Why a service rather than a property on MainWindow: the tray menu, the launcher and later the
resolution tab all need it, and the window is rebuilt wholesale on a theme change.

**Selection is a UI concept only.** It does not change what auto-apply does — that still keys
off whichever game actually launches, via `GameProcessWatcher`. Someone can be configuring
Rust in the app while CS2 is running, and CS2's profile still applies. Tying auto-apply to the
chooser would silently break that, and would mean picking a game in a menu changed the colours
on someone's screen.

### Chooser control

`GameChooser`, in the nav, directly above the version label. Full nav width minus margins,
~40px tall. Shows the current game's name; opens a themed list of installed games plus
Desktop.

Not-installed games are not listed. The catalogue view (all four games, installed or not) is
what the Game tab shows when nothing is selected, which is a better home for it — a chooser
that offers you things you cannot pick is a dead end.

Empty state: no games detected at all → the chooser reads "No games found" and is disabled,
and the Game tab explains what to check.

### Nav and the Game tab

- "Games" → **"Game"**
- Selection is a game → the tab shows that game's page (`RustSettingsPage`, `Cs2SettingsPage`,
  `ApexSettingsPage`, `FortniteSettingsPage`) directly. No grid, no click-through.
- Selection is Desktop → the tab shows the picker: every supported game as a card, installed
  first, clicking one selects it. This is today's `GamesHubPage`, repurposed as the empty
  state instead of a permanent hub.
- The per-game pages lose their "‹ Games" back link. There is nothing to go back to now; the
  chooser is how you change game.

### Profile Editor

- `GlassDropdown _gamePicker` removed. The GAME card shows the selected game's name as text.
- On `Changed`, load that game's saved profile (or the neutral default) into the sliders.
- Desktop selected → the page explains that a profile belongs to a game and points at the
  chooser. Its Save button is disabled; there is nothing to save it against.
- `PopulateGames` / `SelectGame` stay on the class so existing callers and tests keep
  compiling, but they now delegate to `GameSelection`.

### Transition

`GlowPage` gains an intro fade: a float that starts at 1 and decays to 0, painted as a scrim
in `Theme.Background` over the page. `MainWindow`'s existing 33ms animation timer drives it —
no new timer, no new control, and it reuses the paint path every page already has.

~200ms. Long enough to read as a deliberate change of context, short enough that someone
flicking between games is not waiting on it.

### Data flow

```
User picks a game in GameChooser
  → GameSelection.Select(id)
  → AppSettings.CurrentGameId saved
  → Changed raised
      → MainWindow rebuilds the Game tab's content for the new game
      → ProfileEditorPage loads that game's profile
      → the visible page begins its intro fade
```

Nothing else in the app reacts. Auto-apply, the overlay and the Display page are untouched.

## Error handling

- **Saved game no longer installed** — selection falls back to Desktop, and the Game tab says
  which game was dropped rather than silently forgetting it.
- **Saved id not in the catalogue** (downgrade, hand-edited settings) — treated as Desktop.
- **Detection throws** — `GameLibrary` already swallows and returns empty; the chooser shows
  the no-games state rather than failing to build the window.
- **Game uninstalled while the app is open** — no live watcher for this. `Refresh()` runs when
  the Game tab is opened, so it corrects itself the next time the user looks.

## Testing

Pure logic, no message pump:

- `GameSelection` resolves a saved id to a game; unknown and uninstalled ids fall back to
  Desktop
- Selecting raises `Changed` exactly once, and persists
- Selecting the already-selected game does not raise
- `Installed` excludes games that are not detected
- Desktop selection leaves `Current` and `Detected` null

Window-level, with the existing WinForms test pattern:

- Nav reads "Game", not "Games"
- Game tab with a selection contains that game's page and no `GameCard`s
- Game tab at Desktop contains the cards and no per-game page
- Profile Editor contains no `GlassDropdown`
- Changing selection while the Profile Editor is open reloads its values

## What this does not do

- Does not scope Display, Crosshair, FPS Tweaks or Settings. They stay global.
- Does not change auto-apply behaviour.
- Does not add resolution, keybind or launcher features. Those are separate specs that this
  one unblocks.
