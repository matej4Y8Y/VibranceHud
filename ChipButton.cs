using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// A rounded "pill" chip button with hover and an Active state. Active = filled
    /// accent with dark text (used to show which preset matches the current level);
    /// inactive = dark surface with accent text and a hairline border.
    /// </summary>
    public sealed class ChipButton : Control
    {
        private bool _hover;
        private bool _active;

        /// <summary>The vibrance level this chip applies; used by the form to mark it active.</summary>
        public int Level { get; init; }

        public bool Active
        {
            get => _active;
            set { if (_active == value) return; _active = value; Invalidate(); }
        }

        public ChipButton()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Height = 32;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = PillPath(rect);

            if (_active)
            {
                using var fill = new SolidBrush(Theme.Accent);
                g.FillPath(fill, path);
            }
            else
            {
                using var fill = new SolidBrush(_hover ? Theme.SurfaceHover : Theme.Surface);
                g.FillPath(fill, path);
                using var pen = new Pen(Theme.Border, 1f);
                g.DrawPath(pen, path);
            }

            var textColor = _active ? Theme.Background : Theme.Accent;
            TextRenderer.DrawText(g, Text, Font, rect, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private static GraphicsPath PillPath(Rectangle rect)
        {
            var path = new GraphicsPath();
            int d = rect.Height; // full-height radius = pill
            path.AddArc(rect.X, rect.Y, d, d, 90, 180);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180);
            path.CloseFigure();
            return path;
        }
    }
}
