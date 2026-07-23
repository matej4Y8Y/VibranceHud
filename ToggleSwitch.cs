using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// A modern pill toggle switch (the premium replacement for a stock checkbox):
    /// violet track with the knob to the right when on, dim track with the knob left
    /// when off. Click anywhere to flip.
    /// </summary>
    public sealed class ToggleSwitch : Control
    {
        private bool _checked;

        public event EventHandler? CheckedChanged;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Size = new Size(44, 22);
            Cursor = Cursors.Hand;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = PillPath(track))
            using (var fill = new SolidBrush(_checked ? Theme.Accent : Theme.Border))
            {
                g.FillPath(fill, path);
            }

            int knobSize = Height - 6;
            int knobX = _checked ? Width - knobSize - 4 : 3;
            var knob = new Rectangle(knobX, 3, knobSize, knobSize);
            using (var brush = new SolidBrush(_checked ? Theme.Background : Theme.Text))
            {
                g.FillEllipse(brush, knob);
            }
        }

        private static GraphicsPath PillPath(Rectangle rect)
        {
            var path = new GraphicsPath();
            int d = rect.Height;
            path.AddArc(rect.X, rect.Y, d, d, 90, 180);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180);
            path.CloseFigure();
            return path;
        }
    }
}
