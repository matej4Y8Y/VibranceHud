# Brief — Set Profile Editor Redesign

**Datum:** 2026-07-29
**Status:** Brief for code-subagent
**Target:** ProfileEditorCard.cs + MainWindow.cs (host size)
**Build:** dotnet 8, WinForms, single-file self-contained publish

## Context

PlexusX Set Profile editor currently has these problems:

1. **Cards overflow viewport**: Game/Visuals/Hub cards are too tall for
   the 600x628 content host. Scrollbar appears, but Scrollbar is a poor UX
   for a primary editor ("everything should fit").
2. **Footer Save/Cancel can clip**: The Cancel button text clips to
   "Cance" when the host is narrow or the user resizes.
3. **Header is too tall** (60-72px) eating into already-tight space.
4. **Cards-within-cards visual style** is heavy for a small panel —
   border + padding + caption + subtitle per card = 4 visual layers;
   users want a lighter, more focused layout.

Iteration v1 and v2 (current `ProfileEditorCard.cs`) tried card layouts
with mixed results. The cards helped visually but the heights don't fit
in 628px (1040x680 window, 52px title, 628px content area).

## Goal

Same functionality, single screen, no internal scroll. Header sticky on
top, footer sticky on bottom, content in between scrolls only if the
window is unusually small (<600px tall).

## Final Layout

```
┌──────────────────────────────────────────────┐
│ Profile Editor                                │  ← sticky header (60px)
│ Tweak how this game looks when you launch it. │
├──────────────────────────────────────────────┤
│                                               │
│  GAME                                         │  ← section header (10pt bold)
│  [🌲 Rust                            ▼]      │  ← ComboBox full width
│                                               │
│  VISUALS                                      │
│  Vibrance      ▓▓▓▓▓▓▓▓░░░  100%              │
│  Saturation    ▓▓▓▓▓▓▓▓░░░  100%              │
│  Brightness    ▓▓▓▓▓▓▓▓░░░  100%              │
│  Gamma         ▓▓▓▓▓▓▓▓░░░   1.0              │
│                                               │
│  GAME-HUB OPTIONS                             │
│  Quality preset  [Default                ▼]   │
│  FPS cap         [  0  ]                       │
│                                               │
│  ● Auto-apply running                         │  ← status badge
├──────────────────────────────────────────────┤
│                  [ Cancel ]  [ Save profile ] │  ← sticky footer (56px)
└──────────────────────────────────────────────┘
```

## Implementation Plan

### 1. `MainWindow.cs` (line 170-184)

Change `_profileHost`:
- `Size = new Size(600, ClientSize.Height - TitleH)` (was 560)
- `BackColor = Theme.Background` (was hardcoded)
- (Already has Anchor Top|Right|Bottom)

### 2. `ProfileEditorCard.cs` — full rewrite

**Root layout** (Dock-based):
```csharp
var root = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
Controls.Add(root);

// 1. Header (Dock=Top, Height=60)
var header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Theme.Background };
//   Add title + subtitle labels with manual positions
//   Add a 1px separator line at the bottom (paint event)
root.Controls.Add(header);

// 2. Footer (Dock=Bottom, Height=56)
var footer = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Theme.Surface };
//   Add a 1px border line at the top (paint event)
//   Add Save + Cancel buttons right-aligned
root.Controls.Add(footer);

// 3. Status badge (Dock=Bottom, Height=28) — added BEFORE footer so it sits above
var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Color.Transparent };
//   Add dot label + text label
root.Controls.Add(statusPanel);

// 4. Content (Dock=Fill) — fills the remaining space
var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, AutoScroll = true };
root.Controls.Add(content);

// IMPORTANT: With Dock=Bottom, the LAST added control sits at the BOTTOM
// edge of the parent. So add them in order: footer (will be at bottom),
// then status (will be above footer), then content (fills remaining).
```

**Header content**:
```csharp
var titleLabel = new Label {
    Text = "Profile Editor",
    Font = new Font(Theme.FontFamily, 13f, FontStyle.Bold),
    ForeColor = Theme.Text,
    AutoSize = true,
    Location = new Point(24, 10),
    BackColor = Color.Transparent
};
var subtitleLabel = new Label {
    Text = "Tweak how this game looks when you launch it.",
    Font = new Font(Theme.FontFamily, 8.5f),
    ForeColor = Theme.TextDim,
    AutoSize = true,
    Location = new Point(24, 34),
    BackColor = Color.Transparent
};
header.Controls.Add(titleLabel);
header.Controls.Add(subtitleLabel);
// Header bottom border line
header.Paint += (s, e) => {
    using var pen = new Pen(Theme.Border, 1);
    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
};
```

**Footer content**:
```csharp
var saveButton = new Button {
    Text = "Save profile",
    AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
    MinimumSize = new Size(130, 32),
    Padding = new Padding(14, 4, 14, 4),
    BackColor = Theme.Accent,
    ForeColor = Theme.IsLight ? Color.White : Theme.Background,
    FlatStyle = FlatStyle.Flat,
    Font = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
    Cursor = Cursors.Hand
};
saveButton.FlatAppearance.BorderSize = 0;
saveButton.Click += (_, _) => Save();

var cancelButton = new Button {
    Text = "Cancel",
    AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
    MinimumSize = new Size(90, 32),
    Padding = new Padding(14, 4, 14, 4),
    BackColor = Theme.Surface,
    ForeColor = Theme.Text,
    FlatStyle = FlatStyle.Flat,
    Font = new Font(Theme.FontFamily, 9.5f),
    Cursor = Cursors.Hand
};
cancelButton.FlatAppearance.BorderColor = Theme.Border;
cancelButton.FlatAppearance.BorderSize = 1;
cancelButton.Click += (_, _) => OnCancelled?.Invoke(this, EventArgs.Empty);

footer.Controls.Add(saveButton);
footer.Controls.Add(cancelButton);

// Right-align. Resize handler keeps them right-aligned.
saveButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
cancelButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
saveButton.Location = new Point(footer.Width - 24 - saveButton.Width, 12);
cancelButton.Location = new Point(saveButton.Left - 12 - cancelButton.Width, 12);
footer.Resize += (_, _) => {
    saveButton.Location = new Point(footer.Width - 24 - saveButton.Width, 12);
    cancelButton.Location = new Point(saveButton.Left - 12 - cancelButton.Width, 12);
};

// Footer top border line
footer.Paint += (s, e) => {
    using var pen = new Pen(Theme.Border, 1);
    e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
};
```

**Content area** — three sections stacked vertically:

```csharp
int padX = 24;
int contentX = padX;
int contentWidth = 600 - 2 * padX; // 552px

int y = 16;

// ---- GAME section ----
var gameHeader = MakeSectionHeader("GAME", contentX, y);
y += 22;
var gamePicker = new ComboBox {
    Location = new Point(contentX, y),
    Size = new Size(contentWidth, 28),
    DropDownStyle = ComboBoxStyle.DropDownList,
    FlatStyle = FlatStyle.Flat,
    BackColor = Theme.Surface,
    ForeColor = Theme.Text,
    Font = new Font(Theme.FontFamily, 9.5f)
};
gamePicker.SelectedIndexChanged += (_, _) => OnGameChanged();
content.Controls.Add(gameHeader);
content.Controls.Add(gamePicker);
y += 28 + 16;

// ---- VISUALS section ----
var visualsHeader = MakeSectionHeader("VISUALS", contentX, y);
y += 22;
int sliderRowHeight = 36;
_vibrance = MakeVibranceSlider(contentX, y, contentWidth); y += sliderRowHeight;
_saturation = MakeSaturationSlider(contentX, y, contentWidth); y += sliderRowHeight;
_brightness = MakeBrightnessSlider(contentX, y, contentWidth); y += sliderRowHeight;
_gamma = MakeGammaSlider(contentX, y, contentWidth); y += sliderRowHeight + 16;

// ---- GAME-HUB OPTIONS section ----
var hubHeader = MakeSectionHeader("GAME-HUB OPTIONS", contentX, y);
y += 22;
_qualityPicker = new ComboBox { /* full width */ };
content.Controls.Add(MakeLabeledField("Quality preset", _qualityPicker, contentX, y, contentWidth));
y += 32;
_fpsCap = new NumericUpDown { /* width 100, Dock-style positioned */ };
content.Controls.Add(MakeLabeledField("FPS cap", _fpsCap, contentX, y, contentWidth));
```

**Helper methods**:
```csharp
private Label MakeSectionHeader(string text, int x, int y) {
    return new Label {
        Text = text,
        Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold),
        ForeColor = Theme.Text,
        AutoSize = true,
        Location = new Point(x, y),
        BackColor = Color.Transparent
    };
}

private Label MakeFieldLabel(string text, int x, int y, int width) {
    return new Label {
        Text = text,
        Font = new Font(Theme.FontFamily, 9f),
        ForeColor = Theme.TextDim,
        AutoSize = false,
        Size = new Size(width, 16),
        Location = new Point(x, y),
        TextAlign = ContentAlignment.MiddleLeft,
        BackColor = Color.Transparent
    };
}

private Control MakeLabeledField(string caption, Control field, int x, int y, int totalWidth) {
    // 2-col layout: label 35% | field 65%
    int labelW = (int)(totalWidth * 0.35);
    int fieldW = totalWidth - labelW - 8;
    var label = MakeFieldLabel(caption, x, y, labelW);
    field.Location = new Point(x + labelW + 8, y - 2);
    field.Size = new Size(fieldW, 24);
    return label; // caller adds label + field to content
}

private FlatSlider MakeSliderRow(string caption, int min, int max, int x, int y, int totalWidth,
                                   out Label valueLabel, int decimals) {
    int labelW = (int)(totalWidth * 0.35);
    int valueW = 50;
    int sliderW = totalWidth - labelW - valueW - 16;
    var slider = new FlatSlider {
        Location = new Point(x + labelW + 8, y),
        Size = new Size(sliderW, 24),
        Minimum = min,
        Maximum = max,
        Notch = 100
    };
    var label = MakeFieldLabel(caption, x, y + 4, labelW);
    valueLabel = new Label {
        Text = "0",
        Font = new Font(Theme.FontFamily, 9f, FontStyle.Bold),
        ForeColor = Theme.Accent,
        AutoSize = false,
        Size = new Size(valueW, 16),
        Location = new Point(x + labelW + 8 + sliderW + 8, y + 4),
        TextAlign = ContentAlignment.MiddleRight,
        BackColor = Color.Transparent
    };
    content.Controls.Add(label);
    content.Controls.Add(slider);
    content.Controls.Add(valueLabel);
    slider.ValueChanged += (_, _) => UpdateValueLabel(valueLabel, slider.Value, decimals);
    return slider;
}
```

**Status badge**:
```csharp
_statusDot = new Label {
    Text = "○",
    Font = new Font(Theme.FontFamily, 11f, FontStyle.Bold),
    ForeColor = Theme.TextDim,
    AutoSize = true,
    Location = new Point(24, 6),
    BackColor = Color.Transparent
};
_statusLabel = new Label {
    Text = "Auto-apply paused",
    Font = new Font(Theme.FontFamily, 9f),
    ForeColor = Theme.TextDim,
    AutoSize = true,
    Location = new Point(40, 8),
    BackColor = Color.Transparent
};
statusPanel.Controls.Add(_statusDot);
statusPanel.Controls.Add(_statusLabel);
```

**Game emoji badge** (optional, decorative):
```csharp
private Label _gameBadge = null!;
_gameBadge = new Label {
    Text = "🎮",
    Font = new Font("Segoe UI Emoji", 11f),
    AutoSize = true,
    Location = new Point(0, 0), // adjusted based on dropdown position
    BackColor = Color.Transparent,
    ForeColor = Theme.Text
};
// Skip badge if it would clip the dropdown. Just use the dropdown full-width.
```

(Skip the badge for now — keep it simple, just the dropdown. We can add
 the badge later if there's room.)

### 3. Public API (unchanged)

```csharp
public sealed class ProfileEditorCard : UserControl
{
    public event EventHandler? OnSaved;
    public event EventHandler? OnCancelled;

    public void PopulateGames(IEnumerable<(string Id, string Name)> games);
    public void SelectGame(string gameId);
    public void SetStatus(bool watcherRunning);
    public void LoadProfile(GameProfile profile);
}
```

These must remain unchanged so MainWindow callers don't break.

### 4. Tests

Add 3-4 unit tests to `tests/.../ProfileEditorCardTests.cs`:

```csharp
[Fact]
public void LoadProfile_PopulatesAllSliders()
{
    var card = new ProfileEditorCard();
    card.PopulateGames(new[] { ("rust", "Rust") });
    var profile = new GameProfile {
        GameId = "rust", DisplayName = "Rust",
        Vibrance = 75, Saturation = 80, Brightness = 50, Gamma = 110,
        GameHub = new GameHubOptions { GraphicsQuality = "High", FpsCap = 144 }
    };
    card.LoadProfile(profile);
    // Use reflection or expose private fields via internal accessors
    // Assert.Equal(75, GetVibranceValue(card));
    // etc.
}

[Fact]
public void Save_RoundTripsThroughGameProfileStore()
{
    var tmpStore = TestHelpers.CreateTempStorePath();
    var card = new ProfileEditorCard();
    card.PopulateGames(new[] { ("rust", "Rust") });
    // Set slider values via reflection
    // Click save
    // Read back from store
}

[Fact]
public void PopulateGames_ClearsPreviousEntries()
{
    var card = new ProfileEditorCard();
    card.PopulateGames(new[] { ("rust", "Rust"), ("cs2", "CS2") });
    card.PopulateGames(new[] { ("apex", "Apex") });
    // Assert only Apex is in the picker
}
```

Note: ProfileEditorCard is a `UserControl` which is hard to test headless
without WinForms event loop. Use `Application.Run(new HiddenForm())` or
test the helpers separately. Or expose internal fields via `InternalsVisibleTo`
and test methods that don't require UI dispatch.

Recommendation: **Test helpers only.** Pure logic like `UpdateValueLabel`,
`MakeSliderRow`, `MakeLabeledField` should be static or internal so tests
can verify the layout sizing. The full Save() flow is already covered by
GameProfileStoreTests.

## Files to Touch

1. `ProfileEditorCard.cs` — full rewrite of BuildLayout method
2. `MainWindow.cs` — line 170-184, host Size + BackColor
3. `tests/.../ProfileEditorCardTests.cs` — NEW file with 3-4 small tests

## Verification Steps

```bash
# 1. Build
cd /path/to/VibranceHud
dotnet build VibranceHud.csproj -c Release --nologo
# Expected: 0 errors, 0 warnings

# 2. Test
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --nologo
# Expected: 305 PASS, 0 fail (or more if new tests added)

# 3. Publish
dotnet publish VibranceHud.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -o publish --nologo
# Expected: exit 0, publish/PlexusX.exe regenerated

# 4. Visual check
# Launch publish/PlexusX.exe, click Set Profile in nav, capture screenshot.
# Verify: header + all 3 sections + status + footer ALL visible without scroll.
# Save/Cancel fully visible, not clipped.
```

## Constraints

- All existing tests must pass (305/305 minimum)
- No new warnings
- Build clean
- OnSaved/OnCancelled events preserved
- Public API preserved (PopulateGames, SelectGame, SetStatus, LoadProfile)
- Save() must produce a valid GameProfile that GameProfileStore accepts
- Theme-aware (use Theme.Background, Theme.Surface, Theme.Border, Theme.Accent)
- Dark + light theme both work
