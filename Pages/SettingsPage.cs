using System;
using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud.Pages
{
    /// <summary>
    /// App-level settings: launch with Windows, the window's translucency, and the manual
    /// update check. Vibrance itself lives on the Vibrance page.
    /// </summary>
    public sealed class SettingsPage : GlowPage
    {
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly Action<int> _onOpacityChanged;
        private readonly Action<bool> _onThemeChanged;

        public SettingsPage(AppSettings settings, SettingsStore store,
            Action<int> onOpacityChanged, Action<bool> onThemeChanged)
        {
            _settings = settings;
            _store = store;
            _onOpacityChanged = onOpacityChanged;
            _onThemeChanged = onThemeChanged;

            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            AutoScroll = true;
            Font = new Font(Theme.FontFamily, 9.5f);
            Padding = new Padding(40, 32, 40, 32);

            int width = 620;

            var general = new CardPanel { Location = new Point(40, 40), Size = new Size(width, 132) };
            general.Controls.Add(UiHelpers.Caption("GENERAL", 18, 16, 200));
            general.Controls.Add(new Label
            {
                Text = "Launch with Windows",
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Location = new Point(18, 46),
                AutoSize = true
            });
            var startupToggle = new ToggleSwitch
            {
                Location = new Point(width - 62, 44),
                Checked = StartupManager.IsEnabled()
            };
            startupToggle.CheckedChanged += (s, e) =>
            {
                StartupManager.SetEnabled(startupToggle.Checked);
                _settings.StartWithWindows = startupToggle.Checked;
                _store.Save(_settings);
            };
            general.Controls.Add(startupToggle);

            general.Controls.Add(new Label
            {
                Text = "Light theme (black & white)",
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Location = new Point(18, 88),
                AutoSize = true
            });
            var themeToggle = new ToggleSwitch
            {
                Location = new Point(width - 62, 86),
                Checked = _settings.LightTheme
            };
            themeToggle.CheckedChanged += (s, e) => _onThemeChanged(themeToggle.Checked);
            general.Controls.Add(themeToggle);
            Controls.Add(general);

            var appearance = new CardPanel { Location = new Point(40, 192), Size = new Size(width, 108) };
            appearance.Controls.Add(UiHelpers.Caption("WINDOW OPACITY", 18, 16, 240));
            var opacityValue = new Label
            {
                Text = $"{Clamp(settings.OpacityPercent)}%",
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(width - 60, 16),
                Size = new Size(42, 16),
                TextAlign = ContentAlignment.MiddleRight
            };
            appearance.Controls.Add(opacityValue);
            var opacitySlider = new FlatSlider
            {
                Minimum = 50,
                Maximum = 100,
                Location = new Point(16, 52),
                Width = width - 32,
                Value = Clamp(settings.OpacityPercent)
            };
            opacitySlider.ValueChanged += (s, e) =>
            {
                _onOpacityChanged(opacitySlider.Value);
                _settings.OpacityPercent = opacitySlider.Value;
                opacityValue.Text = $"{opacitySlider.Value}%";
                _store.Save(_settings);
            };
            appearance.Controls.Add(opacitySlider);
            Controls.Add(appearance);

            var updates = new CardPanel { Location = new Point(40, 320), Size = new Size(width, 92) };
            updates.Controls.Add(UiHelpers.Caption("UPDATES", 18, 16, 200));
            var checkBtn = FlatButton("Check for updates", 18, 44, 180);
            checkBtn.Click += async (s, e) => await UpdateService.CheckManuallyAsync();
            updates.Controls.Add(checkBtn);
            Controls.Add(updates);
        }

        private static int Clamp(int pct) => Math.Clamp(pct, 50, 100);

        internal static Button FlatButton(string text, int x, int y, int width)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.SurfaceHover,
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 9f),
                Size = new Size(width, 32),
                Location = new Point(x, y),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Theme.Border;
            return b;
        }
    }
}
