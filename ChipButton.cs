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
            var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            float radius = (Height - 1) / 2f; // pill

            if (_active)
                Glass.PaintAccent(g, rect, radius, Theme.Accent);
            else
                Glass.PaintPanel(g, rect, radius, baseAlpha: _hover ? 90 : 60, sheenTop: _hover ? 42 : 26);

            var textColor = _active ? Theme.Background : Theme.Accent;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
