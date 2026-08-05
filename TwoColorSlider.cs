using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// The colour ramp a slider paints itself with, low end through neutral to high end.
    ///
    /// The ramp is the point of this control: the fill and the thumb are coloured by where
    /// the value currently sits, so the slider shows you what it does rather than just how
    /// far along it is. Temperature runs blue through grey to orange; brightness runs dark to
    /// bright. Because the colour is a function of the value and not of the mouse state, it
    /// transforms continuously while you drag instead of flicking on press and off release.
    /// </summary>
    public sealed record SliderPalette(Color Low, Color Mid, Color High)
    {
        /// <summary>Colour at <paramref name="t"/> in 0..1, blended Low → Mid → High.</summary>
        public Color At(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t <= 0.5f
                ? Blend(Low, Mid, t * 2f)
                : Blend(Mid, High, (t - 0.5f) * 2f);
        }

        private static Color Blend(Color a, Color b, float k) => Color.FromArgb(
            (int)(a.R + (b.R - a.R) * k),
            (int)(a.G + (b.G - a.G) * k),
            (int)(a.B + (b.B - a.B) * k));

        /// <summary>Muted through the theme accent to a hot end. The default for anything
        /// that is "more of the thing" as it goes right.</summary>
        public static SliderPalette Accent() => new(
            Color.FromArgb(118, 118, 132),
            Theme.Accent,
            Lift(Theme.Accent, 0.45f));

        /// <summary>Cool blue, neutral grey, warm amber - the actual meaning of the control.</summary>
        public static readonly SliderPalette Temperature = new(
            Color.FromArgb(74, 156, 255),
            Color.FromArgb(150, 150, 160),
            Color.FromArgb(255, 168, 74));

        /// <summary>Dark to bright, for brightness and gamma.</summary>
        public static readonly SliderPalette Luminance = new(
            Color.FromArgb(58, 60, 74),
            Color.FromArgb(150, 152, 168),
            Color.FromArgb(248, 248, 252));

        /// <summary>Flat to punchy.</summary>
        public static readonly SliderPalette Contrast = new(
            Color.FromArgb(120, 122, 134),
            Color.FromArgb(176, 178, 192),
            Color.FromArgb(250, 250, 255));

        private static Color Lift(Color c, float k) => Color.FromArgb(
            (int)(c.R + (255 - c.R) * k),
            (int)(c.G + (255 - c.G) * k),
            (int)(c.B + (255 - c.B) * k));
    }

    /// <summary>
    /// A flat horizontal slider: rounded track, colour-ramped fill, round thumb outlined
    /// against the background so it reads on top of the fill.
    ///
    /// Back to the heavier geometry of the original FlatSlider - a 4px track and a real thumb
    /// - because the hairline version read as a progress bar rather than something you grab.
    /// What is new is that the colour is derived from the value, so dragging transforms it
    /// smoothly all the way along instead of switching state on click.
    /// </summary>
    public sealed class TwoColorSlider : Control
    {
        /// <summary>Padding inside the control so the thumb can't clip at either end. The
        /// visible track runs from Left+EdgeInset to Right-EdgeInset; place with
        /// <see cref="SetTrackBounds"/> so the track lines up with the text above it.</summary>
        public const int EdgeInset = 11;
        public const int MinHeight = 30;

        private const int Pad = EdgeInset;
        private const int TrackHeight = 4;
        private const int ThumbRadius = 9;

        private int _minimum;
        private int _maximum = 100;
        private int _value;
        private int? _notch;
        private bool _enabled = true;
        private bool _hover;
        private bool _dragging;

        public event EventHandler? ValueChanged;
        public event EventHandler? DragBegin;
        public event EventHandler? DragEnd;

        /// <summary>Colours this slider paints itself with. Defaults to the accent ramp.</summary>
        public SliderPalette Palette { get; set; } = SliderPalette.Accent();

        public int Minimum { get => _minimum; set { _minimum = value; Value = _value; Invalidate(); } }
        public int Maximum { get => _maximum; set { _maximum = Math.Max(value, _minimum + 1); Value = _value; Invalidate(); } }

        public int Value
        {
            get => _value;
            set
            {
                var clamped = Math.Clamp(value, _minimum, _maximum);
                if (clamped == _value) return;

                int oldX = XFromValue(_value);
                _value = clamped;
                int newX = XFromValue(_value);

                // Repaint only the strip that actually changed, not the whole control.
                //
                // This matters far more here than it looks. The slider is transparent, so
                // WinForms satisfies its background by painting the card underneath it, and
                // the card is transparent too, so that reaches the page - which paints the
                // wallpaper and the whole animated particle field. A full-width Invalidate on
                // every mouse-move was dragging that entire stack through, 100+ times a
                // second. Confining it to the strip between the old and new thumb positions
                // cuts the repainted area to a fraction of it.
                InvalidateBetween(oldX, newX);
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Invalidate the span between two thumb positions, with enough margin for
        /// the thumb, its ring and its hover glow.</summary>
        private void InvalidateBetween(int x1, int x2)
        {
            const int margin = ThumbRadius + 12;
            int left = Math.Min(x1, x2) - margin;
            int right = Math.Max(x1, x2) + margin;
            Invalidate(new Rectangle(left, 0, right - left, Height));
        }

        public int? Notch { get => _notch; set { _notch = value; Invalidate(); } }
        public new bool Enabled { get => _enabled; set { _enabled = value; Invalidate(); } }

        public TwoColorSlider()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Height = MinHeight;
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        public void SetTrackBounds(int x, int y, int width) =>
            SetBounds(x - EdgeInset, y, width + 2 * EdgeInset, MinHeight);

        /// <summary>Where the value sits in 0..1 - the input to the colour ramp.</summary>
        private float Fraction => (_value - _minimum) / (float)(_maximum - _minimum);

        private int XFromValue(int value)
        {
            float t = (value - _minimum) / (float)(_maximum - _minimum);
            return Pad + (int)Math.Round(t * (Width - 2 * Pad));
        }

        private int ValueFromX(int x)
        {
            float t = (x - Pad) / (float)(Width - 2 * Pad);
            return _minimum + (int)Math.Round(Math.Clamp(t, 0f, 1f) * (_maximum - _minimum));
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!_enabled || e.Button != MouseButtons.Left) return;
            _dragging = true;
            Focus();

            // DragBegin BEFORE the first value change, not after. The engine uses it to
            // suppress overlay writes for the duration of the drag, and the overlay write is
            // a MagSetFullscreenColorEffect syscall that blocks the UI thread for 10-30ms.
            // Raising it afterwards meant the very first movement of every drag paid that
            // cost - which is exactly the hitch you feel when you grab a slider.
            DragBegin?.Invoke(this, EventArgs.Empty);
            Value = ValueFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging && _enabled) Value = ValueFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !_dragging) return;
            _dragging = false;
            DragEnd?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!_enabled) return;
            Value += Math.Sign(e.Delta);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!_enabled) return;
            int step = e.Shift ? 10 : 1;
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Down: Value -= step; e.Handled = true; break;
                case Keys.Right: case Keys.Up: Value += step; e.Handled = true; break;
                case Keys.Home: Value = _minimum; e.Handled = true; break;
                case Keys.End: Value = _maximum; e.Handled = true; break;
            }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int midY = Height / 2;
            int thumbX = XFromValue(_value);
            var track = new Rectangle(Pad, midY - TrackHeight / 2, Width - 2 * Pad, TrackHeight);

            // Unfilled track.
            using (var back = new SolidBrush(Theme.Border))
                FillPill(g, back, track, TrackHeight / 2f);

            // Filled part, painted as a gradient that ends on the colour for the CURRENT
            // value. Both the width and that end colour move with the drag, which is what
            // makes the change read as a transformation rather than a step.
            int fillW = thumbX - Pad;
            var thumbColour = Palette.At(Fraction);
            if (fillW >= TrackHeight)
            {
                var fill = new Rectangle(track.X, track.Y, fillW, TrackHeight);
                using var brush = new LinearGradientBrush(
                    new Rectangle(fill.X, fill.Y - 1, fill.Width, fill.Height + 2),
                    Palette.At(0f), thumbColour, LinearGradientMode.Horizontal);
                brush.WrapMode = WrapMode.TileFlipXY;   // no seam at either end
                FillPill(g, brush, fill, TrackHeight / 2f);
            }

            // Notch (e.g. 100 = where the driver runs out).
            if (_notch is int notch && notch > _minimum && notch < _maximum)
            {
                int nx = XFromValue(notch);
                using var pen = new Pen(Color.FromArgb(150, Theme.TextDim), 1.4f);
                g.DrawLine(pen, nx, midY - 7, nx, midY + 7);
            }

            // Thumb: the value's own colour, ringed in the page background so it stays
            // legible wherever it sits on the fill. Grows slightly on hover/drag.
            int radius = ThumbRadius + (_dragging ? 2 : _hover ? 1 : 0);
            var thumb = new Rectangle(thumbX - radius, midY - radius, radius * 2, radius * 2);

            if (_dragging || _hover)
            {
                using var halo = new SolidBrush(Color.FromArgb(_dragging ? 70 : 40, thumbColour));
                g.FillEllipse(halo, Rectangle.Inflate(thumb, 6, 6));
            }

            int alpha = _enabled ? 255 : 130;
            using (var fill = new SolidBrush(Color.FromArgb(alpha, thumbColour)))
                g.FillEllipse(fill, thumb);
            using (var ring = new Pen(Theme.Background, 2.5f))
                g.DrawEllipse(ring, thumb);

            if (Focused)
            {
                using var focus = new Pen(Color.FromArgb(220, Theme.Accent), 1.4f);
                g.DrawEllipse(focus, Rectangle.Inflate(thumb, 4, 4));
            }
        }

        private static void FillPill(Graphics g, Brush brush, Rectangle r, float radius)
        {
            if (r.Width < 2 || r.Height < 1) return;
            using var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 90, 180);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            path.CloseFigure();
            g.FillPath(brush, path);
        }
    }
}
