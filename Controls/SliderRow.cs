using System;
using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// One labelled slider: name on the left, live value hard against the right, track
    /// underneath.
    ///
    /// The three pieces are real child controls placed together by <see cref="Place"/>,
    /// deliberately NOT text painted in the page's OnPaint. Painted text lives in the page's
    /// unscrolled coordinates while child controls live in scrolled ones, so the moment the
    /// page could scroll the two drifted apart and every caption landed on top of its own
    /// track. Controls move together; there is no second coordinate space left to get wrong.
    ///
    /// Not a container control either: nesting a transparent panel inside the transparent
    /// card is what caused the ghosting documented in HANDOFF.md. These go straight onto the
    /// card, one level deep, which is the arrangement already proven everywhere else.
    /// </summary>
    public sealed class SliderRow
    {
        /// <summary>Caption row + gap + track + breathing room beneath.</summary>
        public static int RowHeight => Design.Tokens.Scale(58);

        /// <summary>The two headline controls get a taller row and larger type, because the
        /// page is about saturation and vibrance and the rest is trim.</summary>
        public static int LargeRowHeight => Design.Tokens.Scale(76);

        private static int CaptionH => Design.Tokens.Scale(18);

        /// <summary>30, not 26: the large readout is set in 15pt, which needs 28px. At 26 the
        /// percentage sign and any descender were being clipped by two pixels - small enough
        /// to look like font rendering rather than a layout bug.</summary>
        private static int LargeCaptionH => Design.Tokens.Scale(30);
        private static int CaptionGap => Design.Tokens.Scale(8);
        private static int ValueW => Design.Tokens.Scale(120);
        private static int LargeValueW => Design.Tokens.Scale(160);

        // Resolved per access rather than cached in static readonly fields.
        //
        // Point sizes are physical, so GDI+ turns them into pixels using the DPI in force
        // when the Font is built. These used to be `static readonly`, built once at type
        // init and never replaced - so after the window moved to a monitor with a different
        // scale factor, every slider caption kept rendering at the OLD pixel size while
        // everything around it resized. Design.Fonts rebuilds on DpiChanged, so going
        // through it is what keeps them in step.
        private static Font CaptionFont => Design.Fonts.Label;
        private static Font ValueFont => Design.Fonts.BodyBold;
        private static Font LargeCaptionFont => Design.Fonts.HeadingRegular;
        private static Font LargeValueFont => Design.Fonts.Title;

        private readonly Label _caption;
        private readonly Label _value;
        private readonly bool _large;
        private readonly Func<int, string>? _format;

        public TwoColorSlider Slider { get; }

        /// <param name="format">Turns a value into the text shown on the right. Held by the
        /// row so a moving slider only ever rewrites its OWN label. The page used to refresh
        /// all six on every mouse-move, and because these labels are transparent, each
        /// rewrite forces the card underneath to repaint its glass - six rounded-path fills
        /// per mouse event, for five labels whose text had not changed.</param>
        public SliderRow(Control parent, string caption,
            int minimum, int maximum, int? notch, int value, bool large = false,
            SliderPalette? palette = null, Func<int, string>? format = null)
        {
            _large = large;
            _format = format;

            _caption = new Label
            {
                Text = caption,
                Font = large ? LargeCaptionFont : CaptionFont,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            _value = new Label
            {
                Font = large ? LargeValueFont : ValueFont,
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
            };

            Slider = new TwoColorSlider
            {
                Minimum = minimum,
                Maximum = maximum,
                Notch = notch,
                Value = value,
                Palette = palette ?? SliderPalette.Accent(),
            };

            parent.Controls.Add(_caption);
            parent.Controls.Add(_value);
            parent.Controls.Add(Slider);

            // The row keeps its own readout current. Subscribed before anything the page
            // wires up, so the number on screen never trails the thumb by a frame.
            if (_format != null)
                Slider.ValueChanged += (_, _) => ValueText = _format(Slider.Value);
            SyncValueText();
        }

        /// <summary>Re-read the value into the label. Used after a programmatic change, where
        /// there was no drag to drive it.</summary>
        public void SyncValueText()
        {
            if (_format != null) ValueText = _format(Slider.Value);
        }

        /// <summary>The reading shown at the right of the caption row.</summary>
        public string ValueText
        {
            get => _value.Text;
            set { if (_value.Text != value) _value.Text = value; }
        }

        /// <summary>
        /// Position all three pieces as one row.
        ///
        /// The value column is a fixed slice of the right-hand edge rather than sized to its
        /// own text, so the left and right columns of the two-column grid line up with each
        /// other instead of each ending wherever its own number happened to.
        /// </summary>
        public void Place(int x, int y, int width)
        {
            int captionH = _large ? LargeCaptionH : CaptionH;
            int valueW = _large ? LargeValueW : ValueW;
            int captionW = Math.Max(Design.Tokens.Scale(40), width - valueW - Design.Tokens.Scale(8));

            _caption.SetBounds(x, y, captionW, captionH);
            _value.SetBounds(x + width - valueW, y, valueW, captionH);
            Slider.SetTrackBounds(x, y + captionH + CaptionGap, width);
        }

        /// <summary>Re-read theme colours. Labels are stock controls: they keep whatever they
        /// were built with until told otherwise, so a theme switch has to come through here.</summary>
        public void Restyle()
        {
            _caption.ForeColor = Theme.TextDim;
            _value.ForeColor = Theme.Text;

            // Fonts too, not just colours: after a DPI change Design.Fonts hands out
            // instances resolved at the new scale, and a Label keeps whatever it was given
            // until it is given something else.
            _caption.Font = _large ? LargeCaptionFont : CaptionFont;
            _value.Font = _large ? LargeValueFont : ValueFont;
        }
    }
}
