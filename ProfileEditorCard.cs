using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// The "Set Profile" editor: a slide-in panel that lives in <c>MainWindow</c>,
    /// revealed by the nav button. Lets the user pick a supported game, dial in
    /// the four visual sliders and a hub-options surface for the picked game,
    /// and save everything as a <see cref="GameProfile"/> via
    /// <see cref="GameProfileStore"/>.
    ///
    /// The four visual sliders write to whatever slider values the picked game
    /// runs at; the hub options stay in the profile for forward-compat. The
    /// card's only responsibility here is persistence — the actual
    /// game-launch-time application lives in <see cref="ProfileApplyEngine"/>.
    /// </summary>
    public sealed class ProfileEditorCard : UserControl
    {
        public event EventHandler? OnSaved;
        public event EventHandler? OnCancelled;

        private ComboBox _gamePicker = null!;
        private FlatSlider _vibrance = null!;
        private FlatSlider _saturation = null!;
        private FlatSlider _brightness = null!;
        private FlatSlider _gamma = null!;
        private ComboBox _qualityPicker = null!;
        private NumericUpDown _fpsCap = null!;
        private Button _saveButton = null!;
        private Button _cancelButton = null!;
        private Label _statusLabel = null!;
        private Label _hubHeader = null!;
        private Control[] _hubControls = Array.Empty<Control>();

        /// <summary>Map of gameId → display name, used to label the picker rows.
        /// Populated once by the parent via <see cref="PopulateGames"/>.</summary>
        private readonly List<(string Id, string Name)> _games = new();

        private static readonly string[] QualityOptions =
            { "(default)", "low", "medium", "high", "very high", "ultra" };

        public ProfileEditorCard()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(30, 28, 36);
            DoubleBuffered = true;
            BuildLayout();
        }

        private void BuildLayout()
        {
            // Root layout: a 2-column TableLayoutPanel gives label/value rows like the
            // other settings pages.
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 12,
                Padding = new Padding(24),
                AutoSize = false
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Row 0: Game picker
            layout.Controls.Add(MakeLabel("Game"), 0, 0);
            _gamePicker = new ComboBox { Dock = DockStyle.Left, Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            _gamePicker.SelectedIndexChanged += (_, _) => OnGameChanged();
            layout.Controls.Add(_gamePicker, 1, 0);

            // Visual sliders (rows 1-4). The local helper returns the slider so
            // we can assign it to the field; the helper also wires it into the host.
            int row = 1;
            (_vibrance, _) = MakeSliderRow("Vibrance", 0, VibranceEngine.MaxVibrance, layout, row++);
            (_saturation, _) = MakeSliderRow("Saturation", 0, VibranceEngine.MaxSaturation, layout, row++);
            (_brightness, _) = MakeSliderRow("Brightness", VibranceEngine.MinBrightness, VibranceEngine.MaxBrightness, layout, row++);
            (_gamma, _) = MakeSliderRow("Gamma", VibranceEngine.MinGamma, VibranceEngine.MaxGamma, layout, row++);

            // Game-Hub sub-header
            _hubHeader = MakeLabel("Game-Hub options");
            _hubHeader.Font = new Font(Theme.FontFamily, 9f, FontStyle.Bold);
            _hubHeader.ForeColor = Theme.Accent;
            layout.SetColumnSpan(_hubHeader, 2);
            layout.Controls.Add(_hubHeader, 0, 5);

            // Graphics quality + FPS cap (rows 6-7)
            layout.Controls.Add(MakeLabel("Quality preset"), 0, 6);
            _qualityPicker = new ComboBox { Dock = DockStyle.Left, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            _qualityPicker.Items.AddRange(QualityOptions);
            _qualityPicker.SelectedIndex = 0;
            layout.Controls.Add(_qualityPicker, 1, 6);

            layout.Controls.Add(MakeLabel("FPS cap"), 0, 7);
            _fpsCap = new NumericUpDown { Dock = DockStyle.Left, Width = 120, Minimum = 0, Maximum = 999, Value = 0 };
            layout.Controls.Add(_fpsCap, 1, 7);

            // Status indicator (spans both cols)
            _statusLabel = new Label
            {
                Text = "Auto-apply paused",
                ForeColor = Color.Gray,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 16, 0, 0)
            };
            layout.SetColumnSpan(_statusLabel, 2);
            layout.Controls.Add(_statusLabel, 0, 8);

            // Save + Cancel buttons in the bottom row
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            _saveButton = new Button { Text = "Save profile", Width = 120, Height = 34, BackColor = Theme.AccentDim, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat };
            _saveButton.FlatAppearance.BorderSize = 0;
            _saveButton.Click += (_, _) => Save();
            _cancelButton = new Button { Text = "Cancel", Width = 100, Height = 34, BackColor = Theme.Surface, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat };
            _cancelButton.FlatAppearance.BorderSize = 0;
            _cancelButton.Click += (_, _) => OnCancelled?.Invoke(this, EventArgs.Empty);
            buttons.Controls.Add(_saveButton);
            buttons.Controls.Add(_cancelButton);
            layout.SetColumnSpan(buttons, 2);
            layout.Controls.Add(buttons, 0, 9);

            // Track which controls to hide when the picked game has no hub surface.
            _hubControls = new Control[] { _hubHeader, _qualityPicker, _fpsCap };

            // Resize leftover rows so the buttons sit at the bottom and there's
            // breathing room above them.
            for (int r = 8; r < layout.RowCount; r++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            Controls.Add(layout);
        }

        private Label MakeLabel(string text) => new()
        {
            Text = text,
            ForeColor = Theme.TextDim,
            Font = new Font(Theme.FontFamily, 9f),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };

        /// <summary>Build a labelled slider row and return the slider instance so the
        /// caller can assign it to a field.</summary>
        private (FlatSlider slider, TableLayoutPanel host) MakeSliderRow(string caption, int min, int max, TableLayoutPanel host, int row)
        {
            var slider = new FlatSlider { Dock = DockStyle.Left, Minimum = min, Maximum = max, Width = 320 };
            slider.Notch = 100; // mark the 100% boundary like the Vibrance page does
            host.Controls.Add(MakeLabel(caption), 0, row);
            host.Controls.Add(slider, 1, row);
            return (slider, host);
        }

        /// <summary>Replace the picker contents and select the first one.</summary>
        public void PopulateGames(IEnumerable<(string Id, string Name)> games)
        {
            _games.Clear();
            _gamePicker.Items.Clear();
            foreach (var g in games)
            {
                _games.Add(g);
                _gamePicker.Items.Add(g.Name);
            }
            if (_gamePicker.Items.Count > 0) _gamePicker.SelectedIndex = 0;
        }

        /// <summary>Pre-select a single game (used when the editor opens via an
        /// "Edit profile" button on a specific GameCard).</summary>
        public void SelectGame(string gameId)
        {
            for (int i = 0; i < _games.Count; i++)
            {
                if (_games[i].Id == gameId)
                {
                    _gamePicker.SelectedIndex = i;
                    return;
                }
            }
        }

        /// <summary>Update the watcher status dot. Called by MainWindow when the
        /// editor opens and on subsequent state changes.</summary>
        public void SetStatus(bool watcherRunning)
        {
            _statusLabel.Text = watcherRunning ? "\u25CF Auto-apply running" : "\u25CB Auto-apply paused";
            _statusLabel.ForeColor = watcherRunning ? Color.LightGreen : Color.Gray;
        }

        private void OnGameChanged()
        {
            // Per-game UX: hide the hub-quality row entirely for games without a
            // portable quality surface (CS2, Apex, Fortnite all use tweak toggles
            // rather than a single quality key). For v0.7.0 we keep the picker
            // visible but note that only Rust and CS2 consume the value today.
            // We always leave the controls enabled and let the applier no-op when
            // the picked game doesn't recognise the value — the user can still set
            // their preference and future game-specific support lifts the matrix.
            var picked = _gamePicker.SelectedItem?.ToString();
            var hasHub = picked != null; // all four known games have *some* surface
            foreach (var c in _hubControls) c.Visible = hasHub;
        }

        private void Save()
        {
            var idx = _gamePicker.SelectedIndex;
            if (idx < 0 || idx >= _games.Count) return;
            var (id, name) = _games[idx];

            var quality = _qualityPicker.SelectedIndex <= 0
                ? ""
                : QualityOptions[_qualityPicker.SelectedIndex];

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
                    GraphicsQuality = quality,
                    FpsCap = (int)_fpsCap.Value,
                },
                LastUpdated = DateTime.UtcNow,
            };
            GameProfileStore.Set(profile);
            OnSaved?.Invoke(this, EventArgs.Empty);
        }
    }
}
