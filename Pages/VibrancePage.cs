using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VibranceHud.Pages
{
    /// <summary>
    /// The home page: an animated purple "dot-wave" glow behind a big vibrance readout,
    /// the 0-200% slider (notch at 100 = driver max), and the presets. The number, caption
    /// and glow are drawn directly (one double-buffered surface) so the animation stays
    /// smooth; the slider and preset chips are real child controls layered on top.
    ///
    /// The animation only runs while the page is actually on screen - it pauses when the
    /// window is hidden or minimized, so it costs no CPU while gaming.
    /// </summary>
    public sealed class VibrancePage : UserControl
    {
        private readonly VibranceEngine _engine;
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;

        private readonly FlatSlider _slider;
        private readonly List<ChipButton> _chips = new();
        private readonly System.Windows.Forms.Timer _timer;
        private double _phase;

        // Layout, recomputed on resize; shared by Layout() and OnPaint().
        private int _cx, _colW, _numberY, _captionY, _scaleY, _presetCapY;

        public VibrancePage(VibranceEngine engine, AppSettings settings, SettingsStore store)
        {
            _engine = engine;
            _settings = settings;
            _store = store;

            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Font = new Font(Theme.FontFamily, 9f);
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw, true);

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
                Invalidate(); // redraw the big number
            };
            Controls.Add(_slider);

            (string name, int level)[] presets =
            {
                ("Natural", 50), ("Standard", 100), ("Vivid", 150), ("Max", 200)
            };
            foreach (var (name, level) in presets)
            {
                var chip = new ChipButton
                {
                    Text = name,
                    Level = level,
                    Font = new Font(Theme.FontFamily, 9f)
                };
                chip.Click += (s, e) => _slider.Value = level;
                _chips.Add(chip);
                Controls.Add(chip);
            }
            UpdateActiveChip();

            Resize += (s, e) => Layout();
            HandleCreated += (s, e) => Layout();

            _timer = new System.Windows.Forms.Timer { Interval = 40 }; // ~25 fps
            _timer.Tick += (s, e) =>
            {
                if (!ShouldAnimate()) return;
                _phase += 0.06;
                Invalidate();
            };
            _timer.Start();
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

        private bool ShouldAnimate()
        {
            var form = FindForm();
            if (!Visible || form is not { Visible: true } || form.WindowState == FormWindowState.Minimized)
                return false;
            // Only animate while our window is actually in front - so tabbing into a game
            // pauses it completely (zero CPU).
            return GetForegroundWindow() == form.Handle;
        }

        private void Layout()
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
            var g = e.Graphics;
            using (var back = new SolidBrush(Theme.Background))
                g.FillRectangle(back, ClientRectangle);

            DrawGlow(g);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (var numFont = new Font(Theme.FontFamily, 46f, FontStyle.Bold))
                TextRenderer.DrawText(g, $"{_slider.Value}%", numFont,
                    new Rectangle(_cx, _numberY, _colW, 84), Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            using (var capFont = new Font(Theme.FontFamily, 8f, FontStyle.Bold))
                TextRenderer.DrawText(g, UiHelpers.Spaced("DIGITAL VIBRANCE"), capFont,
                    new Rectangle(_cx, _captionY, _colW, 16), Theme.TextDim,
                    TextFormatFlags.HorizontalCenter);

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

        /// <summary>The purple dot-wave: a wavy band of violet dots, brightest at centre,
        /// fading out to the sides and away from the ridge, gently shimmering.</summary>
        private void DrawGlow(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int centerX = Width / 2;
            int ridgeY = _numberY + 42;

            // Soft radial bloom behind the ridge.
            using (var bloom = new GraphicsPath())
            {
                int rw = Math.Min(Width, 900), rh = 300;
                var bounds = new Rectangle(centerX - rw / 2, ridgeY - rh / 2, rw, rh);
                bloom.AddEllipse(bounds);
                using var pgb = new PathGradientBrush(bloom)
                {
                    CenterColor = Color.FromArgb(46, Theme.Accent),
                    SurroundColors = new[] { Color.FromArgb(0, Theme.Accent) }
                };
                g.FillPath(pgb, bloom);
            }

            using var dot = new SolidBrush(Theme.Accent);
            const int step = 16;
            for (int x = 0; x <= Width; x += step)
            {
                double wave = Math.Sin(x * 0.012 + _phase) * 26
                            + Math.Sin(x * 0.005 - _phase * 0.6) * 14;
                double waveY = ridgeY + wave;

                double dx = Math.Abs(x - centerX) / (Width * 0.55);
                double hFall = Math.Max(0, 1 - dx);
                hFall *= hFall; // steeper fade to the sides
                if (hFall <= 0.02) continue;

                for (int r = -3; r <= 3; r++)
                {
                    double vFall = 1 - Math.Abs(r) / 4.0;
                    double shimmer = 0.6 + 0.4 * Math.Sin(x * 0.02 + _phase * 2 + r);
                    int a = (int)(120 * hFall * vFall * shimmer);
                    if (a <= 3) continue;

                    dot.Color = Color.FromArgb(Math.Min(a, 200), Theme.Accent);
                    float size = r == 0 ? 3f : 2f;
                    g.FillEllipse(dot, (float)(x - size / 2), (float)(waveY + r * 11 - size / 2), size, size);
                }
            }
        }

        private void UpdateActiveChip()
        {
            foreach (var chip in _chips)
                chip.Active = chip.Level == _slider.Value;
        }

        /// <summary>Called by the window when the page is shown, to resync with the engine.</summary>
        public void Refresh(int level)
        {
            _slider.Value = Math.Clamp(level, 0, VibranceEngine.Max);
            UpdateActiveChip();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer?.Dispose();
            base.Dispose(disposing);
        }
    }
}
