# PlexusX UI & System Contract

LIVING DOC. Written 2026-08-07.

**Every rule here exists because of a real defect.** The defect is named next to the rule, and
the test that enforces it is named too. A rule with a story behind it does not get argued with,
and a rule with a test behind it does not need to be remembered.

Enforcement lives in `tests/VibranceHud.Tests/UiContractTests.cs`,
`ControlPaintTests.cs`, `AllPagesLayoutAuditTests.cs` and `RenderHarness.cs`.

---

## The philosophy

> **PlexusX owns your monitor. Everything you look at, nothing under the hood.**

If a feature changes **what you see** — colour, resolution, refresh rate, HDR, the crosshair,
the panel's own settings — it belongs. If it changes **what the PC is doing** — registry keys,
game config files, process priority, RAM — it does not.

---

## UI rules

### U1 — No stock Win32 controls outside `Controls/`

Banned: `Button`, `TextBox`, `ComboBox`, `CheckBox`, `RadioButton`, `LinkLabel`,
`NumericUpDown`, `TrackBar`, `GroupBox`, `ListBox`, `TabControl`.
Use `GlassButton`, `GlassTextBox`, `GlassDropdown`, `ToggleSwitch`, `ChipButton`, `FlatSlider`,
`GlassLink`. `Label` and `Panel` are fine — they carry no Win32 chrome.

**Why:** a stock control cannot be themed. `FlatStyle.Flat` still keeps square corners and
draws the system focus rectangle, so it reads as a piece of a different application no matter
which colours it is given. Forty call sites went through one helper that returned a stock
`Button`, which is why so much of the app was grey rectangles beside rounded glass cards.

**Exception:** `GlassTextBox` hosts a real `TextBox` internally. Caret placement, selection,
IME, undo and clipboard are not worth reimplementing to change a border. That exception lives
in exactly one file.

**Test:** `NoStockWin32ControlIsConstructedOutsideTheWrappers`

### U2 — No hardcoded colours

Everything from `Theme.*`. `Color.FromArgb` only to alpha-blend a `Theme` colour, or inside
`Controls/` and `Theming/` for painting primitives. `SystemColors.*` is banned outright.

**Why:** `SystemColors` follows the OS theme, not ours. One of those on a glass card is a
light-grey rectangle in the middle of a dark window.

**Test:** `NoSystemColoursAnywhere`

### U3 — Fonts come from `Design/Fonts`

No `new Font(Theme.FontFamily, …)` outside `Design/`. Explicit `"Consolas"` for codes and hex
values is the one exception.

**Why:** these are allocated inside `OnPaint`, which runs about thirty times a second per
control while the plexus animates behind it.

**Note:** `Design.Fonts.Rebuild()` deliberately does **not** dispose. Stock controls hold
references to the old fonts and disposing produces a dead-GDI-handle crash on monitor change.
Do not "fix" that.

**Test:** `FontsComeFromTheDesignLayer`

### U4 — Spacing comes from `Design/Tokens`

No new magic numbers for gutters and gaps. `Tokens.ScaleAt` has a hairline floor so a 1px
border never rounds to 0 at fractional DPI.

### U5 — Nothing clipped, nothing overlapping

Measure wrapped text with
`TextRenderer.MeasureText(text, font, new Size(w, int.MaxValue), TextFormatFlags.WordBreak).Height + 6`.

**Why the `+ 6`:** `MeasureText` and `Label` do not lay text out identically, and the couple of
pixels between them is exactly enough to slice the descenders off the last wrapped line. The
Monitor page's PVP descriptions shipped clipped mid-word — *"you lose horizontal field of view,
so mor"* — while passing every geometric assertion in the suite.

**Tests:** `NothingOverlapsAnythingElse`, `NothingIsClippedByItsOwnCard`,
`NoLabelIsShorterThanItsOwnText`

### U6 — Every page reaches its own content

`AutoScroll` plus a real extent via `FitScrollToContent()`. The wheel must work with the cursor
over any descendant.

**Why:** `AutoScroll` alone reports nothing to scroll on a page laid out at absolute
coordinates, so everything below the window's height was simply unreachable — about 200px of
Settings and FPS Tweaks each. Separately, Windows sends the wheel to the control under the
cursor, and on these pages that is almost always a card, so Display never scrolled at all.

**Tests:** `ScrollingTests`, `AScrollingPageCanReachItsOwnContent`

### U7 — Keyboard-reachable and announced

`TabStop = true`, `AccessibleRole` set, `AccessibleName` tracking `Text`, and a visible focus
ring. **A control that handles arrow keys must claim them in `IsInputKey`.**

**Why:** WinForms treats arrows as dialog navigation unless a control claims them, so
`ColourWheel`'s entire keyboard implementation was dead code — focusing the wheel and pressing
Right moved focus to the next control. The source read correctly and nothing happened on screen.

**Tests:** `FocusAndKeyboardTests`, `ColourWheelTests.TheWheelClaimsTheKeysItHandles`

### U8 — Paints in every state

Normal, hover, pressed, focused, **disabled**, at 1×1 and 2000×40, in all four themes, without
throwing. Disabled must *look* disabled.

**Why:** a radius-0 focus ring shipped a GDI+ crash that 1025 tests missed — the user got
"Parameter is not valid" and a white box where the nav should be. Separately, `GlassButton`
ignored `Enabled` entirely: a disabled one painted identically to a live one and simply stopped
responding, which reads as the app having frozen.

**Test:** `ControlPaintTests` (every control × every state × every theme)

### U9 — User-visible numbers use `CultureInfo.InvariantCulture`

**Why:** the UI is English throughout, but a composite format string picks up the machine's
locale. Gamma rendered as `1,00` on a Czech Windows, and every readout on the crosshair page —
size, gap, thickness, ring — rendered as `1,5`, on the page whose tenths precision was a
specifically requested feature.

**Test:** `DecimalFormattingIsCultureIndependent`

### U10 — No dead affordance

A control that is shown must do something. Unset state says "Not set", never `(none)` or `0x0`.

**Why:** an unbound shortcut rendered as `Ctrl+Shift+(none)`. Modifiers without a key are not a
shortcut, and that reads as a control that has broken rather than one that was never set.

### U11 — Dialogs are `GlassDialog`, or handle Enter/Escape by hand

**Why:** `GlassButton` is an owner-drawn `Control`, not an `IButtonControl`. Assigning
`AcceptButton`/`CancelButton` compiles to nothing and the dialog silently becomes unclosable.
Use `KeyPreview = true` and a `KeyDown` handler.

**Test:** `GlassDialogTests.TheDialogDoesNotRelyOnAcceptOrCancelButton`

### U12 — No mojibake

No `â€`, `Ã¢`, `â–`, `Ã©`, `Ã¼` byte sequences in any source file.

**Why:** UTF-8 read back as CP1250 leaves these behind. Two shipped as visible button labels —
`"Choose imageâ€¦"` and `"Measuringâ€¦"` — and sat in Settings unnoticed.

**Test:** `NoMojibakeInAnySource`

---

## System rules

### S1 — Nothing under the hood
No registry tweaks, no game config writes, no handles opened into a game process, no priority
changes. This is the philosophy expressed as code.

### S2 — No raw exception dialogs
Every unhandled path reaches the friendly crash dialog and writes a log. **The native
`MessageBox` in `Program.cs` is deliberate and stays** — the themed dialog needs a working
WinForms context, which is exactly what may be broken at that point.

### S3 — Settings never silently lost
Every new persisted field needs a `SettingsStore` round-trip test. Watch for self-returning
properties: a `Normalized` that returns its own type caused infinite recursion in the
serializer and broke **every** settings save.

### S4 — Never block the UI thread
Disk, network and display-driver work on a user gesture must not freeze the window. DDC/CI
reads are slow — tens to hundreds of milliseconds — so they go off-thread.

### S5 — Honest claims only
No feature text that promises what the code does not do. Where a capability depends on the
machine, the capability probe decides the wording. Never claim monitor control on a panel that
refused it.

### S6 — Never leave the hardware wrong
Read and store the original value before the first write to the physical monitor. Always offer
a revert, and make it survive a restart.

---

## Process rules

- **P1** — TDD. Failing test, minimal implementation, green.
- **P2** — Full suite green before every commit. Deletions may lower the count legitimately;
  record the new number in `docs/OVERNIGHT-LOG.md`.
- **P3** — Visual changes are verified by rendering through `RenderHarness`, not by reasoning.
  A change nobody looked at is not done.
- **P4** — Commit per task. The message says what changed **and why it was wrong before**.
- **P5** — Evidence before claims. Paste the test line. Say what was skipped.
