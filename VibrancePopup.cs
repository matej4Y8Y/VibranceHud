using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VibranceHud.Pages;

namespace VibranceHud
{
    /// <summary>
    /// The compact quick-adjust popup opened by the global hotkey: every display control the
    /// main window has, in a window you can read in one glance.
    ///
    /// Same category as a Discord overlay - no code injection, no game-process access, just a
    /// normal top-level window. It is a miniature of the Display page: its own particle field
    /// under a rounded matte-glass card, with the captions drawn by TextRenderer in OnPaint
    /// rather than as Labels, because a transparent Label here freezes the patch of plexus
    /// behind it. That is safe in this window specifically - it never scrolls, so painted text
    /// and positioned controls share one coordinate space.
    ///
    /// Presets are deliberately the plain text pills rather than the Display page's
    /// photo-backed cards. This window exists to be finished with in three seconds; a
    /// thumbnail strip would make it something you browse.
    ///
    /// Slider drags write straight to the shared engine, so the effect is visible immediately,
    /// and autosave (250ms debounce) plus <see cref="AppSettings.ManualOverrideActive"/> mean
    /// a quick tweak survives a restart and is not clobbered by a game profile on next launch.
    /// </summary>
    public sealed class VibrancePopup : Form
    {
        private const int CornerRadius = 16;
        private const int Pad = 22;
        private const int ColGap = 16;
        private const int AutosaveDebounceMs = 250;

        private readonly IVibranceEngine _engine;
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly DebouncedAction _autosaveDebounce;

        // Its own field - the popup is a standalone top-level window, so it can't share
        // MainWindow's. Fewer nodes because the surface is much smaller.
        private readonly ParticleField _field = new(28);
        private readonly System.Windows.Forms.Timer _timer;
        private DateTime _last = DateTime.UtcNow;
        private readonly bool _built;

        private static readonly Font TitleFont = new(Theme.FontFamily, 12f, FontStyle.Bold);
        private static readonly Font CaptionFont = new(Theme.FontFamily, 8f, FontStyle.Bold);
        private static readonly Font ValueFont = new(Theme.FontFamily, 9f, FontStyle.Bold);

        internal TwoColorSlider VibranceSlider { get; }
        internal TwoColorSlider SaturationSlider { get; }
        internal TwoColorSlider BrightnessSlider { get; }
        internal TwoColorSlider GammaSlider { get; }
        internal TwoColorSlider ContrastSlider { get; }
        internal TwoColorSlider TemperatureSlider { get; }

        private readonly List<ChipButton> _presetChips = new();
        private readonly List<DisplayPreset> _presets = new();
        private bool _applyingPreset;

        /// <summary>Caption, its column, and how to render the current value - so OnPaint
        /// never re-derives the layout the constructor already decided.</summary>
        private readonly List<(string Caption, Rectangle Row, Func<string> Value)> _rows = new();

        public VibrancePopup(IVibranceEngine engine, AppSettings settings, SettingsStore store)
        {
            _engine = engine;
            _settings = settings;
            _store = store;
            _autosaveDebounce = new DebouncedAction(Save, AutosaveDebounceMs);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(420, 404);
            DoubleBuffered = true;
            Icon = AppIcon.Value;

            SetStyle(ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw, true);

            // Repaint once per message-pump idle so the readout drawn in OnPaint lands in the
            // same frame as the thumb, instead of waiting for the 33ms particle tick.
            Shown += (s, e) => Application.Idle += OnIdleRepaint;
            FormClosed += (s, e) => Application.Idle -= OnIdleRepaint;

            int contentW = ClientSize.Width - 2 * Pad;
            int colW = (contentW - ColGap) / 2;
            int rightX = Pad + colW + ColGap;

            // ---- presets: plain pills, kept small on purpose ----
            int chipW = (contentW - 3 * 8) / 4;
            for (int i = 0; i < DisplayPresets.All.Count; i++)
            {
                var preset = DisplayPresets.All[i];
                var chip = new ChipButton
                {
                    Text = preset.Name,
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Bounds = new Rectangle(Pad + i * (chipW + 8), 58, chipW, 28),
                };
                chip.Click += (s, e) => ApplyPreset(preset);
                _presets.Add(preset);
                _presetChips.Add(chip);
                Controls.Add(chip);
            }

            // ---- the two headline controls, full width ----
            VibranceSlider = AddRow("VIBRANCE", Pad, 98, contentW,
                0, VibranceEngine.MaxVibrance, VibranceEngine.DriverVibranceCeiling,
                _engine.Vibrance, v => _engine.Vibrance = v,
                SliderPalette.Accent(), () => $"{VibranceSlider!.Value}%");

            SaturationSlider = AddRow("SATURATION", Pad, 158, contentW,
                0, VibranceEngine.MaxSaturation, 100,
                _engine.Saturation, v => _engine.Saturation = v,
                SliderPalette.Accent(), () => $"{SaturationSlider!.Value}%");

            // ---- fine tune, two columns ----
            BrightnessSlider = AddRow("BRIGHTNESS", Pad, 218, colW,
                VibranceEngine.MinBrightness, VibranceEngine.MaxBrightness, 100,
                _engine.Brightness, v => _engine.Brightness = v,
                SliderPalette.Luminance, () => $"{BrightnessSlider!.Value}%");

            GammaSlider = AddRow("GAMMA", rightX, 218, colW,
                VibranceEngine.MinGamma, VibranceEngine.MaxGamma, 100,
                _engine.Gamma, v => _engine.Gamma = v,
                SliderPalette.Luminance, () => $"{GammaSlider!.Value / 100f:0.00}");

            ContrastSlider = AddRow("CONTRAST", Pad, 278, colW,
                VibranceEngine.MinContrast, VibranceEngine.MaxContrast, 100,
                _engine.Contrast, v => _engine.Contrast = v,
                SliderPalette.Contrast, () => $"{ContrastSlider!.Value}%");

            TemperatureSlider = AddRow("TEMPERATURE", rightX, 278, colW,
                VibranceEngine.MinTemperature, VibranceEngine.MaxTemperature, 0,
                _engine.Temperature, v => _engine.Temperature = v,
                SliderPalette.Temperature, () => VibrancePage.TemperatureText(TemperatureSlider!.Value));

            var saveBtn = Pages.SettingsPage.PrimaryButton("Save", Pad, 348, colW, height: 34);
            saveBtn.Click += (s, e) => Save();
            Controls.Add(saveBtn);

            var closeBtn = Pages.SettingsPage.FlatButton("Close", rightX, 348, colW);
            closeBtn.Height = 34;
            closeBtn.Click += (s, e) => Close();
            Controls.Add(closeBtn);

            _field.Resize(ClientSize.Width, ClientSize.Height);
            ApplyRoundedRegion();
            // Borderless and always on top: without this it is stuck wherever it opened,
            // covering whatever happened to be in the middle of the screen.
            WindowDrag.Enable(this, this);
            RestorePosition();
            UpdateActiveChip();

            _timer = new System.Windows.Forms.Timer { Interval = 33 };
            _timer.Tick += OnAnimationTick;
            _timer.Start();

            _built = true;
        }

        /// <summary>Build one caption + value + slider row and register it for painting.</summary>
        private TwoColorSlider AddRow(string caption, int x, int y, int width,
            int min, int max, int? notch, int value, Action<int> apply,
            SliderPalette palette, Func<string> readout)
        {
            var slider = new TwoColorSlider
            {
                Minimum = min,
                Maximum = max,
                Notch = notch,
                Value = value,
                Palette = palette,
            };
            slider.SetTrackBounds(x, y + 20, width);

            slider.ValueChanged += (s, e) =>
            {
                apply(slider.Value);
                // Repaint just this row's readout, not the whole window and its particle field.
                Invalidate(new Rectangle(x, y, width, 18));
                if (!_applyingPreset) UpdateActiveChip();
                _autosaveDebounce.Trigger();
                // Tell the coordinator the user has overridden whatever a game profile applied,
                // so the next launch of that game doesn't clobber this.
                _settings.ManualOverrideActive = true;
            };
            slider.DragBegin += (s, e) => _engine.BeginDrag();
            slider.DragEnd += (s, e) => _engine.EndDrag();

            Controls.Add(slider);
            _rows.Add((caption, new Rectangle(x, y, width, 18), readout));
            return slider;
        }

        private void ApplyPreset(DisplayPreset preset)
        {
            // Tone only - saturation and vibrance are the user's own taste, same rule the
            // Display page follows.
            _applyingPreset = true;
            try
            {
                BrightnessSlider.Value = preset.Brightness;
                GammaSlider.Value = preset.Gamma;
                ContrastSlider.Value = preset.Contrast;
                TemperatureSlider.Value = preset.Temperature;
            }
            finally { _applyingPreset = false; }

            UpdateActiveChip();
            Save();
            Invalidate();
        }

        private void UpdateActiveChip()
        {
            for (int i = 0; i < _presetChips.Count; i++)
                _presetChips[i].Active = _presets[i].Matches(
                    BrightnessSlider.Value, GammaSlider.Value,
                    ContrastSlider.Value, TemperatureSlider.Value);
        }

        /// <summary>Cuts the window to a rounded rectangle so the card's corners are real
        /// transparency, not dark pixels sitting outside the rim.</summary>
        private void ApplyRoundedRegion()
        {
            using var path = Glass.RoundedPath(
                new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), CornerRadius);
            Region = new Region(path);
        }

        /// <summary>Reopen where the user last left it, unless that spot is no longer on any
        /// screen - a second monitor that has since been unplugged would otherwise strand it
        /// somewhere unreachable.</summary>
        private void RestorePosition()
        {
            if (_settings.PopupX == int.MinValue || _settings.PopupY == int.MinValue) return;

            var wanted = new Rectangle(_settings.PopupX, _settings.PopupY, Width, Height);
            bool reachable = false;
            foreach (var screen in Screen.AllScreens)
            {
                var hit = Rectangle.Intersect(screen.WorkingArea, wanted);
                if (hit.Width >= 120 && hit.Height >= 60) { reachable = true; break; }
            }
            if (!reachable) return;

            StartPosition = FormStartPosition.Manual;
            Location = wanted.Location;
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);
            // _built guards the placement moves that happen while the constructor runs - the
            // debounce flushes through Save(), which reads sliders that don't exist yet.
            if (!_built || WindowState != FormWindowState.Normal) return;
            _settings.PopupX = Left;
            _settings.PopupY = Top;
            _autosaveDebounce.Trigger();
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            if (!Visible || WindowState == FormWindowState.Minimized) { _last = DateTime.UtcNow; return; }

            var now = DateTime.UtcNow;
            _field.Update(Math.Min((now - _last).TotalSeconds, 0.1));
            _last = now;

            // invalidateChildren: true so the transparent sliders re-sample the moving plexus
            // instead of freezing the patch behind them.
            Invalidate(true);
        }

        private void OnIdleRepaint(object? sender, EventArgs e) => Invalidate();

        /// <summary>
        /// Click away and it goes. This is a hotkey popup - it is summoned over whatever the
        /// user is actually doing, so leaving it floating there once their attention has moved
        /// on makes it clutter rather than a shortcut. Hidden rather than closed so the next
        /// press of the hotkey is instant.
        /// </summary>
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Save();
            Hide();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var back = new SolidBrush(Theme.Background))
                g.FillRectangle(back, ClientRectangle);
            Theming.AppBackground.Paint(g, 0, 0);
            _field.Paint(g, 0, 0);

            Glass.PaintPanel(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1),
                CornerRadius, fillAlpha: 170);

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            TextRenderer.DrawText(g, "Quick vibrance", TitleFont,
                new Rectangle(Pad, 14, Width - 2 * Pad, 24), Theme.Text, TextFormatFlags.Left);

            using (var rule = new SolidBrush(Color.FromArgb(190, Theme.Accent)))
                g.FillRectangle(rule, Pad, 42, 44, 2);

            foreach (var (caption, row, value) in _rows)
            {
                TextRenderer.DrawText(g, UiHelpers.Spaced(caption), CaptionFont,
                    row, Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, value(), ValueFont,
                    row, Theme.Text, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }

            base.OnPaint(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Everything is painted in OnPaint; suppressing the default erase keeps the
            // animated field flicker-free.
        }

        /// <summary>Persist the current values. Drags already updated the live engine and
        /// triggered the debounce; this is the synchronous flush, so the last movement is
        /// never lost on shutdown.</summary>
        internal void Save()
        {
            _settings.VibrancePercent = VibranceSlider.Value;
            _settings.SaturationPercent = SaturationSlider.Value;
            _settings.BrightnessPercent = BrightnessSlider.Value;
            _settings.GammaPercent = GammaSlider.Value;
            _settings.ContrastPercent = ContrastSlider.Value;
            _settings.Temperature = TemperatureSlider.Value;
            _store.Save(_settings);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // The debounce may not have fired on a drag-then-immediately-close, and we want
            // it now rather than in 250ms on a form that is going away.
            Save();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Stop before Dispose - a queued WM_TIMER is still dispatched, and this popup
                // is created and destroyed repeatedly by the global hotkey.
                _timer?.Stop();
                _timer?.Dispose();
                _autosaveDebounce.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
