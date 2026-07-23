using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// The startup screen: the PlexusX mark on a rounded matte-glass card over the same
    /// particle field the app uses, with a status line and progress bar. It stays up while
    /// the app checks for (and installs) updates, so an update feels like part of loading
    /// rather than an interruption.
    /// </summary>
    public sealed class SplashForm : Form
    {
        private readonly ParticleField _field = new(45);
        private readonly System.Windows.Forms.Timer _timer;
        private DateTime _last = DateTime.UtcNow;

        private string _status = "Starting…";
        private int _progress = -1;   // -1 = indeterminate
        private float _sweep;         // indeterminate bar position

        private static readonly Font BrandFont = new(Theme.FontFamily, 22f, FontStyle.Bold);
        private static readonly Font StatusFont = new(Theme.FontFamily, 9f);

        public SplashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(440, 250);
            BackColor = Theme.Background;
            DoubleBuffered = true;
            Icon = AppIcon.Value;

            _field.Resize(ClientSize.Width, ClientSize.Height);

            _timer = new System.Windows.Forms.Timer { Interval = 33 };
            _timer.Tick += (s, e) =>
            {
                var now = DateTime.UtcNow;
                _field.Update(Math.Min((now - _last).TotalSeconds, 0.1));
                _last = now;
                _sweep = (_sweep + 0.012f) % 1f;
                Invalidate();
            };
            _timer.Start();
        }

        /// <summary>Update the status line (and optionally a 0-100 progress value).</summary>
        public void SetStatus(string text, int percent = -1)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => SetStatus(text, percent))); return; }
            _status = text;
            _progress = percent;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var back = new SolidBrush(Theme.Background))
                g.FillRectangle(back, ClientRectangle);
            _field.Paint(g, 0, 0);

            var card = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Glass.PaintPanel(g, card, 18, fillAlpha: 170);

            // Mark
            int size = 56;
            var iconRect = new Rectangle((Width - size) / 2, 34, size, size);
            using (var star = new SolidBrush(Theme.Accent))
                g.FillPolygon(star, StarPoints(iconRect));

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            TextRenderer.DrawText(g, "PlexusX", BrandFont,
                new Rectangle(0, 104, Width, 40), Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, _status, StatusFont,
                new Rectangle(0, 150, Width, 20), Theme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            DrawProgress(g, new Rectangle(60, 186, Width - 120, 5));
        }

        private void DrawProgress(Graphics g, Rectangle bar)
        {
            using (var track = new SolidBrush(Theme.Border))
                FillPill(g, track, bar);

            if (_progress >= 0)
            {
                int w = Math.Max(4, (int)(bar.Width * (_progress / 100f)));
                using var fill = new SolidBrush(Theme.Accent);
                FillPill(g, fill, new Rectangle(bar.X, bar.Y, w, bar.Height));
            }
            else
            {
                // Indeterminate: a short segment sweeping left to right.
                int w = bar.Width / 4;
                int x = bar.X + (int)((bar.Width + w) * _sweep) - w;
                var seg = Rectangle.Intersect(bar, new Rectangle(x, bar.Y, w, bar.Height));
                if (seg.Width > 2)
                {
                    using var fill = new SolidBrush(Theme.Accent);
                    FillPill(g, fill, seg);
                }
            }
        }

        private static void FillPill(Graphics g, Brush brush, Rectangle r)
        {
            if (r.Width < 2 || r.Height < 1) return;
            using var path = new GraphicsPath();
            float d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 90, 180);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            path.CloseFigure();
            g.FillPath(brush, path);
        }

        private static Point[] StarPoints(Rectangle r)
        {
            const int points = 12;
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            float outer = r.Width / 2f, inner = outer * 0.44f;
            var pts = new Point[points * 2];
            for (int i = 0; i < points * 2; i++)
            {
                double ang = Math.PI / points * i - Math.PI / 2;
                float rad = i % 2 == 0 ? outer : inner;
                pts[i] = new Point((int)(cx + rad * Math.Cos(ang)), (int)(cy + rad * Math.Sin(ang)));
            }
            return pts;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer?.Dispose();
            base.Dispose(disposing);
        }
    }
}
