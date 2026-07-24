using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// One row in the left navigation: a small drawn icon + label. Active state gets an
    /// accent left bar, a filled surface, and brighter text. Icons are drawn with GDI
    /// (not a font) so they render identically on every machine.
    /// </summary>
    public sealed class NavButton : Control
    {
        private bool _active;
        private bool _hover;

        /// <summary>0 = vibrance, 1 = games, 2 = settings, 3 = account, 4 = fps (lightning).</summary>
        public int IconKind { get; init; }

        public bool Active
        {
            get => _active;
            set { if (_active == value) return; _active = value; Invalidate(); }
        }

        public NavButton()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw, true);
            Height = 46;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bg = _active ? Theme.SurfaceHover : (_hover ? Theme.Surface : Theme.Background);
            using (var back = new SolidBrush(bg))
                g.FillRectangle(back, ClientRectangle);

            if (_active)
                using (var bar = new SolidBrush(Theme.Accent))
                    g.FillRectangle(bar, 0, 8, 3, Height - 16);

            var color = _active ? Theme.Accent : Theme.TextDim;
            DrawIcon(g, IconKind, new Rectangle(20, (Height - 18) / 2, 18, 18), color);

            var textColor = _active ? Theme.Text : Theme.TextDim;
            using var labelFont = new Font(Theme.FontFamily, 9.5f, _active ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(g, Text, labelFont, new Rectangle(52, 0, Width - 56, Height), textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        private static void DrawIcon(Graphics g, int kind, Rectangle r, Color color)
        {
            using var pen = new Pen(color, 1.6f);
            using var brush = new SolidBrush(color);
            switch (kind)
            {
                case 0: // vibrance / contrast: circle, right half filled
                    g.DrawEllipse(pen, r);
                    g.FillPie(brush, r, -90, 180);
                    break;

                case 1: // games: controller body + two buttons
                    var body = new Rectangle(r.X, r.Y + 4, r.Width, r.Height - 8);
                    using (var path = RoundedRect(body, 5))
                        g.DrawPath(pen, path);
                    g.FillEllipse(brush, r.Right - 6, r.Y + 6, 3, 3);
                    g.FillEllipse(brush, r.Right - 10, r.Y + 9, 3, 3);
                    break;

                case 2: // settings: three sliders with offset knobs
                    for (int i = 0; i < 3; i++)
                    {
                        int y = r.Y + 2 + i * 6;
                        g.DrawLine(pen, r.X, y, r.Right, y);
                        int kx = r.X + (i == 1 ? r.Width - 8 : 4);
                        g.FillEllipse(brush, kx, y - 2, 4, 4);
                    }
                    break;

                case 4: // fps boost: lightning bolt
                    var bolt = new[]
                    {
                        new PointF(r.X + r.Width * 0.55f, r.Y),
                        new PointF(r.X + r.Width * 0.15f, r.Y + r.Height * 0.55f),
                        new PointF(r.X + r.Width * 0.45f, r.Y + r.Height * 0.55f),
                        new PointF(r.X + r.Width * 0.40f, r.Y + r.Height),
                        new PointF(r.X + r.Width * 0.85f, r.Y + r.Height * 0.40f),
                        new PointF(r.X + r.Width * 0.55f, r.Y + r.Height * 0.40f),
                    };
                    g.FillPolygon(brush, bolt);
                    break;

                default: // account: head + shoulders
                    g.DrawEllipse(pen, r.X + 5, r.Y, 8, 8);
                    g.DrawArc(pen, r.X + 1, r.Y + 9, r.Width - 2, r.Height, 200, 140);
                    break;
            }
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
