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
        private const int Pad = 12;       // horizontal padding so the thumb never clips
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

            var fillRect = new Rectangle(trackRect.X, trackRect.Y, Math.Max(thumbX - Pad, 1), TrackHeight);
            using (var fill = new SolidBrush(Theme.Accent))
                FillRounded(g, fill, fillRect, TrackHeight / 2f);

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
