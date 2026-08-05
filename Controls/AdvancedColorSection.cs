using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using VibranceHud.Capabilities;
using VibranceHud.Design;

namespace VibranceHud
{
    /// <summary>
    /// The advanced colour controls: tone shaping and split toning.
    ///
    /// Deliberately NOT a Control. Nesting a transparent container inside the transparent
    /// card is what caused the ghosting documented in HANDOFF.md, so this follows the same
    /// shape as <see cref="SliderRow"/> - a plain class that creates its children directly on
    /// the card, one level deep, and positions them from <see cref="Place"/>.
    ///
    /// Collapsed by default. The page's job is saturation and vibrance; everything here is
    /// for somebody who has already got those right and wants to go further.
    ///
    /// Every control resolves to the display gamma ramp, which is why this section asks
    /// <see cref="Machine"/> whether that ramp actually works before offering them. On a PC
    /// where Windows refuses or clamps the ramp these sliders would otherwise move, update
    /// their numbers, and change nothing at all.
    /// </summary>
    public sealed class AdvancedColorSection
    {
        private const int RowGap = 4;

        private readonly Label _title;
        private readonly GlassButton _toggle;
        private readonly Label _limitation;

        private readonly Label _toneLabel, _balanceLabel;
        private readonly GlassButton _resetTone, _resetBalance;

        private readonly SliderRow _highlights, _shadows, _whites, _blacks, _fade;
        private readonly SliderRow _shadowTint, _midTint, _highTint;

        private readonly List<SliderRow> _all = new();
        private bool _expanded;
        private bool _loading;

        /// <summary>Raised when any value changes. Once per user change, not once per
        /// slider - loading a whole grade must not fire nine times.</summary>
        public event EventHandler? ToneChanged;

        /// <summary>Raised when the section is opened or closed, so the page can re-lay out.</summary>
        public event EventHandler? ExpandedChanged;

        public AdvancedColorSection(Control parent, Font sectionFont, Font bodyFont)
        {
            _title = Caption(parent, "ADVANCED", sectionFont);

            _toggle = new GlassButton { Text = "Show", Kind = GlassButtonKind.Ghost };
            _toggle.Click += (_, _) => Expanded = !Expanded;
            parent.Controls.Add(_toggle);

            _limitation = new Label
            {
                Font = bodyFont,
                ForeColor = Theme.Accent,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft,
                Visible = false,
            };
            parent.Controls.Add(_limitation);

            _toneLabel = Caption(parent, "TONE", sectionFont);
            _resetTone = ResetButton(parent, ResetTone);

            _highlights = Row(parent, "Highlights", -100, 100, v => Signed(v));
            _shadows = Row(parent, "Shadows", -100, 100, v => Signed(v));
            _whites = Row(parent, "Whites", -100, 100, v => Signed(v));
            _blacks = Row(parent, "Blacks", -100, 100, v => Signed(v));
            _fade = Row(parent, "Fade", 0, 100, v => $"{v}%", SliderPalette.Luminance);

            _balanceLabel = Caption(parent, "COLOUR BALANCE", sectionFont);
            _resetBalance = ResetButton(parent, ResetBalance);

            _shadowTint = Row(parent, "Shadows", -100, 100, Warmth, SliderPalette.Temperature);
            _midTint = Row(parent, "Midtones", -100, 100, Warmth, SliderPalette.Temperature);
            _highTint = Row(parent, "Highlights", -100, 100, Warmth, SliderPalette.Temperature);

            ApplyCapabilities();
            SetChildVisibility();
        }

        // ---- state -----------------------------------------------------------------------

        public bool Expanded
        {
            get => _expanded;
            set
            {
                if (_expanded == value) return;
                _expanded = value;
                _toggle.Text = value ? "Hide" : "Show";
                SetChildVisibility();
                ExpandedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Whether this PC can actually apply any of it.</summary>
        public bool Usable => Machine.Current.ToneControlsWork;

        public ToneSettings Tone
        {
            get => new(
                Gamma: 100,   // owned by the FINE TUNE slider; the page merges it back in
                Highlights: _highlights.Slider.Value,
                Shadows: _shadows.Slider.Value,
                Whites: _whites.Slider.Value,
                Blacks: _blacks.Slider.Value,
                Fade: _fade.Slider.Value,
                ShadowTint: _shadowTint.Slider.Value,
                MidtoneTint: _midTint.Slider.Value,
                HighlightTint: _highTint.Slider.Value);

            set
            {
                // One flag around the whole set: without it, loading a saved grade fires
                // ToneChanged nine times and saves nine times.
                _loading = true;
                try
                {
                    _highlights.Slider.Value = value.Highlights;
                    _shadows.Slider.Value = value.Shadows;
                    _whites.Slider.Value = value.Whites;
                    _blacks.Slider.Value = value.Blacks;
                    _fade.Slider.Value = value.Fade;
                    _shadowTint.Slider.Value = value.ShadowTint;
                    _midTint.Slider.Value = value.MidtoneTint;
                    _highTint.Slider.Value = value.HighlightTint;
                }
                finally { _loading = false; }

                foreach (var row in _all) row.SyncValueText();
                ToneChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ResetTone() => ApplyGroup(new[] { _highlights, _shadows, _whites, _blacks, _fade });

        public void ResetBalance() => ApplyGroup(new[] { _shadowTint, _midTint, _highTint });

        private void ApplyGroup(IEnumerable<SliderRow> rows)
        {
            _loading = true;
            try { foreach (var r in rows) r.Slider.Value = 0; }
            finally { _loading = false; }

            foreach (var r in rows) r.SyncValueText();
            ToneChanged?.Invoke(this, EventArgs.Empty);
        }

        // ---- capability ------------------------------------------------------------------

        /// <summary>
        /// Reflect what the machine can actually do.
        ///
        /// A refused gamma ramp means every control here is inert, so they are disabled and
        /// the reason is stated. A clamped ramp still does something, so they stay live and
        /// the note only warns that the effect is weaker than the numbers suggest.
        /// </summary>
        private void ApplyCapabilities()
        {
            var caps = Machine.Current;
            string limitation = caps.ToneLimitation;

            _limitation.Text = limitation;
            _limitation.ForeColor = caps.ToneControlsWork ? Theme.TextDim : Theme.Accent;

            if (!caps.ToneControlsWork)
            {
                foreach (var row in _all) row.Slider.Enabled = false;
                _resetTone.Enabled = false;
                _resetBalance.Enabled = false;
            }
        }

        // ---- layout ----------------------------------------------------------------------

        /// <summary>Height this section needs at its current state.</summary>
        public int PreferredHeight
        {
            get
            {
                int header = Tokens.Scale(22) + Tokens.Scale(RowGap);
                if (!_expanded) return header + LimitationHeight();

                int gap = Tokens.Scale(Tokens.XL);
                int label = Tokens.Scale(22) + Tokens.Scale(6);

                // Tone: three rows of two (five controls). Balance: two rows of two (three).
                int tone = label + 3 * SliderRow.RowHeight;
                int balance = label + 2 * SliderRow.RowHeight;

                return header + LimitationHeight() + tone + gap + balance;
            }
        }

        private int LimitationHeight() =>
            string.IsNullOrEmpty(_limitation.Text) ? 0 : Tokens.Scale(34);

        public void Place(int x, int y, int width, int columnWidth, int columnGap)
        {
            int labelH = Tokens.Scale(22);
            int toggleW = Tokens.Scale(72);
            int toggleH = Tokens.Scale(26);
            int rightX = x + columnWidth + columnGap;

            _title.SetBounds(x, y, width - toggleW - Tokens.Scale(Tokens.S), labelH);
            _toggle.SetBounds(x + width - toggleW, y - Tokens.Scale(4), toggleW, toggleH);
            y += labelH + Tokens.Scale(RowGap);

            if (!string.IsNullOrEmpty(_limitation.Text))
            {
                _limitation.SetBounds(x, y, width, Tokens.Scale(32));
                y += Tokens.Scale(34);
            }

            if (!_expanded) return;

            // ---- tone ----
            int resetW = Tokens.Scale(72), resetH = Tokens.Scale(26);
            _toneLabel.SetBounds(x, y, width - resetW - Tokens.Scale(Tokens.S), labelH);
            _resetTone.SetBounds(x + width - resetW, y - Tokens.Scale(4), resetW, resetH);
            y += labelH + Tokens.Scale(6);

            _highlights.Place(x, y, columnWidth);
            _shadows.Place(rightX, y, columnWidth);
            y += SliderRow.RowHeight;

            _whites.Place(x, y, columnWidth);
            _blacks.Place(rightX, y, columnWidth);
            y += SliderRow.RowHeight;

            _fade.Place(x, y, columnWidth);
            y += SliderRow.RowHeight + Tokens.Scale(Tokens.XL);

            // ---- colour balance ----
            _balanceLabel.SetBounds(x, y, width - resetW - Tokens.Scale(Tokens.S), labelH);
            _resetBalance.SetBounds(x + width - resetW, y - Tokens.Scale(4), resetW, resetH);
            y += labelH + Tokens.Scale(6);

            _shadowTint.Place(x, y, columnWidth);
            _midTint.Place(rightX, y, columnWidth);
            y += SliderRow.RowHeight;

            _highTint.Place(x, y, columnWidth);
        }

        private void SetChildVisibility()
        {
            foreach (var row in _all) row.Visible = _expanded;

            _toneLabel.Visible = _expanded;
            _balanceLabel.Visible = _expanded;
            _resetTone.Visible = _expanded && Usable;
            _resetBalance.Visible = _expanded && Usable;

            // The limitation is worth seeing whether or not the section is open - it is the
            // reason someone would go looking in here in the first place.
            _limitation.Visible = !string.IsNullOrEmpty(_limitation.Text);
        }

        // ---- construction helpers ---------------------------------------------------------

        private SliderRow Row(Control parent, string caption, int min, int max,
            Func<int, string> format, SliderPalette? palette = null)
        {
            var row = new SliderRow(parent, caption, min, max, notch: 0, value: 0,
                large: false, palette: palette, format: format);

            row.Slider.ValueChanged += (_, _) =>
            {
                if (_loading) return;
                ToneChanged?.Invoke(this, EventArgs.Empty);
            };

            _all.Add(row);
            return row;
        }

        private static Label Caption(Control parent, string text, Font font)
        {
            var l = new Label
            {
                Text = UiHelpers.Spaced(text),
                Font = font,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            parent.Controls.Add(l);
            return l;
        }

        private static GlassButton ResetButton(Control parent, Action onClick)
        {
            var b = new GlassButton { Text = "Reset", Kind = GlassButtonKind.Ghost };
            b.Click += (_, _) => onClick();
            parent.Controls.Add(b);
            return b;
        }

        /// <summary>A bare signed number reads as noise; the sign is the whole meaning.</summary>
        private static string Signed(int v) => v == 0 ? "0" : (v > 0 ? $"+{v}" : v.ToString());

        /// <summary>Same reasoning as the Temperature slider on the main page: the direction
        /// is what the user is actually choosing.</summary>
        private static string Warmth(int v) => v switch
        {
            0 => "Neutral",
            < 0 => $"Cool {-v}",
            _ => $"Warm {v}",
        };
    }
}
