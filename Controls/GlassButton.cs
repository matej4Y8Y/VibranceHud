using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// A pill button in the app's own language, in two weights:
    /// <see cref="GlassButtonKind.Primary"/> - filled accent, for the one action the
    /// page exists to perform; <see cref="GlassButtonKind.Ghost"/> - glass fill with a
    /// hairline rim, for everything else.
    ///
    /// Replaces the stock <see cref="Button"/>, which even with <c>FlatStyle.Flat</c>
    /// keeps square corners and a system focus rectangle - visibly foreign next to the
    /// rounded chips and cards on every other page.
    /// </summary>
    public enum GlassButtonKind
    {
        Primary,
        Ghost,
    }

    public sealed class GlassButton : Control
    {
        private bool _hover;
        private bool _pressed;

        public GlassButton()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 34;
            Font = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold);

            // Reachable and announced. Only three controls in the app did this before, so
            // tabbing through PlexusX was invisible and a screen reader saw nothing at all.
            TabStop = true;
            SetStyle(ControlStyles.Selectable, true);
            AccessibleRole = AccessibleRole.PushButton;
        }

        public GlassButtonKind Kind { get; init; } = GlassButtonKind.Ghost;

        /// <summary>Keep the accessible name in step with the label. Control.AccessibleName
        /// is not virtual, so it is mirrored here rather than overridden.</summary>
        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            AccessibleName = Text;
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is not (Keys.Space or Keys.Enter)) return;
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }

        /// <summary>Test seam: OnKeyDown is protected and a unit test has no message loop.</summary>
        internal void TestPressKey(Keys key) => OnKeyDown(new KeyEventArgs(key));

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
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            // Clicking focuses, so the keyboard carries on from where the mouse left off
            // rather than jumping back to the top of the page.
            Focus();
            _pressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _pressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            float radius = (Height - 1) / 2f;   // full pill

            Color textColor;
            if (Kind == GlassButtonKind.Primary)
            {
                // Pressed dims the accent rather than moving the label - a 1px text
                // nudge reads as a rendering glitch at this size.
                int alpha = _pressed ? 175 : _hover ? 235 : 210;
                Glass.PaintAccent(g, rect, radius, Theme.Accent, alpha);
                textColor = Theme.OnAccent;
            }
            else
            {
                Glass.PaintPanel(g, rect, radius, fillAlpha: _pressed ? 195 : _hover ? 180 : 148);
                textColor = Theme.Text;
            }

            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (Focused) UiHelpers.DrawFocusRing(g, ClientRectangle, radius);
        }
    }
}
