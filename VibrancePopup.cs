using System;
using System.Drawing;
using System.Windows.Forms;
using VibranceHud.Pages;

namespace VibranceHud
{
    /// <summary>
    /// The compact quick-adjust popup opened by the global Ctrl+Alt+V hotkey - a small,
    /// always-on-top window with the four visual sliders. Same category as a Discord
    /// overlay: no code injection, no game-process access, just a normal top-level window.
    ///
    /// Slider drags write straight to the shared <see cref="IVibranceEngine"/>, exactly like
    /// the full Vibrance page, so the effect is visible immediately. Deliberately depends on
    /// nothing from the auto-apply path (<c>ProfileApplyEngine</c>, <c>GameProfileStore</c>,
    /// <c>ProfileEngineCoordinator</c>) - a quick manual tweak here must never register as,
    /// or get silently clobbered by, a game's auto-applied profile.
    /// </summary>
    public sealed class VibrancePopup : Form
    {
        private readonly IVibranceEngine _engine;
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;

        internal FlatSlider VibranceSlider { get; }
        internal FlatSlider SaturationSlider { get; }
        internal FlatSlider BrightnessSlider { get; }
        internal FlatSlider GammaSlider { get; }

        public VibrancePopup(IVibranceEngine engine, AppSettings settings, SettingsStore store)
        {
            _engine = engine;
            _settings = settings;
            _store = store;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(320, 316);
            BackColor = Theme.Background;
            DoubleBuffered = true;
            Icon = AppIcon.Value;

            Controls.Add(new Label
            {
                Text = "Quick Vibrance",
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 12f, FontStyle.Bold),
                BackColor = Color.Transparent,
                Location = new Point(20, 16),
                AutoSize = true,
            });

            int y = 52;
            VibranceSlider = AddSliderRow("VIBRANCE", 0, VibranceEngine.MaxVibrance, _engine.Vibrance, ref y,
                v => _engine.Vibrance = v);
            SaturationSlider = AddSliderRow("SATURATION", 0, VibranceEngine.MaxSaturation, _engine.Saturation, ref y,
                v => _engine.Saturation = v);
            BrightnessSlider = AddSliderRow("BRIGHTNESS", VibranceEngine.MinBrightness, VibranceEngine.MaxBrightness, _engine.Brightness, ref y,
                v => _engine.Brightness = v);
            GammaSlider = AddSliderRow("GAMMA", VibranceEngine.MinGamma, VibranceEngine.MaxGamma, _engine.Gamma, ref y,
                v => _engine.Gamma = v);

            var saveBtn = SettingsPage.FlatButton("Save", 20, y + 10, 130);
            saveBtn.Click += (s, e) => Save();
            Controls.Add(saveBtn);

            var closeBtn = SettingsPage.FlatButton("Close", 168, y + 10, 132);
            closeBtn.Click += (s, e) => Close();
            Controls.Add(closeBtn);
        }

        private FlatSlider AddSliderRow(string caption, int min, int max, int value, ref int y, Action<int> onChange)
        {
            Controls.Add(new Label
            {
                Text = caption,
                ForeColor = Theme.TextDim,
                Font = new Font(Theme.FontFamily, 8f, FontStyle.Bold),
                BackColor = Color.Transparent,
                Location = new Point(20, y),
                AutoSize = true,
            });

            var valueLabel = new Label
            {
                Text = value.ToString(),
                ForeColor = Theme.TextDim,
                Font = new Font(Theme.FontFamily, 8.5f),
                BackColor = Color.Transparent,
                Location = new Point(260, y - 2),
                Size = new Size(40, 16),
                TextAlign = ContentAlignment.MiddleRight,
            };
            Controls.Add(valueLabel);

            var slider = new FlatSlider
            {
                Minimum = min,
                Maximum = max,
                Value = value,
                Location = new Point(18, y + 18),
                Width = 284,
            };
            slider.ValueChanged += (s, e) =>
            {
                onChange(slider.Value);
                valueLabel.Text = slider.Value.ToString();
            };
            Controls.Add(slider);

            y += 56;
            return slider;
        }

        /// <summary>Persists the four sliders' current values to AppSettings. Slider drags
        /// already updated the live engine; this just makes them survive a restart.
        /// Internal (not private) so tests can trigger it without simulating a click.</summary>
        internal void Save()
        {
            _settings.VibrancePercent = VibranceSlider.Value;
            _settings.SaturationPercent = SaturationSlider.Value;
            _settings.BrightnessPercent = BrightnessSlider.Value;
            _settings.GammaPercent = GammaSlider.Value;
            _store.Save(_settings);
        }
    }
}
