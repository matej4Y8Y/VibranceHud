using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// A round colour chip for the theme picker: filled with the theme's accent (a white
    /// disc for the Light theme), ringed when it's the active theme. Click to select.
    /// </summary>
    public sealed class SwatchButton : Control
    {
        private bool _active;

        public ThemePalette Palette { get; }

        public bool Active
        {
            get => _active;
            set { if (_active == value) return; _active = value; Invalidate(); }
        }

        public SwatchButton(ThemePalette palette)
        {
            Palette = palette;
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Size = new Size(30, 30);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var fill = Palette.IsLight ? Color.White : Palette.Accent;
            var disc = new Rectangle(4, 4, Width - 9, Height - 9);

            if (_active)
                using (var ring = new Pen(Theme.Text, 2f))
                    g.DrawEllipse(ring, 1, 1, Width - 3, Height - 3);

            using (var brush = new SolidBrush(fill))
                g.FillEllipse(brush, disc);

            // Light's white disc needs an outline so it reads on a light card too.
            if (Palette.IsLight)
                using (var edge = new Pen(Theme.Border, 1f))
                    g.DrawEllipse(edge, disc);
        }
    }
}
