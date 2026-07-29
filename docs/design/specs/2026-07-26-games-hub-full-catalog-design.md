# Games Hub — Show Full Supported Catalog with Install Status

**Date:** 2026-07-26
**Status:** Approved by user, ready for implementation
**Scope:** `Pages/GamesHubPage.cs`, `GameCard.cs` (no changes to detection/catalog)

## Problem

The Games Hub only renders cards for games detected on the PC
(`GamesHubPage.cs:50` — `GameLibrary.DetectInstalled()`). Users can't see what
games PlexusX supports unless they happen to own them; the empty state is a plain
text label listing names.

## Approved Design

Show **every game in `SupportedGames.All`** as a card, with an install-state badge.

### GamesHubPage

- Still call `GameLibrary.DetectInstalled()` once; build a lookup from
  `SupportedGame` → `DetectedGame`.
- Render one card per `SupportedGames.All` entry, ordered: **installed first,
  then not installed** (catalog order within each group).
- Same grid math as today (200×160 cards, gap 16, 3 cols, start at 40,104). If
  the catalog grows past one screen, that's fine — page already scrolls (check
  `AutoScrollMinSize` still covers the grid bottom).
- Click handler only wired for installed cards.
- Subtitle text: "Everything PlexusX supports. Installed games are ready to configure."
- Remove the empty-state label (the grid always has cards now).

### GameCard

Add an installed/not-installed state (e.g. ctor overload taking
`SupportedGame game, DetectedGame? detected` or an `bool installed` — pick what
fits the codebase best; keep the existing `DetectedGame Game` property working
for installed cards, or expose `SupportedGame` + nullable `DetectedGame` — your
call, keep it clean):

- **Installed** (unchanged from today): accent tile with initial, green dot +
  "Installed", "Configure ›" in `Theme.Accent`, hand cursor, hover = brighter
  fill + accent border, clickable.
- **Not installed**: identical layout but —
  - tile uses a dim/neutral fill (e.g. `Theme.SurfaceHover`-ish, no accent),
    initial in `Theme.TextDim`
  - grey dot + "Not installed" in `Theme.TextDim`
  - **no** "Configure ›" text
  - no hover highlight, `Cursor = Cursors.Default`, no click event
- All colours from `Theme.*` only (must work in light + dark themes).

## Acceptance criteria

1. `dotnet build` 0 errors; existing tests still pass (`dotnet test`).
2. Hub shows 4 cards (Rust, CS2, Apex, Fortnite) regardless of what's installed.
3. Installed games appear first, look exactly as before, and open their settings
   page on click.
4. Not-installed cards are visibly dimmed, badged "Not installed", and clicking
   does nothing.
5. No regressions: transparency/backdrop rendering pattern (single-level
   transparent controls directly on the page — see the comment at
   `GamesHubPage.cs:43-49`) is preserved.

## Out of scope

- "Get it" / store links on not-installed cards (possible follow-up).
- Adding new games to the catalog.
- Real game logo artwork.
