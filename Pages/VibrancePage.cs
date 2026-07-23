using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud.Pages
{
    /// <summary>
    /// The home page: the vibrance value readout, the 0-200% slider (notch at 100 = driver
    /// max), and the one-click presets. Same behaviour as the old popup, now hosted in the
    /// big window.
    /// </summary>
    public sealed class VibrancePage : UserControl
    {
        private readonly VibranceEngine _engine;
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;

        private readonly Panel _content;
        private readonly Label _value;
        private readonly FlatSlider _slider;
        private readonly List<ChipButton> _chips = new();

        public VibrancePage(VibranceEngine engine, AppSettings settings, SettingsStore store)
        {
            _engine = engine;
            _settings = settings;
            _store = store;

            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Font = new Font(Theme.FontFamily, 9f);

            _content = new Panel { Size = new Size(480, 320), BackColor = Theme.Background };

            _value = new Label
            {
                Text = $"{_engine.CurrentLevel}%",
                ForeColor = Theme.Accent,
                Font = new Font(Theme.FontFamily, 40f, FontStyle.Bold),
                Location = new Point(0, 0),
                Size = new Size(480, 66),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            _content.Controls.Add(_value);
            _content.Controls.Add(UiHelpers.Caption("DIGITAL VIBRANCE", 0, 72, 480, ContentAlignment.MiddleCenter));

            _slider = new FlatSlider
            {
                Minimum = 0,
                Maximum = VibranceEngine.Max,
                Notch = 100,
                Location = new Point(0, 104),
                Width = 480,
                Value = _engine.CurrentLevel
            };
            _slider.ValueChanged += (s, e) =>
            {
                _engine.SetLevel(_slider.Value);
                _settings.Level = _slider.Value;
                _value.Text = $"{_slider.Value}%";
                UpdateActiveChip();
            };
            _content.Controls.Add(_slider);

            _content.Controls.Add(Scale("0", 0, 138, ContentAlignment.MiddleLeft));
            _content.Controls.Add(Scale("100", 480 / 2 - 20, 138, ContentAlignment.MiddleCenter));
            _content.Controls.Add(Scale("200", 480 - 40, 138, ContentAlignment.MiddleRight));

            _content.Controls.Add(UiHelpers.Caption("PRESETS", 0, 178, 480));

            (string name, int level)[] presets =
            {
                ("Natural", 50), ("Standard", 100), ("Vivid", 150), ("Max", 200)
            };
            int chipWidth = (480 - 3 * 10) / 4;
            for (int i = 0; i < presets.Length; i++)
            {
                var (name, level) = presets[i];
                var chip = new ChipButton
                {
                    Text = name,
                    Level = level,
                    Font = new Font(Theme.FontFamily, 9f),
                    Size = new Size(chipWidth, 36),
                    Location = new Point(i * (chipWidth + 10), 202)
                };
                chip.Click += (s, e) => _slider.Value = level;
                _chips.Add(chip);
                _content.Controls.Add(chip);
            }
            UpdateActiveChip();

            Controls.Add(_content);
            Resize += (s, e) => CenterContent();
            HandleCreated += (s, e) => CenterContent();
        }

        private void CenterContent()
        {
            _content.Left = Math.Max(20, (Width - _content.Width) / 2);
            _content.Top = Math.Max(20, (Height - _content.Height) / 2 - 20);
        }

        private void UpdateActiveChip()
        {
            foreach (var chip in _chips)
                chip.Active = chip.Level == _slider.Value;
        }

        private static Label Scale(string text, int x, int y, ContentAlignment align) => new()
        {
            Text = text,
            ForeColor = Theme.TextDim,
            Font = new Font(Theme.FontFamily, 8f),
            Location = new Point(x, y),
            Size = new Size(40, 14),
            TextAlign = align,
            BackColor = Color.Transparent
        };

        /// <summary>Called by the window when the page is shown, to resync with the engine.</summary>
        public void Refresh(int level)
        {
            _slider.Value = Math.Clamp(level, 0, VibranceEngine.Max);
            _value.Text = $"{_slider.Value}%";
            UpdateActiveChip();
        }
    }
}
