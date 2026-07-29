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

        // Sliders fire ValueChanged on every mouse-move during a drag - saving on each one
        // would hammer disk I/O and risk torn writes. Debounce so one save happens ~500ms
        // after the user stops moving a slider, not on every intermediate value.
        private const int SaveDebounceMs = 500;
        private readonly DebouncedAction _saveDebounce;

        private readonly FlatSlider _slider;     // saturation (software, 0-200)
        private readonly FlatSlider _vibrance;   // driver Digital Vibrance (0-100)
        private readonly FlatSlider _brightness;
        private readonly FlatSlider _gamma;
        private readonly HotkeyPicker _hotkeyPicker;
        private readonly HotkeyPicker _mainHotkeyPicker;
        private readonly ToggleSwitch _eyeCare;
        private readonly List<ChipButton> _chips = new();
        private readonly List<(int Vib, int Sat)> _presetValues = new();

        private int _cx, _colW, _numberY, _captionY, _scaleY, _presetCapY;
        private int _vibCapY, _hotkeyCapY, _brightCapY, _gammaCapY, _eyeY;
        private int _mainHotkeyCapY;

        /// <summary>Raised when the user picks a new quick-vibrance hotkey combo. The
        /// tray forwards to <see cref="TrayApplicationContext.ReRegisterHotkey"/> so the
        /// OS-level RegisterHotKey is swapped without a restart.</summary>
        public event Action<uint, uint>? HotkeyChanged;

        /// <summary>Raised when the user picks a new main-window hotkey combo (or disables
        /// the existing one). Argument is (modifier mask, virtual key, enabled). The tray
        /// forwards to TrayApplicationContext.ReRegisterMainHotkey so the OS-level
        /// RegisterHotKey is swapped without a restart.</summary>
        public event Action<uint, uint, bool>? MainHotkeyChanged;

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
                    _saveDebounce = new DebouncedAction(() => _store.Save(_settings), SaveDebounceMs);
                                Font = new Font(Theme.FontFamily, 9f);

                                // Layout must fit the entire page content (4 sliders + presets +
                                // eye-care + 2 hotkey pickers) into the default 628-px content-host
                                // height with no scroll. The previous layout assumed 700 px of height
                                // and used AutoScroll as a crutch, which interacted poorly with the
                                // content host's own AutoScroll (added in v0.9.0) - the two scroll
                                // surfaces fought each other and the bottom hotkey picker clipped.
                                // With AutoScroll gone here, this page is now a single fixed-size
                                // block that the content host scrolls if the window is too small.

                    // Saturation is the headline control: it's the one that goes past the driver
                    // ceiling, and it works without an NVIDIA GPU.
                    _slider = new FlatSlider
                    {
                        Minimum = 0,
                        Maximum = VibranceEngine.MaxSaturation,
                        Notch = 100,
                        Value = _engine.Saturation
                    };
                    _slider.ValueChanged += (s, e) =>
                    {
                        _engine.Saturation = _slider.Value;
                        _settings.SaturationPercent = _slider.Value;
                        UpdateActiveChip();
                        Invalidate();
                        _saveDebounce.Trigger();
                    };
                    // Tell the engine when the user is actively dragging so it can suppress
                    // overlay writes during the drag (the chip tracks the cursor 1:1; the
                    // screen catches up on MouseUp via EndDrag's single flush).
                    _slider.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _engine.BeginDrag(); };
                    _slider.MouseUp += (s, e) => _engine.EndDrag();
                    Controls.Add(_slider);

            _vibrance = new FlatSlider
            {
                Minimum = 0,
                Maximum = VibranceEngine.MaxVibrance,
                // The notch marks where the driver runs out and software takes over.
                Notch = VibranceEngine.DriverVibranceCeiling,
                Value = _engine.Vibrance
            };
            _vibrance.ValueChanged += (s, e) =>
            {
                _engine.Vibrance = _vibrance.Value;
                _settings.VibrancePercent = _vibrance.Value;
                UpdateActiveChip();
                Invalidate();
                _saveDebounce.Trigger();
            };
            _vibrance.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _engine.BeginDrag(); };
            _vibrance.MouseUp += (s, e) => _engine.EndDrag();
            Controls.Add(_vibrance);

            // Hotkey picker: seeded from settings, raised through the page so the tray can
            // swap the live RegisterHotKey without the page having to know about Win32.
            _hotkeyPicker = new HotkeyPicker
            {
                ModifierMask = _settings.HotkeyModifierMask,
                VirtualKey = _settings.HotkeyVirtualKey
            };
            _hotkeyPicker.HotkeyChanged += (mask, vk) =>
            {
                _settings.HotkeyModifierMask = mask;
                _settings.HotkeyVirtualKey = vk;
                _store.Save(_settings);
                HotkeyChanged?.Invoke(mask, vk);
            };
            Controls.Add(_hotkeyPicker);

            // Second picker for the main-window hotkey (separate from the popup).
            // Default-disabled so existing users do not get a surprise binding - the
            // moment the user picks a combo here, the flag flips on and the tray
            // starts registering it.
            _mainHotkeyPicker = new HotkeyPicker
            {
                ModifierMask = _settings.MainHotkeyModifierMask,
                VirtualKey = _settings.MainHotkeyEnabled ? _settings.MainHotkeyVirtualKey : 0,
            };
            _mainHotkeyPicker.HotkeyChanged += (mask, vk) =>
            {
                _settings.MainHotkeyModifierMask = mask;
                _settings.MainHotkeyVirtualKey = vk;
                _settings.MainHotkeyEnabled = true;
                _store.Save(_settings);
                MainHotkeyChanged?.Invoke(mask, vk, true);
            };
            Controls.Add(_mainHotkeyPicker);

            // Presets set BOTH controls. These pairs reproduce exactly what the old
            // combined 0-200 slider did at 50/100/150/200, so nothing shifts on upgrade.
            (string name, int vib, int sat)[] presets =
            {
                ("Natural", 50, 100), ("Standard", 100, 100),
                ("Vivid", 100, 150), ("Max", 100, 200)
            };
            foreach (var (name, vib, sat) in presets)
            {
                var chip = new ChipButton { Text = name, Level = sat, Font = new Font(Theme.FontFamily, 9f) };
                int v = vib, t = sat;
                chip.Click += (s, e) => { _vibrance.Value = v; _slider.Value = t; };
                _chips.Add(chip);
                _presetValues.Add((vib, sat));
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
                _saveDebounce.Trigger();
            };
            _brightness.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _engine.BeginDrag(); };
            _brightness.MouseUp += (s, e) => _engine.EndDrag();
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
                _saveDebounce.Trigger();
            };
            _gamma.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _engine.BeginDrag(); };
            _gamma.MouseUp += (s, e) => _engine.EndDrag();
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
                    // Dense packing - the whole page (100% + caption + saturation slider +
                    // 0/100/200 scale + presets chips + 3 more sliders + eye care + 2 hotkey
                    // pickers) must fit in 628 px (the default content-host height). All
                    // spacing numbers below were tuned to fit exactly that - don't add any
                    // new vertical gaps without removing one elsewhere first.
                    int top = 8;

                                _numberY = top;
                                // The "100%" headline is drawn in a 84-px-tall box (NumberFont 46f,
                                // centered). The SATURATION caption must sit BELOW that box or the
                                // two overlap. top + 92 leaves a 0 px gap; top + 96 adds 4 px of
                                // breathing room that matches the visual rhythm the original
                                // 90-px gap (against the previous top of 128) used to provide.
                                _captionY = top + 96;
                                int sliderY = top + 120;
                                _slider.SetBounds(_cx, sliderY, _colW, 32);
                                _scaleY = sliderY + 34;
                                _presetCapY = sliderY + 60;         // 60 (was 70) - tighter under-slider gap

                                int chipW = (_colW - 3 * 10) / 4;
                                int chipY = sliderY + 84;           // 84 (was 92) - tighter
                                for (int i = 0; i < _chips.Count; i++)
                                    _chips[i].SetBounds(_cx + i * (chipW + 10), chipY, chipW, 36);

                                _vibCapY = chipY + 50;              // 50 (was 56)
                                _vibrance.SetBounds(_cx, chipY + 70, _colW, 32);   // 70 (was 78)

                                // Brightness, Gamma, then Eye care, then the two hotkey pickers at the
                                // very bottom. Each band is 36 px tall (caption 16 + 4 gap + slider 32
                                // overlaps caption by 16 - net 36).
                                _brightCapY = chipY + 110;           // 110 (was 122)
                                _brightness.SetBounds(_cx, chipY + 130, _colW, 32);  // 130 (was 144)

                                _gammaCapY = chipY + 162;            // 162 (was 188) - pulls gamma row up so eye-care fits
                                _gamma.SetBounds(_cx, chipY + 182, _colW, 32);    // 182 (was 210)

                                _eyeY = chipY + 224;                // 224 (was 262) - tightens eye-care row
                                _eyeCare.SetBounds(_cx + _colW - 44, _eyeY - 2, 44, 22);

                                // Hotkey pickers - placed directly under eye-care with minimum gap.
                                // Each picker is 40 px tall (picker Height), caption sits 4 px above it.
                                int hotkeyW = 300;
                                hotkeyW = Math.Max(HotkeyPicker.PickerMinimumSize.Width,
                                    Math.Min(hotkeyW, _colW));
                                int hotkeyH = HotkeyPicker.PickerDefaultSize.Height;
                                int hotkeyX = _cx + (_colW - hotkeyW) / 2;

                                _hotkeyCapY = _eyeY + 28;            // caption 28 px under eye-care row baseline
                                _hotkeyPicker.SetBounds(hotkeyX, _hotkeyCapY + 16, hotkeyW, hotkeyH);

                                _mainHotkeyCapY = _hotkeyCapY + 60;  // 60 (was 70) - tighter between two pickers
                                _mainHotkeyPicker.SetBounds(hotkeyX, _mainHotkeyCapY + 16, hotkeyW, hotkeyH);

                    Invalidate();
                }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); // particle-field background
            var g = e.Graphics;

            // Frosted-glass panel behind the content - the plexus shows through it, dimmed.
            var panel = new RectangleF(_cx - 36, _numberY - 28, _colW + 72, 800);
            Glass.PaintPanel(g, panel, 24, fillAlpha: 165);

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            TextRenderer.DrawText(g, $"{_slider.Value}%", NumberFont,
                new Rectangle(_cx, _numberY, _colW, 84), Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, UiHelpers.Spaced("SATURATION"), CaptionFont,
                new Rectangle(_cx, _captionY, _colW, 16), Theme.TextDim, TextFormatFlags.HorizontalCenter);

            TextRenderer.DrawText(g, "0", SmallFont, new Rectangle(_cx, _scaleY, 40, 14), Theme.TextDim, TextFormatFlags.Left);
            TextRenderer.DrawText(g, "100", SmallFont, new Rectangle(_cx, _scaleY, _colW, 14), Theme.TextDim, TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, "200", SmallFont, new Rectangle(_cx + _colW - 40, _scaleY, 40, 14), Theme.TextDim, TextFormatFlags.Right);

            TextRenderer.DrawText(g, UiHelpers.Spaced("PRESETS"), CaptionFont,
                new Rectangle(_cx, _presetCapY, 200, 16), Theme.TextDim, TextFormatFlags.Left);

            // ---- Vibrance (driver) ----
            TextRenderer.DrawText(g, UiHelpers.Spaced("VIBRANCE"), CaptionFont,
                new Rectangle(_cx, _vibCapY, 240, 16), Theme.TextDim, TextFormatFlags.Left);
            TextRenderer.DrawText(g,
                _engine.DriverAvailable ? $"{_vibrance.Value}%" : "no NVIDIA GPU",
                SmallFont, new Rectangle(_cx + _colW - 110, _vibCapY, 110, 16),
                Theme.TextDim, TextFormatFlags.Right);

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

            // ---- Quick hotkey vibrance (last element on the page) ----
            // Lowercase, regular weight - not the all-caps style of the slider
            // captions, since this is a small one-shot config row rather than a
            // primary control section.
            TextRenderer.DrawText(g, "Quick hotkey vibrance", SmallFont,
                new Rectangle(_cx, _hotkeyCapY, 200, 16), Theme.TextDim, TextFormatFlags.Left);

            // ---- Main window hotkey (one row below the popup hotkey) ----
            TextRenderer.DrawText(g, "Open main window", SmallFont,
                new Rectangle(_cx, _mainHotkeyCapY, 200, 16), Theme.TextDim, TextFormatFlags.Left);
        }

        private void UpdateActiveChip()
        {
            // A preset lights up only when BOTH controls still match it - Natural and
            // Standard share saturation 100 and differ only in vibrance.
            for (int i = 0; i < _chips.Count; i++)
            {
                var (vib, sat) = _presetValues[i];
                _chips[i].Active = _vibrance.Value == vib && _slider.Value == sat;
            }
        }

        public new void Refresh()
        {
            _slider.Value = _engine.Saturation;
            _vibrance.Value = _engine.Vibrance;
            _brightness.Value = _engine.Brightness;
            _gamma.Value = _engine.Gamma;
            _eyeCare.Checked = _engine.EyeCare;
            _hotkeyPicker.ModifierMask = _settings.HotkeyModifierMask;
            _hotkeyPicker.VirtualKey = _settings.HotkeyVirtualKey;
            _mainHotkeyPicker.ModifierMask = _settings.MainHotkeyModifierMask;
            _mainHotkeyPicker.VirtualKey = _settings.MainHotkeyEnabled ? _settings.MainHotkeyVirtualKey : 0;
            UpdateActiveChip();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _saveDebounce.Dispose();
            base.Dispose(disposing);
        }
    }
}
