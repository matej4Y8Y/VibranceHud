using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VibranceHud.Pages;

namespace VibranceHud
{
    /// <summary>
    /// The compact quick-adjust popup opened by the global Ctrl+Alt+V hotkey - a small,
    /// always-on-top window with the four visual sliders. Same category as a Discord
    /// overlay: no code injection, no game-process access, just a normal top-level window.
    ///
    /// Visually it's a miniature of the main Vibrance page: its own particle field under a
    /// rounded matte-glass card (same <see cref="Glass.PaintPanel"/> surface, same rounded
    /// region trick as <see cref="SplashForm"/>), with the captions drawn by
    /// <c>TextRenderer</c> in OnPaint rather than as Labels - transparent Labels would
    /// freeze the patch of plexus behind them, so the page paints its own text.
    ///
    /// Slider drags write straight to the shared <see cref="IVibranceEngine"/>, exactly like
    /// the full Vibrance page, so the effect is visible immediately. Deliberately depends on
    /// nothing from the auto-apply path (<c>ProfileApplyEngine</c>, <c>GameProfileStore</c>,
    /// <c>ProfileEngineCoordinator</c>) - a quick manual tweak here must never register as,
    /// or get silently clobbered by, a game's auto-applied profile.
    /// </summary>
    public sealed class VibrancePopup : Form
    {
        private const int CornerRadius = 16;
        private const int RowStride = 56;   // caption + slider per row
        private const int Pad = 24;   // named Pad, not Margin: Form.Margin already exists

        private readonly IVibranceEngine _engine;
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;

        // Its own field (the popup is a standalone top-level window, so it can't share
        // MainWindow's). Fewer nodes than the main window - the surface is much smaller.
        private readonly ParticleField _field = new(28);
        private readonly System.Windows.Forms.Timer _timer;
        private DateTime _last = DateTime.UtcNow;

        // OnPaint runs ~30x/sec, so fonts are built once - never inside the paint path.
        private static readonly Font TitleFont = new(Theme.FontFamily, 12f, FontStyle.Bold);
        private static readonly Font CaptionFont = new(Theme.FontFamily, 8f, FontStyle.Bold);
        private static readonly Font ValueFont = new(Theme.FontFamily, 8.5f);

        internal FlatSlider VibranceSlider { get; }
        internal FlatSlider SaturationSlider { get; }
        internal FlatSlider BrightnessSlider { get; }
        internal FlatSlider GammaSlider { get; }

        // Caption text + the y of each row, so OnPaint can draw the labels the constructor
        // laid out without re-deriving the layout.
        private readonly (string Caption, int Y, Func<string> Value)[] _rows;

        public VibrancePopup(IVibranceEngine engine, AppSettings settings, SettingsStore store)
        {
            _engine = engine;
            _settings = settings;
            _store = store;

            FormBorderStyle = FormBorderStyle.None;
                        StartPosition = FormStartPosition.CenterScreen;
                        ShowInTaskbar = false;
                        TopMost = true;
                        ClientSize = new Size(360, 364);
                        DoubleBuffered = true;

                        // Reduce paint flicker during slider drags. The popup repaints on every
                        // ValueChanged (the value readout in OnPaint) and continuously from the
                        // animation tick, so without AllPaintingInWmPaint + OptimizedDoubleBuffer
                        // each repaint flashes. ResizeRedraw keeps the rounded region in sync on
                        // DPI / maximize changes; UserPaint is what makes OnPaint run at all when
                        // there are no child-controls invalidating the form.
                        SetStyle(
                            ControlStyles.OptimizedDoubleBuffer |
                            ControlStyles.AllPaintingInWmPaint |
                            ControlStyles.UserPaint |
                            ControlStyles.ResizeRedraw,
                            true);

                        // Drive per-message-tick repaints: every message that goes through the
                                                // pump (mouse move during a drag, timer tick, focus change) leaves the
                                                // queue idle, so this fires after each one and gets the value readout
                                                // drawn in OnPaint to the screen within the same frame as the slider
                                                // chip itself. Without this, repaints only happened on the 33ms particle
                                                // timer, which made the chip feel a frame or two behind the cursor.
                                                Shown += (s, e) => Application.Idle += OnIdleRepaint;
                                                FormClosed += (s, e) => Application.Idle -= OnIdleRepaint;

                        Icon = AppIcon.Value;

            int y = 56;
            int vibY = y;
            VibranceSlider = AddSliderRow(0, VibranceEngine.MaxVibrance, _engine.Vibrance, ref y,
                v => _engine.Vibrance = v, VibranceEngine.DriverVibranceCeiling);
            VibranceSlider.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _engine.BeginDrag(); };
            VibranceSlider.MouseUp += (s, e) => _engine.EndDrag();
            int satY = y;
            SaturationSlider = AddSliderRow(0, VibranceEngine.MaxSaturation, _engine.Saturation, ref y,
                v => _engine.Saturation = v, 100);
            SaturationSlider.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _engine.BeginDrag(); };
            SaturationSlider.MouseUp += (s, e) => _engine.EndDrag();
            int brightY = y;
            BrightnessSlider = AddSliderRow(VibranceEngine.MinBrightness, VibranceEngine.MaxBrightness, _engine.Brightness, ref y,
                v => _engine.Brightness = v, 100);
            BrightnessSlider.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _engine.BeginDrag(); };
            BrightnessSlider.MouseUp += (s, e) => _engine.EndDrag();
            int gammaY = y;
            GammaSlider = AddSliderRow(VibranceEngine.MinGamma, VibranceEngine.MaxGamma, _engine.Gamma, ref y,
                v => _engine.Gamma = v, 100);
            GammaSlider.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _engine.BeginDrag(); };
            GammaSlider.MouseUp += (s, e) => _engine.EndDrag();

            // Value readouts mirror the main page's formatting: percentages everywhere,
            // gamma as a 0.00 multiplier.
            _rows = new (string, int, Func<string>)[]
            {
                ("VIBRANCE",   vibY,    () => $"{VibranceSlider.Value}%"),
                ("SATURATION", satY,    () => $"{SaturationSlider.Value}%"),
                ("BRIGHTNESS", brightY, () => $"{BrightnessSlider.Value}%"),
                ("GAMMA",      gammaY,  () => $"{GammaSlider.Value / 100f:0.00}"),
            };

            var saveBtn = SettingsPage.FlatButton("Save", Pad, y + 12, 152);
            saveBtn.BackColor = Theme.AccentDim;   // primary action gets the accent pop
            saveBtn.Click += (s, e) => Save();
            Controls.Add(saveBtn);

            var closeBtn = SettingsPage.FlatButton("Close", Pad + 160, y + 12, 152);
            closeBtn.Click += (s, e) => Close();
            Controls.Add(closeBtn);

            _field.Resize(ClientSize.Width, ClientSize.Height);
            ApplyRoundedRegion();

            _timer = new System.Windows.Forms.Timer { Interval = 33 };
            _timer.Tick += OnAnimationTick;
            _timer.Start();
        }

        /// <summary>Cuts the window to a rounded rectangle so the glass card's corners are
        /// real transparency, not dark pixels sitting outside the rim.</summary>
        private void ApplyRoundedRegion()
        {
            using var path = Glass.RoundedPath(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), CornerRadius);
            Region = new Region(path);
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            // Only burn frames while the popup is actually on screen.
            if (!Visible || WindowState == FormWindowState.Minimized) { _last = DateTime.UtcNow; return; }

            var now = DateTime.UtcNow;
            _field.Update(Math.Min((now - _last).TotalSeconds, 0.1));
            _last = now;

            // invalidateChildren: true so the transparent sliders re-sample the moving
            // plexus instead of freezing the patch behind them.
            Invalidate(true);
        }

        /// <summary>Fired once per message-pump idle, so a slider drag or focus change
        /// repaints the value readout in the same frame the chip moves, without waiting
        /// for the 33ms particle timer to fire.</summary>
        private void OnIdleRepaint(object? sender, EventArgs e) => Invalidate();

        private FlatSlider AddSliderRow(int min, int max, int value, ref int y, Action<int> onChange, int? notch = null)
        {
            var slider = new FlatSlider
            {
                Minimum = min,
                Maximum = max,
                Value = value,
                Notch = notch,
                Location = new Point(20, y + 20),
                Width = 320,
            };
            slider.ValueChanged += (s, e) =>
            {
                onChange(slider.Value);
                Invalidate();   // repaint the value readout drawn in OnPaint
            };
            Controls.Add(slider);

            y += RowStride;
            return slider;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Backdrop: theme base, the user's background image (if set), then the plexus -
            // the same stack GlowPage paints behind every page in the main window.
            using (var back = new SolidBrush(Theme.Background))
                g.FillRectangle(back, ClientRectangle);
            Theming.AppBackground.Paint(g, 0, 0);
            _field.Paint(g, 0, 0);

            // The whole popup is the frosted card, matching the main page's glass surface.
            Glass.PaintPanel(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), CornerRadius, fillAlpha: 170);

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            TextRenderer.DrawText(g, "Quick vibrance", TitleFont,
                new Rectangle(Pad, 14, Width - 2 * Pad, 24), Theme.Text, TextFormatFlags.Left);

            // Thin accent rule under the title - the purple/pink cue from the main page.
            using (var rule = new SolidBrush(Color.FromArgb(190, Theme.Accent)))
                g.FillRectangle(rule, Pad, 40, 44, 2);

            foreach (var (caption, rowY, value) in _rows)
            {
                TextRenderer.DrawText(g, UiHelpers.Spaced(caption), CaptionFont,
                    new Rectangle(Pad, rowY, 200, 16), Theme.TextDim, TextFormatFlags.Left);
                TextRenderer.DrawText(g, value(), ValueFont,
                    new Rectangle(Width - Pad - 60, rowY, 60, 16), Theme.TextDim, TextFormatFlags.Right);
            }

            base.OnPaint(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Everything is painted in OnPaint; suppressing the default background erase
            // keeps the animated field flicker-free.
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

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer?.Dispose();
            base.Dispose(disposing);
        }
    }
}
