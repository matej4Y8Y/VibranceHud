using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud.Pages
{
    /// <summary>
    /// The home page: the big vibrance readout and caption (drawn text), the 0-200% slider
    /// (notch at 100 = driver max), and the presets - all over the shared particle-field
    /// background from <see cref="GlowPage"/>.
    /// </summary>
    public sealed class VibrancePage : GlowPage
    {
        private readonly VibranceEngine _engine;
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;

        private readonly FlatSlider _slider;
        private readonly List<ChipButton> _chips = new();

        private int _cx, _colW, _numberY, _captionY, _scaleY, _presetCapY;

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

            Resize += (s, e) => LayoutContent();
            HandleCreated += (s, e) => LayoutContent();
        }

        private void LayoutContent()
        {
            _colW = Math.Min(560, Width - 80);
            _cx = (Width - _colW) / 2;
            int top = Math.Max(30, (Height - 380) / 2);

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

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); // particle-field background
            var g = e.Graphics;

            // Frosted-glass panel behind the content - the plexus shows through it, dimmed.
            var panel = new RectangleF(_cx - 36, _numberY - 28, _colW + 72, 300);
            Glass.PaintPanel(g, panel, 24, fillAlpha: 165);

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (var numFont = new Font(Theme.FontFamily, 46f, FontStyle.Bold))
                TextRenderer.DrawText(g, $"{_slider.Value}%", numFont,
                    new Rectangle(_cx, _numberY, _colW, 84), Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            using (var capFont = new Font(Theme.FontFamily, 8f, FontStyle.Bold))
                TextRenderer.DrawText(g, UiHelpers.Spaced("DIGITAL VIBRANCE"), capFont,
                    new Rectangle(_cx, _captionY, _colW, 16), Theme.TextDim, TextFormatFlags.HorizontalCenter);

            using (var small = new Font(Theme.FontFamily, 8f))
            {
                TextRenderer.DrawText(g, "0", small, new Rectangle(_cx, _scaleY, 40, 14), Theme.TextDim, TextFormatFlags.Left);
                TextRenderer.DrawText(g, "100", small, new Rectangle(_cx, _scaleY, _colW, 14), Theme.TextDim, TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(g, "200", small, new Rectangle(_cx + _colW - 40, _scaleY, 40, 14), Theme.TextDim, TextFormatFlags.Right);
                TextRenderer.DrawText(g, UiHelpers.Spaced("PRESETS"),
                    new Font(Theme.FontFamily, 8f, FontStyle.Bold),
                    new Rectangle(_cx, _presetCapY, 200, 16), Theme.TextDim, TextFormatFlags.Left);
            }
        }

        private void UpdateActiveChip()
        {
            foreach (var chip in _chips)
                chip.Active = chip.Level == _slider.Value;
        }

        public void Refresh(int level)
        {
            _slider.Value = Math.Clamp(level, 0, VibranceEngine.Max);
            UpdateActiveChip();
            Invalidate();
        }
    }
}
