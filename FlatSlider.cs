using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// A flat, owner-drawn horizontal slider: thin rounded track, accent-colored fill,
    /// circular thumb, and an optional notch marker (used to mark 100% - the boundary
    /// between driver vibrance and software oversaturation). Replaces the dated stock
    /// TrackBar look.
    /// </summary>
    public sealed class FlatSlider : Control
    {
        /// <summary>
        /// Horizontal padding inside the control so the thumb never clips at either end.
        ///
        /// Public because it changes where callers should put the slider: the visible track
        /// runs from <c>Left + EdgeInset</c> to <c>Right - EdgeInset</c>, so a slider dropped
        /// at a card's text gutter draws its track 12px inside the captions and readouts
        /// above it. Place sliders with <see cref="SetTrackBounds"/> instead of guessing.
        /// </summary>
        public const int EdgeInset = 12;

        private const int Pad = EdgeInset;
        private const int ThumbRadius = 8;
        private const int TrackHeight = 4;

        private int _minimum;
        private int _maximum = 100;
        private int _value;
        private bool _dragging;

        public event EventHandler? ValueChanged;

        /// <summary>Draw a small marker at this value; null for none.</summary>
        public int? Notch { get; set; }

        public FlatSlider()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Height = 32;
            Cursor = Cursors.Hand;

            TabStop = true;
            SetStyle(ControlStyles.Selectable, true);
            AccessibleRole = AccessibleRole.Slider;
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        /// <summary>
        /// Keyboard control of the value.
        ///
        /// Arrows nudge by one, Page keys by ten, Home and End go to the limits. Without this
        /// a slider could be focused and not actually operated, which is worse than not being
        /// reachable at all - the focus ring promises something the control cannot do.
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            int step = e.KeyCode switch
            {
                Keys.Left or Keys.Down => -1,
                Keys.Right or Keys.Up => 1,
                Keys.PageDown => -10,
                Keys.PageUp => 10,
                _ => 0,
            };

            if (step != 0)
            {
                Value = Math.Clamp(Value + step, Minimum, Maximum);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Home) { Value = Minimum; e.Handled = true; }
            else if (e.KeyCode == Keys.End) { Value = Maximum; e.Handled = true; }
        }

        internal void TestPressKey(Keys key) => OnKeyDown(new KeyEventArgs(key));

        /// <summary>Announce the value, not just "slider". A screen reader that says a slider
        /// exists without saying where it is set has told the user nothing useful.</summary>
        protected override AccessibleObject CreateAccessibilityInstance() =>
            new SliderAccessibleObject(this);

        private sealed class SliderAccessibleObject : ControlAccessibleObject
        {
            private readonly FlatSlider _owner;
            public SliderAccessibleObject(FlatSlider owner) : base(owner) { _owner = owner; }
            public override string? Value => _owner.Value.ToString();
        }

        public int Minimum
        {
            get => _minimum;
            set { _minimum = value; Invalidate(); }
        }

        public int Maximum
        {
            get => _maximum;
            set { _maximum = Math.Max(value, _minimum + 1); Invalidate(); }
        }

        public int Value
        {
            get => _value;
            set
            {
                var clamped = Math.Clamp(value, _minimum, _maximum);
                if (clamped == _value) return;
                _value = clamped;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Position the slider so its visible TRACK spans exactly
        /// <paramref name="x"/>..<paramref name="x"/>+<paramref name="width"/>, i.e. so it
        /// lines up with the caption and value text of the row it belongs to. The control
        /// itself is grown by <see cref="EdgeInset"/> on each side to hold the thumb.</summary>
        public void SetTrackBounds(int x, int y, int width, int height = 32) =>
            SetBounds(x - EdgeInset, y, width + 2 * EdgeInset, height);

        private int XFromValue(int value)
        {
            float t = (value - _minimum) / (float)(_maximum - _minimum);
            return Pad + (int)(t * (Width - 2 * Pad));
        }

        private int ValueFromX(int x)
        {
            float t = (x - Pad) / (float)(Width - 2 * Pad);
            return _minimum + (int)Math.Round(Math.Clamp(t, 0f, 1f) * (_maximum - _minimum));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            Value = ValueFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging) Value = ValueFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int midY = Height / 2;
            int thumbX = XFromValue(_value);
            var trackRect = new Rectangle(Pad, midY - TrackHeight / 2, Width - 2 * Pad, TrackHeight);

            // Full track (dim), then the filled part up to the thumb (accent).
            using (var back = new SolidBrush(Theme.Border))
                FillRounded(g, back, trackRect, TrackHeight / 2f);

            // At the minimum there is nothing filled yet. Drawing a 1px-wide pill here used
            // to spill a small accent blob ~3px past the left end of the track, because the
            // right-hand arc of the rounded path lands left of the rect's own origin once
            // the rect is narrower than the corner diameter.
            int fillW = thumbX - Pad;
            if (fillW >= TrackHeight)
            {
                var fillRect = new Rectangle(trackRect.X, trackRect.Y, fillW, TrackHeight);
                using var fill = new SolidBrush(Theme.Accent);
                FillRounded(g, fill, fillRect, TrackHeight / 2f);
            }

            // Notch marker (e.g. at 100 = driver max).
            if (Notch is int notch && notch > _minimum && notch < _maximum)
            {
                int nx = XFromValue(notch);
                using var pen = new Pen(Theme.TextDim, 1.5f);
                g.DrawLine(pen, nx, midY - 8, nx, midY + 8);
            }

            // Thumb: accent circle with a darker outline so it pops on the track.
            var thumb = new Rectangle(thumbX - ThumbRadius, midY - ThumbRadius, ThumbRadius * 2, ThumbRadius * 2);
            using (var brush = new SolidBrush(Theme.Accent))
                g.FillEllipse(brush, thumb);
            using (var outline = new Pen(Theme.Background, 2.5f))
                g.DrawEllipse(outline, thumb);

            // Around the thumb rather than the whole control: the track spans the full width
            // and a ring along all of it would read as the row being selected, not focused.
            if (Focused)
                UiHelpers.DrawFocusRing(g, Rectangle.Inflate(thumb, 4, 4), (ThumbRadius + 4));
        }

        private static void FillRounded(Graphics g, Brush brush, Rectangle rect, float radius)
        {
            using var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 90, 180);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180);
            path.CloseFigure();
            g.FillPath(brush, path);
        }
    }
}
