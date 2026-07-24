using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud.Pages
{
    /// <summary>
    /// The home page: the big vibrance readout, the 0-200% slider (notch at 100 = driver
    /// max), the presets, and below them the brightness calibration slider and the eye-care
    /// toggle - all over the shared particle-field background from <see cref="GlowPage"/>.
    /// </summary>
    public sealed class VibrancePage : GlowPage
    {
        private readonly VibranceEngine _engine;
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;

        private readonly FlatSlider _slider;
        private readonly FlatSlider _brightness;
        private readonly FlatSlider _gamma;
        private readonly ToggleSwitch _eyeCare;
        private readonly List<ChipButton> _chips = new();

        private int _cx, _colW, _numberY, _captionY, _scaleY, _presetCapY, _brightCapY, _gammaCapY, _eyeY;

        // Built once - OnPaint runs ~30x/sec, so never allocate fonts inside it.
        private static readonly Font NumberFont = new(Theme.FontFamily, 46f, FontStyle.Bold);
        private static readonly Font CaptionFont = new(Theme.FontFamily, 8f, FontStyle.Bold);
        private static readonly Font SmallFont = new(Theme.FontFamily, 8f);
        private static readonly Font RowFont = new(Theme.FontFamily, 9.5f);

        public VibrancePage(VibranceEngine engine, AppSettings settings, SettingsStore store)
        {
            _engine = engine;
            _settings = settings;
            _store = store;
            Font = new Font(Theme.FontFamily, 9f);

            _slider = new FlatSlider
            {
                Minimum = 0,
                Maximum = VibranceEngine.Max,
                Notch = 100,
                Value = _engine.CurrentLevel
            };
            _slider.ValueChanged += (s, e) =>
            {
                _engine.SetLevel(_slider.Value);
                _settings.Level = _slider.Value;
                UpdateActiveChip();
                Invalidate();
            };
            Controls.Add(_slider);

            (string name, int level)[] presets =
            {
                ("Natural", 50), ("Standard", 100), ("Vivid", 150), ("Max", 200)
            };
            foreach (var (name, level) in presets)
            {
                var chip = new ChipButton { Text = name, Level = level, Font = new Font(Theme.FontFamily, 9f) };
                chip.Click += (s, e) => _slider.Value = level;
                _chips.Add(chip);
                Controls.Add(chip);
            }
            UpdateActiveChip();

            _brightness = new FlatSlider
            {
                Minimum = VibranceEngine.MinBrightness,
                Maximum = VibranceEngine.MaxBrightness,
                Notch = 100,
                Value = _engine.Brightness
            };
            _brightness.ValueChanged += (s, e) =>
            {
                _engine.Brightness = _brightness.Value;
                _settings.BrightnessPercent = _brightness.Value;
                Invalidate();
            };
            Controls.Add(_brightness);

            _gamma = new FlatSlider
            {
                Minimum = VibranceEngine.MinGamma,
                Maximum = VibranceEngine.MaxGamma,
                Notch = 100,
                Value = _engine.Gamma
            };
            _gamma.ValueChanged += (s, e) =>
            {
                _engine.Gamma = _gamma.Value;
                _settings.GammaPercent = _gamma.Value;
                Invalidate();
            };
            Controls.Add(_gamma);

            _eyeCare = new ToggleSwitch { Checked = _engine.EyeCare };
            _eyeCare.CheckedChanged += (s, e) =>
            {
                _engine.EyeCare = _eyeCare.Checked;
                _settings.EyeCare = _eyeCare.Checked;
                _store.Save(_settings);
            };
            Controls.Add(_eyeCare);

            Resize += (s, e) => LayoutContent();
            HandleCreated += (s, e) => LayoutContent();
        }

        private void LayoutContent()
        {
            _colW = Math.Min(560, Width - 80);
            _cx = (Width - _colW) / 2;
            int top = Math.Max(20, (Height - 560) / 2);

            _numberY = top;
            _captionY = top + 90;
            int sliderY = top + 128;
            _slider.SetBounds(_cx, sliderY, _colW, 32);
            _scaleY = sliderY + 34;
            _presetCapY = sliderY + 70;

            int chipW = (_colW - 3 * 10) / 4;
            int chipY = sliderY + 92;
            for (int i = 0; i < _chips.Count; i++)
                _chips[i].SetBounds(_cx + i * (chipW + 10), chipY, chipW, 36);

            _brightCapY = chipY + 56;
            _brightness.SetBounds(_cx, chipY + 78, _colW, 32);

            _gammaCapY = chipY + 122;
            _gamma.SetBounds(_cx, chipY + 144, _colW, 32);

            _eyeY = chipY + 196;
            _eyeCare.SetBounds(_cx + _colW - 44, _eyeY - 2, 44, 22);

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); // particle-field background
            var g = e.Graphics;

            // Frosted-glass panel behind the content - the plexus shows through it, dimmed.
            var panel = new RectangleF(_cx - 36, _numberY - 28, _colW + 72, 514);
            Glass.PaintPanel(g, panel, 24, fillAlpha: 165);

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            TextRenderer.DrawText(g, $"{_slider.Value}%", NumberFont,
                new Rectangle(_cx, _numberY, _colW, 84), Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, UiHelpers.Spaced("DIGITAL VIBRANCE"), CaptionFont,
                new Rectangle(_cx, _captionY, _colW, 16), Theme.TextDim, TextFormatFlags.HorizontalCenter);

            TextRenderer.DrawText(g, "0", SmallFont, new Rectangle(_cx, _scaleY, 40, 14), Theme.TextDim, TextFormatFlags.Left);
            TextRenderer.DrawText(g, "100", SmallFont, new Rectangle(_cx, _scaleY, _colW, 14), Theme.TextDim, TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, "200", SmallFont, new Rectangle(_cx + _colW - 40, _scaleY, 40, 14), Theme.TextDim, TextFormatFlags.Right);

            if (!_engine.DriverAvailable)
            {
                TextRenderer.DrawText(g, "0-100% needs an NVIDIA GPU · boost above 100% still works", SmallFont,
                    new Rectangle(_cx, _scaleY + 16, _colW, 14), Theme.TextDim, TextFormatFlags.HorizontalCenter);
            }

            TextRenderer.DrawText(g, UiHelpers.Spaced("PRESETS"), CaptionFont,
                new Rectangle(_cx, _presetCapY, 200, 16), Theme.TextDim, TextFormatFlags.Left);

            // ---- Brightness calibration ----
            TextRenderer.DrawText(g, UiHelpers.Spaced("BRIGHTNESS"), CaptionFont,
                new Rectangle(_cx, _brightCapY, 240, 16), Theme.TextDim, TextFormatFlags.Left);
            TextRenderer.DrawText(g, $"{_brightness.Value}%", SmallFont,
                new Rectangle(_cx + _colW - 50, _brightCapY, 50, 16), Theme.TextDim, TextFormatFlags.Right);

            // ---- Gamma ----
            TextRenderer.DrawText(g, UiHelpers.Spaced("GAMMA"), CaptionFont,
                new Rectangle(_cx, _gammaCapY, 240, 16), Theme.TextDim, TextFormatFlags.Left);
            TextRenderer.DrawText(g, $"{_gamma.Value / 100f:0.00}", SmallFont,
                new Rectangle(_cx + _colW - 50, _gammaCapY, 50, 16), Theme.TextDim, TextFormatFlags.Right);

            // ---- Eye care ----
            TextRenderer.DrawText(g, "Eye care  (warm light)", RowFont,
                new Rectangle(_cx, _eyeY, 300, 20), Theme.Text, TextFormatFlags.Left);
        }

        private void UpdateActiveChip()
        {
            foreach (var chip in _chips)
                chip.Active = chip.Level == _slider.Value;
        }

        public void Refresh(int level)
        {
            _slider.Value = Math.Clamp(level, 0, VibranceEngine.Max);
            _brightness.Value = _engine.Brightness;
            _gamma.Value = _engine.Gamma;
            _eyeCare.Checked = _engine.EyeCare;
            UpdateActiveChip();
            Invalidate();
        }
    }
}
