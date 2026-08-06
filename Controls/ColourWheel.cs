using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace VibranceHud.Controls
{
    /// <summary>
    /// An HSV colour wheel with a brightness bar beside it.
    ///
    /// Owner-drawn rather than Windows' own ColorDialog. The stock dialog is a grey Win32
    /// window that would land in the middle of a glass app the same way the MessageBoxes did,
    /// and it takes over the screen for what is a small adjustment - you cannot see the
    /// crosshair change while you are picking.
    ///
    /// Hue runs around the wheel, saturation from the centre out, and brightness is the bar.
    /// That split is deliberate: hue and saturation are the two people actually hunt for, and
    /// putting them on one surface means a colour is one gesture rather than three sliders.
    ///
    /// The wheel is rendered once into a bitmap and reused. It is a per-pixel trigonometric
    /// fill - cheap once, far too expensive on every repaint of a control that repaints
    /// whenever the plexus animates behind it.
    /// </summary>
    public sealed class ColourWheel : Control
    {
        private const int BarWidth = 18;
        private const int BarGap = 12;

        private Bitmap? _wheel;
        private int _wheelDiameter;

        private float _hue;          // 0-360
        private float _saturation;   // 0-1
        private float _value = 1f;   // 0-1

        private bool _draggingWheel;
        private bool _draggingBar;

        /// <summary>Raised while the user is choosing, not only when they let go - the point
        /// of an inline picker is watching the crosshair change as you move.</summary>
        public event EventHandler? ColourChanged;

        public ColourWheel()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.Selectable, true);

            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.Slider;
            AccessibleName = "Colour picker";
            Size = new Size(180, 160);
        }

        /// <summary>The chosen colour. Setting it moves the picker to match.</summary>
        public Color Colour
        {
            get => FromHsv(_hue, _saturation, _value);
            set
            {
                ToHsv(value, out _hue, out _saturation, out _value);
                Invalidate();
            }
        }

        // ---- geometry --------------------------------------------------------------------

        private int Diameter => Math.Max(8, Math.Min(Width - BarWidth - BarGap, Height));
        private Rectangle WheelRect => new(0, (Height - Diameter) / 2, Diameter, Diameter);
        // One pixel short on the right and bottom: DrawRectangle draws the last line *on* the
        // far edge, so a bar sized to the full width has its right border painted outside the
        // control and simply vanishes.
        private Rectangle BarRect => new(Width - BarWidth, 4, BarWidth - 1, Math.Max(8, Height - 9));

        // ---- input -----------------------------------------------------------------------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (BarRect.Contains(e.Location)) { _draggingBar = true; TakeBar(e.Y); }
            else if (WheelRect.Contains(e.Location)) { _draggingWheel = true; TakeWheel(e.Location); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_draggingBar) TakeBar(e.Y);
            else if (_draggingWheel) TakeWheel(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _draggingWheel = false;
            _draggingBar = false;
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        /// <summary>
        /// Keyboard control. Left and right walk the hue, up and down the brightness.
        ///
        /// Without this the picker can be focused and not used, which is worse than not being
        /// reachable - the focus ring promises something the control cannot do.
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            switch (e.KeyCode)
            {
                case Keys.Left: _hue = (_hue + 355) % 360; break;
                case Keys.Right: _hue = (_hue + 5) % 360; break;
                case Keys.Up: _value = Math.Min(1f, _value + 0.05f); break;
                case Keys.Down: _value = Math.Max(0f, _value - 0.05f); break;
                case Keys.PageUp: _saturation = Math.Min(1f, _saturation + 0.1f); break;
                case Keys.PageDown: _saturation = Math.Max(0f, _saturation - 0.1f); break;
                default: return;
            }

            e.Handled = true;
            Invalidate();
            ColourChanged?.Invoke(this, EventArgs.Empty);
        }

        internal void TestPressKey(Keys key) => OnKeyDown(new KeyEventArgs(key));

        /// <summary>
        /// Claim the keys the picker actually uses.
        ///
        /// Without this the arrows never reach OnKeyDown at all - WinForms treats them as
        /// dialog navigation and moves focus to the next control instead, so everything above
        /// was dead code and the focus ring advertised a control that could not be operated.
        /// Tab is deliberately left alone: it still has to be able to leave the wheel.
        /// </summary>
        /// <summary>Test seam: IsInputKey is protected, and the bug it guards against is
        /// invisible from outside - the control simply never sees the key.</summary>
        internal bool TestClaimsKey(Keys key) => IsInputKey(key);

        protected override bool IsInputKey(Keys keyData) => keyData switch
        {
            Keys.Left or Keys.Right or Keys.Up or Keys.Down
                or Keys.PageUp or Keys.PageDown => true,
            _ => base.IsInputKey(keyData),
        };

        private void TakeWheel(Point p)
        {
            var r = WheelRect;
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            float dx = p.X - cx, dy = p.Y - cy;
            float radius = r.Width / 2f;

            float distance = (float)Math.Sqrt(dx * dx + dy * dy);

            // Clamped rather than ignored: dragging past the rim should ride the edge at full
            // saturation, not stop dead the moment the cursor leaves the circle.
            _saturation = Math.Clamp(distance / radius, 0f, 1f);
            _hue = (float)((Math.Atan2(dy, dx) * 180.0 / Math.PI + 360) % 360);

            Invalidate();
            ColourChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TakeBar(int y)
        {
            var r = BarRect;
            _value = 1f - Math.Clamp((y - r.Y) / (float)r.Height, 0f, 1f);

            Invalidate();
            ColourChanged?.Invoke(this, EventArgs.Empty);
        }

        // ---- painting --------------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var wheel = WheelRect;
            if (wheel.Width > 8)
            {
                EnsureWheel(wheel.Width);
                if (_wheel != null) g.DrawImage(_wheel, wheel);

                // The wheel bitmap is drawn at full brightness; the chosen value darkens it,
                // so what is on screen is always the colour that would actually be applied.
                if (_value < 1f)
                {
                    using var shade = new SolidBrush(
                        Color.FromArgb((int)(255 * (1 - _value)), 0, 0, 0));
                    using var clip = new GraphicsPath();
                    clip.AddEllipse(wheel);
                    var saved = g.Clip;
                    g.SetClip(clip);
                    g.FillRectangle(shade, wheel);
                    g.Clip = saved;
                }

                DrawWheelMarker(g, wheel);
            }

            DrawBar(g);

            if (Focused) UiHelpers.DrawFocusRing(g, ClientRectangle, 6f);
        }

        private void DrawWheelMarker(Graphics g, Rectangle wheel)
        {
            float radius = wheel.Width / 2f;
            float angle = (float)(_hue * Math.PI / 180.0);
            float cx = wheel.X + radius + (float)Math.Cos(angle) * radius * _saturation;
            float cy = wheel.Y + radius + (float)Math.Sin(angle) * radius * _saturation;

            var dot = new RectangleF(cx - 6, cy - 6, 12, 12);

            // Dark ring outside, light ring inside: readable on any hue without needing to
            // know which one is underneath.
            using (var outer = new Pen(Color.FromArgb(200, 0, 0, 0), 3f)) g.DrawEllipse(outer, dot);
            using (var inner = new Pen(Color.White, 1.6f)) g.DrawEllipse(inner, dot);
        }

        private void DrawBar(Graphics g)
        {
            var r = BarRect;
            if (r.Height <= 8) return;

            var full = FromHsv(_hue, _saturation, 1f);
            using (var gradient = new LinearGradientBrush(r, full, Color.Black, 90f))
                g.FillRectangle(gradient, r);

            using (var rim = new Pen(Color.FromArgb(90, Theme.GlassEdge), 1f))
                g.DrawRectangle(rim, r);

            int y = r.Y + (int)((1 - _value) * r.Height);
            using (var outer = new Pen(Color.FromArgb(200, 0, 0, 0), 3f))
                g.DrawLine(outer, r.X, y, r.Right, y);
            using (var inner = new Pen(Color.White, 1.4f))
                g.DrawLine(inner, r.X, y, r.Right, y);
        }

        /// <summary>
        /// Render the wheel once and keep it.
        ///
        /// This is a per-pixel fill with a trig call each - fine once, ruinous on a control
        /// that repaints every time the plexus animates behind it.
        /// </summary>
        private void EnsureWheel(int diameter)
        {
            if (_wheel != null && _wheelDiameter == diameter) return;

            _wheel?.Dispose();
            _wheelDiameter = diameter;
            _wheel = new Bitmap(diameter, diameter, PixelFormat.Format32bppArgb);

            float radius = diameter / 2f;

            var data = _wheel.LockBits(new Rectangle(0, 0, diameter, diameter),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[data.Stride];

                for (int y = 0; y < diameter; y++)
                {
                    Array.Clear(row, 0, row.Length);

                    for (int x = 0; x < diameter; x++)
                    {
                        float dx = x - radius + 0.5f, dy = y - radius + 0.5f;
                        float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (distance > radius) continue;   // outside stays transparent

                        float hue = (float)((Math.Atan2(dy, dx) * 180.0 / Math.PI + 360) % 360);
                        var colour = FromHsv(hue, Math.Min(1f, distance / radius), 1f);

                        // Feather the last pixel so the rim is not a staircase.
                        byte alpha = distance > radius - 1.2f
                            ? (byte)(255 * Math.Clamp(radius - distance, 0f, 1.2f) / 1.2f)
                            : (byte)255;

                        int i = x * 4;
                        row[i + 0] = colour.B;
                        row[i + 1] = colour.G;
                        row[i + 2] = colour.R;
                        row[i + 3] = alpha;
                    }

                    System.Runtime.InteropServices.Marshal.Copy(
                        row, 0, data.Scan0 + y * data.Stride, data.Stride);
                }
            }
            finally { _wheel.UnlockBits(data); }
        }

        // ---- colour maths ----------------------------------------------------------------

        /// <summary>HSV to RGB. Public and static so the conversion is testable on its own -
        /// a wheel that hands back the wrong colour is very hard to spot by eye.</summary>
        public static Color FromHsv(float hue, float saturation, float value)
        {
            hue = ((hue % 360) + 360) % 360;
            saturation = Math.Clamp(saturation, 0f, 1f);
            value = Math.Clamp(value, 0f, 1f);

            float c = value * saturation;
            float x = c * (1 - Math.Abs((hue / 60f) % 2 - 1));
            float m = value - c;

            (float r, float g, float b) = hue switch
            {
                < 60 => (c, x, 0f),
                < 120 => (x, c, 0f),
                < 180 => (0f, c, x),
                < 240 => (0f, x, c),
                < 300 => (x, 0f, c),
                _ => (c, 0f, x),
            };

            return Color.FromArgb(255,
                (int)Math.Round((r + m) * 255),
                (int)Math.Round((g + m) * 255),
                (int)Math.Round((b + m) * 255));
        }

        /// <summary>The colour as six hex digits, no leading hash. Alpha is deliberately left
        /// out - opacity is a separate control, and folding it in here would mean pasting a
        /// friend's colour silently changed how transparent your crosshair is.</summary>
        public static string ToHex(Color colour) =>
            $"{colour.R:X2}{colour.G:X2}{colour.B:X2}";

        /// <summary>
        /// Read a hex colour the forgiving way: with or without the hash, upper or lower case,
        /// and in the three-digit short form as well as the usual six.
        ///
        /// Forgiving because this is the field people paste into. Rejecting "#00ff66" for the
        /// hash, when it is the form every colour picker on the web hands out, would make the
        /// field look broken rather than strict.
        /// </summary>
        public static bool TryParseHex(string? text, out Color colour)
        {
            colour = Color.Empty;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var s = text.Trim().TrimStart('#');
            if (s.Length is not (3 or 6)) return false;

            foreach (char c in s)
                if (!Uri.IsHexDigit(c)) return false;

            if (s.Length == 3)                       // 0f6 means 00ff66
                s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);

            colour = Color.FromArgb(255,
                Convert.ToInt32(s.Substring(0, 2), 16),
                Convert.ToInt32(s.Substring(2, 2), 16),
                Convert.ToInt32(s.Substring(4, 2), 16));
            return true;
        }

        public static void ToHsv(Color colour, out float hue, out float saturation, out float value)
        {
            float r = colour.R / 255f, g = colour.G / 255f, b = colour.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;

            value = max;
            saturation = max <= 0f ? 0f : delta / max;

            if (delta <= 0f) { hue = 0f; return; }

            hue = max == r ? 60 * (((g - b) / delta) % 6)
                : max == g ? 60 * (((b - r) / delta) + 2)
                : 60 * (((r - g) / delta) + 4);

            hue = ((hue % 360) + 360) % 360;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _wheel?.Dispose();
            base.Dispose(disposing);
        }
    }
}
