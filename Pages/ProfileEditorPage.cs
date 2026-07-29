// Set Profile — rebuilt in the app's own visual language (2026-07-29, 2nd pass).
//
// What was wrong before: the page filled itself with an opaque `root` Panel plus
// opaque header/content/footer Panels. WinForms child controls paint OVER the
// parent's OnPaint, so GlowPage's particle field was drawn and then immediately
// covered — the page rendered as a flat grey slab that looked like it belonged to
// a different program. It also laid content into a 600px column inside an 830px
// host (a leftover from when this was a slide-in side panel), which left a wide
// dead margin down the right-hand side.
//
// Now: no opaque panels at all. Static text and the glass cards are drawn in
// OnPaint on top of the shared particle field, exactly like VibrancePage; real
// controls exist only for the things the user actually manipulates. Stock
// ComboBox/NumericUpDown/Button are gone — they cannot be themed on Windows — and
// are replaced by GlassDropdown, ChipButton, FlatSlider and GlassButton.
//
// Public API (unchanged): OnSaved, OnCancelled, PopulateGames, SelectGame,
// SetStatus, LoadProfile.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud.Pages
{
    public sealed class ProfileEditorPage : GlowPage
    {
        public event EventHandler? OnSaved;
        public event EventHandler? OnCancelled;

        private readonly GlassDropdown _gamePicker;
        private readonly FlatSlider _vibrance;
        private readonly FlatSlider _saturation;
        private readonly FlatSlider _brightness;
        private readonly FlatSlider _gamma;
        private readonly FlatSlider _fpsCap;
        private readonly List<ChipButton> _qualityChips = new();
        private readonly GlassButton _saveButton;
        private readonly GlassButton _cancelButton;

        private readonly List<(string Id, string Name)> _games = new();
        private EditorMetrics _m = ProfileEditorLayout.Compute(830, 628);
        private bool _watcherRunning;

        /// <summary>Stored values are unchanged so existing profiles.json keeps loading;
        /// only the on-screen labels differ (a parenthesised "(default)" reads like a
        /// placeholder rather than a choice).</summary>
        private static readonly string[] QualityOptions =
            { "(default)", "Low", "Medium", "High", "Very High", "Ultra" };

        private static readonly string[] QualityLabels =
            { "Default", "Low", "Medium", "High", "Very High", "Ultra" };

        /// <summary>Highest frame cap the slider offers. 0 means "no cap".</summary>
        private const int MaxFpsCap = 360;

        // Built once — OnPaint runs on every repaint, so never allocate fonts inside it.
        private static readonly Font TitleFont = new(Theme.FontFamily, 15f, FontStyle.Bold);
        private static readonly Font SubtitleFont = new(Theme.FontFamily, 8.5f);
        private static readonly Font SectionFont = new(Theme.FontFamily, 8f, FontStyle.Bold);
        private static readonly Font CaptionFont = new(Theme.FontFamily, 8f, FontStyle.Bold);
        private static readonly Font ValueFont = new(Theme.FontFamily, 10f, FontStyle.Bold);
        private static readonly Font StatusFont = new(Theme.FontFamily, 8f, FontStyle.Bold);

        public ProfileEditorPage()
        {
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            Font = new Font(Theme.FontFamily, 9.5f);

            _gamePicker = new GlassDropdown { Placeholder = "No supported game detected" };
            _gamePicker.SelectedIndexChanged += (_, _) => OnGameChanged();
            Controls.Add(_gamePicker);

            // Vibrance and Saturation share the engine's own 0–200 range: the notch at
            // 100 marks where the NVIDIA driver runs out and software takes over, which
            // is the same story the Vibrance page tells.
            // Every visual slider starts neutral (100%), never at its minimum - a page
            // that opens on 0% vibrance invites the user to save a black-and-white
            // profile without realising it.
            _vibrance = MakeSlider(0, VibranceEngine.MaxVibrance, VibranceEngine.DriverVibranceCeiling, initial: 100);
            _saturation = MakeSlider(0, VibranceEngine.MaxSaturation, notch: 100, initial: 100);
            _brightness = MakeSlider(VibranceEngine.MinBrightness, VibranceEngine.MaxBrightness, notch: 100, initial: 100);
            _gamma = MakeSlider(VibranceEngine.MinGamma, VibranceEngine.MaxGamma, notch: 100, initial: 100);
            _fpsCap = MakeSlider(0, MaxFpsCap, notch: null, initial: 0);

            for (int i = 0; i < QualityLabels.Length; i++)
            {
                int index = i;
                var chip = new ChipButton
                {
                    Text = QualityLabels[i],
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Active = i == 0,
                };
                chip.Click += (_, _) => SelectQuality(index);
                _qualityChips.Add(chip);
                Controls.Add(chip);
            }

            _saveButton = new GlassButton { Text = "Save profile", Kind = GlassButtonKind.Primary };
            _saveButton.Click += (_, _) => Save();
            Controls.Add(_saveButton);

            _cancelButton = new GlassButton { Text = "Cancel", Kind = GlassButtonKind.Ghost };
            _cancelButton.Click += (_, _) => OnCancelled?.Invoke(this, EventArgs.Empty);
            Controls.Add(_cancelButton);

            Resize += (_, _) => LayoutContent();
            HandleCreated += (_, _) => LayoutContent();
        }

        private FlatSlider MakeSlider(int min, int max, int? notch, int initial)
        {
            var slider = new FlatSlider
            {
                Minimum = min,
                Maximum = max,
                Notch = notch,
                Value = Math.Clamp(initial, min, max),
            };
            // The value chip is drawn in OnPaint, so a repaint is all a change needs.
            slider.ValueChanged += (_, _) => Invalidate();
            Controls.Add(slider);
            return slider;
        }

        // ---- Layout ----

        private void LayoutContent()
        {
            if (Width <= 0 || Height <= 0) return;
            _m = ProfileEditorLayout.Compute(Width, Height);

            _gamePicker.Bounds = _m.GameControl;

            PlaceSliderRow(_vibrance, _m.VisualsRow(0));
            PlaceSliderRow(_saturation, _m.VisualsRow(1));
            PlaceSliderRow(_brightness, _m.VisualsRow(2));
            PlaceSliderRow(_gamma, _m.VisualsRow(3));
            PlaceSliderRow(_fpsCap, _m.HubFpsRow);

            LayoutQualityChips();
            LayoutFooterButtons();

            Invalidate();
        }

        private void PlaceSliderRow(FlatSlider slider, Rectangle row)
        {
            var (_, track, _) = _m.SplitRow(row);
            slider.Bounds = track;
        }

        /// <summary>The six presets sit in the space left over after the row caption, so
        /// the chip strip lines up with the FPS slider track directly below it.</summary>
        private void LayoutQualityChips()
        {
            var (_, strip, value) = _m.SplitRow(_m.HubChipsRow);
            // Chips run all the way to the row's right edge — there is no value chip on
            // this row, so reclaim that column too.
            int available = value.Right - strip.Left;
            int gap = _m.Density == EditorDensity.Comfortable ? 8 : 6;
            int count = _qualityChips.Count;
            int chipW = (available - (count - 1) * gap) / count;
            int chipH = Math.Min(_m.ControlHeight, 32);
            int y = _m.HubChipsRow.Y + (_m.HubChipsRow.Height - chipH) / 2;

            for (int i = 0; i < count; i++)
                _qualityChips[i].SetBounds(strip.Left + i * (chipW + gap), y, chipW, chipH);
        }

        private void LayoutFooterButtons()
        {
            int h = _m.Density == EditorDensity.Comfortable ? 36 : 32;
            int y = _m.Footer.Y + (_m.Footer.Height - h) / 2;

            _saveButton.SetBounds(_m.Footer.Right - FooterSaveWidth, y, FooterSaveWidth, h);
            _cancelButton.SetBounds(_saveButton.Left - 10 - FooterCancelWidth, y, FooterCancelWidth, h);
        }

        /// <summary>Footer button widths. Wide enough that "Save profile" never ellipsises
        /// at the compact density.</summary>
        internal const int FooterSaveWidth = 132;
        internal const int FooterCancelWidth = 92;

        // ---- Painting ----

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);   // particle field + user background image
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            PaintHeader(g);

            PaintCard(g, _m.GameCard, "GAME");
            PaintCard(g, _m.VisualsCard, "VISUALS");
            PaintCard(g, _m.HubCard, "GAME HUB");

            PaintSliderRow(g, _m.VisualsRow(0), "VIBRANCE", _vibrance.Value, decimals: 0, suffix: "%");
            PaintSliderRow(g, _m.VisualsRow(1), "SATURATION", _saturation.Value, decimals: 0, suffix: "%");
            PaintSliderRow(g, _m.VisualsRow(2), "BRIGHTNESS", _brightness.Value, decimals: 0, suffix: "%");
            PaintSliderRow(g, _m.VisualsRow(3), "GAMMA", _gamma.Value, decimals: 2, suffix: "");

            // Quality row: caption only — the chips draw themselves.
            var (qCaption, _, _) = _m.SplitRow(_m.HubChipsRow);
            DrawCaption(g, qCaption, "QUALITY");

            var (fCaption, _, fValue) = _m.SplitRow(_m.HubFpsRow);
            DrawCaption(g, fCaption, "FPS CAP");
            TextRenderer.DrawText(g, FormatFpsCap(_fpsCap.Value), ValueFont, fValue,
                _fpsCap.Value == 0 ? Theme.TextDim : Theme.Accent,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        private void PaintHeader(Graphics g)
        {
            var h = _m.Header;
            TextRenderer.DrawText(g, "Profile Editor", TitleFont,
                new Rectangle(h.X, h.Y + 8, h.Width, 24), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, "Applied automatically the moment this game launches.", SubtitleFont,
                new Rectangle(h.X, h.Y + 32, h.Width - 170, 18), Theme.TextDim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            PaintStatusPill(g, h);
        }

        /// <summary>
        /// Auto-apply state as a pill in the header rather than a line of text pinned to
        /// the bottom of the window: it belongs to the page's identity ("is this thing
        /// live?"), not to the save action.
        /// </summary>
        private void PaintStatusPill(Graphics g, Rectangle header)
        {
            var text = _watcherRunning ? "AUTO-APPLY ON" : "AUTO-APPLY OFF";
            var size = TextRenderer.MeasureText(text, StatusFont);
            int pillW = size.Width + 34;
            int pillH = 24;
            var pill = new RectangleF(header.Right - pillW, header.Y + 14, pillW, pillH);

            Glass.PaintPanel(g, pill, pillH / 2f, fillAlpha: 150);

            var dot = new RectangleF(pill.X + 12, pill.Y + pillH / 2f - 3.5f, 7, 7);
            using (var brush = new SolidBrush(_watcherRunning ? Theme.Accent : Theme.TextDim))
                g.FillEllipse(brush, dot);

            TextRenderer.DrawText(g, text, StatusFont,
                new Rectangle((int)pill.X + 25, (int)pill.Y, pillW - 30, pillH),
                _watcherRunning ? Theme.Text : Theme.TextDim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private void PaintCard(Graphics g, Rectangle card, string caption)
        {
            Glass.PaintPanel(g, new RectangleF(card.X + 0.5f, card.Y + 0.5f, card.Width - 1, card.Height - 1),
                             14, fillAlpha: 158);

            TextRenderer.DrawText(g, UiHelpers.Spaced(caption), SectionFont,
                new Rectangle(card.X + _m.CardPadding, card.Y + _m.CardPadding, card.Width, _m.CaptionHeight),
                Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private void PaintSliderRow(Graphics g, Rectangle row, string caption, int value, int decimals, string suffix)
        {
            var (captionRect, _, valueRect) = _m.SplitRow(row);
            DrawCaption(g, captionRect, caption);
            TextRenderer.DrawText(g, FormatValue(value, decimals, suffix), ValueFont, valueRect,
                Theme.Accent, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        private static void DrawCaption(Graphics g, Rectangle rect, string caption) =>
            TextRenderer.DrawText(g, UiHelpers.Spaced(caption), CaptionFont, rect, Theme.TextDim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        // ---- Formatting (pure; pinned by tests) ----

        /// <summary>Format a slider value for its chip. Czech-locale machines render
        /// "1,10" rather than "1.10", so the decimal goes through the current culture
        /// instead of a hard-coded separator.</summary>
        internal static string FormatValue(int value, int decimals, string suffix) =>
            (decimals == 0 ? value.ToString() : (value / 100.0).ToString("F" + decimals)) + suffix;

        /// <summary>A cap of 0 means "don't cap", which reads far better as "Off" than
        /// as a bare zero next to a slider pinned to its left edge.</summary>
        internal static string FormatFpsCap(int value) => value == 0 ? "Off" : value.ToString();

        // ---- Public API (unchanged across every redesign) ----

        public void PopulateGames(IEnumerable<(string Id, string Name)> games)
        {
            _games.Clear();
            var names = new List<string>();
            foreach (var g in games)
            {
                _games.Add(g);
                names.Add(g.Name);
            }
            _gamePicker.SetItems(names);
        }

        public void SelectGame(string gameId)
        {
            for (int i = 0; i < _games.Count; i++)
            {
                if (_games[i].Id != gameId) continue;
                _gamePicker.SelectedIndex = i;
                return;
            }
        }

        public void SetStatus(bool watcherRunning)
        {
            _watcherRunning = watcherRunning;
            Invalidate();
        }

        public void LoadProfile(GameProfile profile)
        {
            _vibrance.Value = Math.Clamp(profile.Vibrance, _vibrance.Minimum, _vibrance.Maximum);
            _saturation.Value = Math.Clamp(profile.Saturation, _saturation.Minimum, _saturation.Maximum);
            _brightness.Value = Math.Clamp(profile.Brightness, _brightness.Minimum, _brightness.Maximum);
            _gamma.Value = Math.Clamp(profile.Gamma, _gamma.Minimum, _gamma.Maximum);

            if (profile.GameHub != null)
            {
                int qIdx = Array.IndexOf(QualityOptions, profile.GameHub.GraphicsQuality);
                SelectQuality(qIdx >= 0 ? qIdx : 0);
                _fpsCap.Value = Math.Clamp(profile.GameHub.FpsCap, 0, MaxFpsCap);
            }
            Invalidate();
        }

        // ---- Behaviour ----

        /// <summary>
        /// Switching game reloads that game's saved profile. Without this the editor
        /// opened on whatever the sliders happened to be at - which, on a fresh page,
        /// was each slider's MINIMUM (0% vibrance, 0% saturation, 0.50 gamma). Pressing
        /// Save from there silently overwrote a good profile with a black-and-white
        /// screen, so this is a data-loss fix as much as a usability one.
        /// </summary>
        private void OnGameChanged()
        {
            int idx = _gamePicker.SelectedIndex;
            var saved = idx >= 0 && idx < _games.Count
                ? GameProfileStore.Get(_games[idx].Id)
                : null;
            LoadProfile(saved ?? NeutralProfile());
        }

        /// <summary>
        /// What an unconfigured game starts at: every control neutral (100%), no quality
        /// override, no frame cap. Deliberately the same values <see cref="GameProfile"/>
        /// itself defaults to, so "never saved" and "saved straight away without
        /// touching anything" produce an identical profile.
        /// </summary>
        internal static GameProfile NeutralProfile() => new();

        private void SelectQuality(int index)
        {
            for (int i = 0; i < _qualityChips.Count; i++)
                _qualityChips[i].Active = i == index;
            Invalidate();
        }

        private int SelectedQualityIndex()
        {
            for (int i = 0; i < _qualityChips.Count; i++)
                if (_qualityChips[i].Active) return i;
            return 0;
        }

        private void Save()
        {
            var idx = _gamePicker.SelectedIndex;
            if (idx < 0 || idx >= _games.Count) return;
            var (id, name) = _games[idx];

            int q = SelectedQualityIndex();

            var profile = new GameProfile
            {
                GameId = id,
                DisplayName = name,
                Vibrance = _vibrance.Value,
                Saturation = _saturation.Value,
                Brightness = _brightness.Value,
                Gamma = _gamma.Value,
                GameHub = new GameHubOptions
                {
                    // Index 0 is "(default)", persisted as an empty string so a profile
                    // that never touched the preset doesn't pin the game to one.
                    GraphicsQuality = q <= 0 ? "" : QualityOptions[q],
                    FpsCap = _fpsCap.Value,
                },
                LastUpdated = DateTime.UtcNow,
            };
            GameProfileStore.Set(profile);
            OnSaved?.Invoke(this, EventArgs.Empty);
        }
    }
}
