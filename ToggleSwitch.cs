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

            TabStop = true;
            SetStyle(ControlStyles.Selectable, true);
            AccessibleRole = AccessibleRole.CheckButton;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is not (Keys.Space or Keys.Enter)) return;
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }

        internal void TestPressKey(Keys key) => OnKeyDown(new KeyEventArgs(key));

        /// <summary>
        /// Announce on/off, not just "check button".
        ///
        /// A screen reader reading a toggle without its state tells the user a switch exists
        /// and nothing about what it is currently doing, which for something like "Start with
        /// Windows" is the only part that matters.
        /// </summary>
        protected override AccessibleObject CreateAccessibilityInstance() =>
            new ToggleAccessibleObject(this);

        private sealed class ToggleAccessibleObject : ControlAccessibleObject
        {
            private readonly ToggleSwitch _owner;
            public ToggleAccessibleObject(ToggleSwitch owner) : base(owner) { _owner = owner; }

            public override AccessibleStates State =>
                base.State | (_owner.Checked ? AccessibleStates.Checked : AccessibleStates.None);
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

            // Centre the knob in the track rect that was actually drawn (Width-1 x Height-1),
            // not in the control's raw size. Sizing it off the raw height left an odd number
            // of pixels to share between top and bottom, so the knob sat a pixel low in every
            // toggle in the app.
            int knobSize = track.Height - 6;
            int inset = (track.Height - knobSize) / 2;
            int knobX = _checked ? track.Right - knobSize - inset : track.X + inset;
            var knob = new Rectangle(knobX, track.Y + inset, knobSize, knobSize);
            using (var brush = new SolidBrush(_checked ? Theme.Background : Theme.Text))
            {
                g.FillEllipse(brush, knob);
            }

            if (Focused) UiHelpers.DrawFocusRing(g, ClientRectangle, Height / 2f);
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
