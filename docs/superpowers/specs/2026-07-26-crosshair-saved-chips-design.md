# Crosshair Page — Saved Configs as Preview Chips

**Date:** 2026-07-26
**Status:** Approved by user, ready for implementation
**Scope:** `Pages/CrosshairPage.cs` (+ extracted shared renderer if needed)

## Problem

The SAVED section on the Crosshair page uses a native WinForms `ComboBox`
(`CrosshairPage.cs:131`). In the dark-themed UI the native dropdown is visually
jarring, shows no preview of what each saved crosshair looks like, and pairs with
separate Save/Delete buttons that clutter the row. The user finds the format
annoying and wants it replaced.

## Approved Design

Replace the ComboBox + Delete button with a **chip/pill list**:

- A `FlowLayoutPanel` under the existing `SAVED` caption (`CrosshairPage.cs:130`),
  `AutoSize = true`, `WrapContents = true`, themed with `Theme.Surface`.
- One **chip** per entry in `_settings.SavedCrosshairs`. Each chip is a custom
  owner-drawn control containing:
  - a **mini crosshair preview** (~22×22 px) rendering the saved shape in its
    saved colour on a dark/checker background — reuse the existing drawing code
    from `PreviewBox` (extract into a shared static renderer, e.g.
    `CrosshairRenderer.Draw(Graphics, CrosshairConfig, Rectangle)`, used by both
    `PreviewBox` and the chips — no copy-pasted drawing logic);
  - the config **name** in `Theme.Text`;
  - a small **×** on the right that deletes that saved config immediately
    (no confirmation dialog).
- **Click chip** → load that config (same behaviour as today's `LoadSaved`).
- The chip matching `_current.Name` gets a `Theme.Accent` border to show it's active.
- Hover state: `Theme.SurfaceHover` background.
- **`Save as…` button stays**, repositioned next to the flow panel. The standalone
  **Delete button and the ComboBox are removed.**
- **Empty state:** when `SavedCrosshairs` is empty, show a dim label
  "No saved crosshairs yet — tweak, then Save as…".

## Behaviour notes

- `RefreshSavedList()` rebuilds the chip list; keep selection/active-highlight
  in sync after save/delete/load.
- After delete, if the deleted chip was the active one, just clear the highlight
  (current on-screen config stays as-is — same as today's behaviour).
- Data model (`AppSettings.SavedCrosshairs`) is unchanged — pure UI rework.

## Acceptance criteria

1. `dotnet build` succeeds, 0 errors.
2. Saving via "Save as…" adds a chip with correct mini preview (shape + colour).
3. Clicking a chip loads its config into the editor, preview and live overlay
   update (via existing `Push()`), and that chip shows the accent border.
4. × removes only that chip; settings file updated.
5. Empty state shows when no saved configs exist.
6. No ComboBox or Delete button remains on the page.
7. Chips render correctly in both dark and light themes (use `Theme.*` colours
   only, no hardcoded colours).

## Out of scope

- Reordering chips, renaming in place, confirmation dialogs.
- Any change to the overlay service or other pages.
