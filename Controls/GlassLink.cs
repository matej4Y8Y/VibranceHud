using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// A text link in the app's own language.
    ///
    /// Replaces <see cref="LinkLabel"/>, which is a stock Win32 control: it brings its own
    /// focus rectangle, its own idea of what a visited link looks like, and its own hit
    /// testing. None of that can be themed, and the app only needs the two things a link
    /// actually is - text that reacts to the pointer, and something that happens when you
    /// press it.
    ///
    /// Underlined on hover only. A permanently underlined link inside a glass panel reads as
    /// a mistake rather than as an affordance, and the colour change already says "this does
    /// something".
    /// </summary>
    public sealed class GlassLink : Control
    {
        private bool _hover;

        public GlassLink()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.Selectable, true);

            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 20;

            TabStop = true;
            AccessibleRole = AccessibleRole.Link;
        }

        /// <summary>Where the text sits in the box. Centred by default, because the two links
        /// this replaced were both centred under something.</summary>
        public ContentAlignment TextAlign { get; set; } = ContentAlignment.MiddleCenter;

        /// <summary>Keep the accessible name in step with the label. Control.AccessibleName is
        /// not virtual, so it is mirrored here rather than overridden.</summary>
        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            AccessibleName = Text;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            _hover = false;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is not (Keys.Space or Keys.Enter)) return;
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }

        /// <summary>Test seam: OnKeyDown is protected and a unit test has no message loop.</summary>
        internal void TestPressKey(Keys key) => OnKeyDown(new KeyEventArgs(key));

        /// <summary>Click without a mouse, for callers that answer Enter themselves.</summary>
        public void PerformClick()
        {
            if (Enabled) OnClick(EventArgs.Empty);
        }

        private static TextFormatFlags Flags(ContentAlignment align) => align switch
        {
            ContentAlignment.MiddleLeft => TextFormatFlags.Left | TextFormatFlags.VerticalCenter,
            ContentAlignment.MiddleRight => TextFormatFlags.Right | TextFormatFlags.VerticalCenter,
            _ => TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
        };

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color colour = !Enabled ? Theme.Border
                         : _hover || Focused ? Theme.Accent
                         : Theme.TextDim;

            var font = _hover && Enabled
                ? new Font(Design.Fonts.Body, FontStyle.Underline)
                : Design.Fonts.Body;

            try
            {
                TextRenderer.DrawText(g, Text, font, ClientRectangle, colour, Flags(TextAlign));
            }
            finally
            {
                // Only the hover font is constructed here; the cached one must not be disposed.
                if (!ReferenceEquals(font, Design.Fonts.Body)) font.Dispose();
            }

            if (Focused && Enabled)
                UiHelpers.DrawFocusRing(g, ClientRectangle, 6f);
        }
    }
}
