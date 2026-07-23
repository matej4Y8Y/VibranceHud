using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// The HUD itself: a borderless, rounded, translucent matte-black "glass card"
    /// that pops up near the cursor on the hotkey. Hosts the 0-200% vibrance slider,
    /// one-click preset pills (with a live active state), and app settings behind
    /// custom-drawn controls - no stock WinForms chrome anywhere.
    ///
    /// Same show/hide model as before: it never really closes while the app runs -
    /// clicking away, the close glyph, or Alt+F4 just hides it. Exit lives in the tray.
    /// </summary>
    public sealed class HudForm : Form
    {
        private const int CornerRadius = 16;
        private const int Pad = 20; // side padding - the breathing room the design needs

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private readonly VibranceEngine _engine;
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;

        private readonly Label _valueLabel;
        private readonly FlatSlider _vibranceSlider;
        private readonly FlatSlider _opacitySlider;
        private readonly Label _opacityValueLabel;
        private readonly List<ChipButton> _chips = new();

        public HudForm(VibranceEngine engine, AppSettings settings, SettingsStore store)
        {
            _engine = engine;
            _settings = settings;
            _store = store;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Text = "Vibrance HUD";
            BackColor = Theme.Background;
            ClientSize = new Size(340, 452);
            Opacity = Math.Clamp(settings.OpacityPercent, 50, 100) / 100.0;
            Font = new Font(Theme.FontFamily, 9f);

            int innerWidth = ClientSize.Width - 2 * Pad;

            // ---- Title bar (draggable) ----------------------------------------
            var titleBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(ClientSize.Width, 48),
                BackColor = Color.Transparent
            };
            titleBar.MouseDown += DragWindow;

            var titleAccent = new Label
            {
                Text = "VIBRANCE",
                ForeColor = Theme.Accent,
                Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold),
                Location = new Point(Pad, 15),
                AutoSize = true
            };
            titleAccent.MouseDown += DragWindow;

            var titleRest = new Label
            {
                Text = "HUD",
                ForeColor = Theme.TextDim,
                Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold),
                Location = new Point(Pad + 77, 15),
                AutoSize = true
            };
            titleRest.MouseDown += DragWindow;

            var closeButton = new Label
            {
                Text = "✕",
                ForeColor = Theme.TextDim,
                Font = new Font(Theme.FontFamily, 10f),
                Size = new Size(28, 24),
                Location = new Point(ClientSize.Width - Pad - 22, 13),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            closeButton.MouseEnter += (s, e) => closeButton.ForeColor = Theme.Text;
            closeButton.MouseLeave += (s, e) => closeButton.ForeColor = Theme.TextDim;
            closeButton.Click += (s, e) => HideAndSave();

            titleBar.Controls.Add(titleAccent);
            titleBar.Controls.Add(titleRest);
            titleBar.Controls.Add(closeButton);
            Controls.Add(titleBar);

            AddDivider(48);

            // ---- Big value readout --------------------------------------------
            _valueLabel = new Label
            {
                Text = $"{_engine.CurrentLevel}%",
                ForeColor = Theme.Accent,
                Font = new Font(Theme.FontFamily, 27f, FontStyle.Bold),
                Location = new Point(0, 62),
                Size = new Size(ClientSize.Width, 46),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(_valueLabel);

            Controls.Add(MakeCaption(Spaced("DIGITAL VIBRANCE"), 110, centered: true));

            // ---- Vibrance slider ----------------------------------------------
            _vibranceSlider = new FlatSlider
            {
                Minimum = 0,
                Maximum = VibranceEngine.Max,
                Notch = 100,
                Location = new Point(Pad, 136),
                Width = innerWidth,
                Value = _engine.CurrentLevel
            };
            _vibranceSlider.ValueChanged += (s, e) =>
            {
                _engine.SetLevel(_vibranceSlider.Value);
                _settings.Level = _vibranceSlider.Value;
                _valueLabel.Text = $"{_vibranceSlider.Value}%";
                UpdateActiveChip();
            };
            Controls.Add(_vibranceSlider);

            Controls.Add(MakeScaleLabel("0", Pad, 170, ContentAlignment.MiddleLeft));
            Controls.Add(MakeScaleLabel("100", ClientSize.Width / 2 - 20, 170, ContentAlignment.MiddleCenter));
            Controls.Add(MakeScaleLabel("200", ClientSize.Width - Pad - 40, 170, ContentAlignment.MiddleRight));

            AddDivider(198);

            // ---- Presets -------------------------------------------------------
            Controls.Add(MakeCaption(Spaced("PRESETS"), 212));

            (string name, int level)[] presets =
            {
                ("Natural", 50), ("Standard", 100), ("Vivid", 150), ("Max", 200)
            };
            int chipWidth = (innerWidth - 3 * 8) / 4;
            for (int i = 0; i < presets.Length; i++)
            {
                var (name, level) = presets[i];
                var chip = new ChipButton
                {
                    Text = name,
                    Level = level,
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Size = new Size(chipWidth, 32),
                    Location = new Point(Pad + i * (chipWidth + 8), 236)
                };
                chip.Click += (s, e) => _vibranceSlider.Value = level;
                _chips.Add(chip);
                Controls.Add(chip);
            }
            UpdateActiveChip();

            AddDivider(292);

            // ---- Settings ------------------------------------------------------
            Controls.Add(MakeCaption(Spaced("SETTINGS"), 306));

            var startupLabel = new Label
            {
                Text = "Launch with Windows",
                ForeColor = Theme.Text,
                Location = new Point(Pad, 332),
                AutoSize = true
            };
            Controls.Add(startupLabel);

            var startupToggle = new ToggleSwitch
            {
                Location = new Point(ClientSize.Width - Pad - 44, 330),
                Checked = StartupManager.IsEnabled()
            };
            startupToggle.CheckedChanged += (s, e) =>
            {
                StartupManager.SetEnabled(startupToggle.Checked);
                _settings.StartWithWindows = startupToggle.Checked;
            };
            Controls.Add(startupToggle);

            Controls.Add(MakeCaption(Spaced("WINDOW OPACITY"), 368));

            _opacityValueLabel = new Label
            {
                Text = $"{Math.Clamp(settings.OpacityPercent, 50, 100)}%",
                ForeColor = Theme.TextDim,
                Font = new Font(Theme.FontFamily, 8f),
                Location = new Point(ClientSize.Width - Pad - 34, 368),
                Size = new Size(34, 16),
                TextAlign = ContentAlignment.MiddleRight
            };
            Controls.Add(_opacityValueLabel);

            _opacitySlider = new FlatSlider
            {
                Minimum = 50,
                Maximum = 100,
                Location = new Point(Pad, 390),
                Width = innerWidth,
                Value = Math.Clamp(settings.OpacityPercent, 50, 100)
            };
            _opacitySlider.ValueChanged += (s, e) =>
            {
                Opacity = _opacitySlider.Value / 100.0;
                _settings.OpacityPercent = _opacitySlider.Value;
                _opacityValueLabel.Text = $"{_opacitySlider.Value}%";
            };
            Controls.Add(_opacitySlider);

            // ---- Footer hint ---------------------------------------------------
            var hint = new Label
            {
                Text = "CTRL + ALT + V  —  TOGGLE HUD",
                ForeColor = Theme.TextDim,
                Font = new Font(Theme.FontFamily, 7.5f),
                Location = new Point(0, 428),
                Size = new Size(ClientSize.Width, 16),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(hint);

            // Popping up steals focus by design - losing it again means "click away to dismiss".
            Deactivate += (s, e) => HideAndSave();
        }

        /// <summary>"PRESETS" -> "P R E S E T S" with wider gaps between words.</summary>
        private static string Spaced(string text) =>
            string.Join("   ", text.Split(' ').Select(w => string.Join(" ", w.ToCharArray())));

        private void UpdateActiveChip()
        {
            foreach (var chip in _chips)
            {
                chip.Active = chip.Level == _vibranceSlider.Value;
            }
        }

        private void AddDivider(int y)
        {
            Controls.Add(new Panel
            {
                Location = new Point(Pad, y),
                Size = new Size(ClientSize.Width - 2 * Pad, 1),
                BackColor = Theme.Border
            });
        }

        private Label MakeCaption(string text, int y, bool centered = false) => new()
        {
            Text = text,
            ForeColor = Theme.TextDim,
            Font = new Font(Theme.FontFamily, 8f, FontStyle.Bold),
            Location = new Point(centered ? 0 : Pad, y),
            Size = centered ? new Size(ClientSize.Width, 16) : new Size(220, 16),
            TextAlign = centered ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft
        };

        private static Label MakeScaleLabel(string text, int x, int y, ContentAlignment align) => new()
        {
            Text = text,
            ForeColor = Theme.TextDim,
            Font = new Font(Theme.FontFamily, 7.5f),
            Location = new Point(x, y),
            Size = new Size(40, 14),
            TextAlign = align
        };

        private void DragWindow(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        private void HideAndSave()
        {
            Hide();
            _store.Save(_settings);
        }

        public void ShowNearCursor()
        {
            var cursor = Cursor.Position;
            var target = new Point(cursor.X - Width / 2, cursor.Y - Height - 12);

            // Keep it on-screen if the cursor is near a screen edge.
            var screen = Screen.FromPoint(cursor).WorkingArea;
            target.X = Math.Max(screen.Left, Math.Min(target.X, screen.Right - Width));
            target.Y = Math.Max(screen.Top, Math.Min(target.Y, screen.Bottom - Height));
            Location = target;

            _vibranceSlider.Value = Math.Clamp(_engine.CurrentLevel, 0, VibranceEngine.Max);
            _valueLabel.Text = $"{_engine.CurrentLevel}%";
            UpdateActiveChip();

            Show();
            Activate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyRoundedRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundedRegion();
        }

        private void ApplyRoundedRegion()
        {
            using var path = RoundedPath(new Rectangle(0, 0, Width, Height), CornerRadius);
            Region = new Region(path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Glass rim: faint white edge = light catching the border of a translucent
            // card. This is what makes the dark panel read as a physical object.
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
            using var pen = new Pen(Theme.GlassEdge, 1f);
            e.Graphics.DrawPath(pen, path);
        }

        private static GraphicsPath RoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // The window never really "closes" while the app is running - Alt+F4 just
            // hides it, same as clicking away. Actual exit goes through the tray icon.
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideAndSave();
                return;
            }

            base.OnFormClosing(e);
        }
    }
}
