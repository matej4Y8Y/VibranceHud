using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VibranceHud.Pages
{
    /// <summary>
    /// Base for every page: paints its slice of the window's shared particle field as the
    /// background, so the whole app shares one continuous backdrop. The field instance and
    /// the page's window offset are injected by the window, which also owns the single
    /// animation timer.
    ///
    /// Derived pages that override OnPaint must call base.OnPaint(e) first, then draw their
    /// content on top; pages that only host child controls need do nothing.
    /// </summary>
    public class GlowPage : UserControl
    {
        public ParticleField? Field { get; set; }
        public Point FieldOffset { get; set; }

        protected GlowPage()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw, true);
        }

        /// <summary>
        /// Draw the page and everything on it into one off-screen buffer before it reaches the
        /// screen.
        ///
        /// Repainting on scroll fixed the smearing but introduced a flicker, because the two
        /// steps were visible separately: Windows drags the old pixels to their new position,
        /// then our repaint snaps the backdrop back to where it belongs. One frame of each,
        /// which reads as the background trying to follow the scroll.
        ///
        /// WS_EX_COMPOSITED makes both happen off-screen and arrive as a single frame, so
        /// there's no intermediate state to see. This is also why it goes on the page rather
        /// than the window - it covers the page and its children without forcing the whole
        /// app through one buffer.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_COMPOSITED = 0x02000000;
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_COMPOSITED;
                return cp;
            }
        }

        // ---- scrolling ---------------------------------------------------------------------
        //
        // The backdrop is painted at a fixed window offset - it is one continuous picture
        // behind the whole app and is not supposed to move. Windows, though, scrolls by
        // copying the pixels that are already on screen and repainting only the thin strip
        // that just came into view. So it drags the backdrop along with the cards, and the
        // rest of it is never redrawn: smeared plexus lines, cards printed over other cards,
        // captions appearing twice.
        //
        // Repainting the whole page on every scroll costs a frame and removes the artefact
        // entirely. Children are included because the copy has already moved them.

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            Invalidate(true);
        }

        /// <summary>
        /// Scroll the wheel ourselves.
        ///
        /// ScrollableControl only acts on the wheel when its scrollbar is actually visible -
        /// it checks VScroll first. Hiding the native bars therefore killed wheel scrolling
        /// outright, which is far worse than the ugly bar it was hiding.
        ///
        /// Worth stating why the test missed it: it moved AutoScrollPosition directly, which
        /// works whether or not a bar is showing. It never went through the wheel path, so it
        /// passed on a page nobody could actually scroll. Setting a property is not the same
        /// as using the control.
        /// </summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int extent = AutoScrollMinSize.Height - ClientSize.Height;

            if (AutoScroll && extent > 0)
            {
                // Three lines per notch, matching Windows' own default.
                int lines = SystemInformation.MouseWheelScrollLines;
                if (lines <= 0) lines = 3;

                int step = e.Delta * lines * 6 / 120;
                int target = Math.Clamp(-AutoScrollPosition.Y - step, 0, extent);

                AutoScrollPosition = new Point(-AutoScrollPosition.X, target);

                if (e is HandledMouseEventArgs handled) handled.Handled = true;
            }
            else
            {
                base.OnMouseWheel(e);
            }

            Invalidate(true);
        }

        /// <summary>Test seam: OnMouseWheel is protected and a unit test has no message loop.</summary>
        internal void TestScrollWheel(int delta) =>
            OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 0, 0, delta));

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // Keyboard scrolling, and anything that moves the view without going through the
            // two overrides above, arrives here as a scroll message.
            const int WM_HSCROLL = 0x0114;
            const int WM_VSCROLL = 0x0115;
            if (m.Msg is WM_HSCROLL or WM_VSCROLL) Invalidate(true);

            // Windows re-shows the scrollbars whenever it recalculates the non-client area,
            // so they have to be hidden again after it does.
            const int WM_NCCALCSIZE = 0x0083;
            if (m.Msg is WM_NCCALCSIZE or WM_HSCROLL or WM_VSCROLL) HideNativeScrollBars();
        }

        // ---- scrollbars ------------------------------------------------------------------
        //
        // AutoScroll brings Windows' own scrollbars with it, and they are the one part of the
        // app that cannot be themed: a flat grey-and-white bar down the side of a dark glass
        // panel, plus a horizontal one that had no business being there at all.
        //
        // The scrolling itself is kept - wheel, keyboard, AutoScrollPosition all still work -
        // and only the bars are hidden. Hiding them also hands their width back to the page,
        // which is why the content no longer sits inside a reserved gutter.

        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        private const int SB_BOTH = 3;

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            HideNativeScrollBars();
        }

        private void HideNativeScrollBars()
        {
            if (!IsHandleCreated || IsDisposed) return;
            ShowScrollBar(Handle, SB_BOTH, false);
        }

        /// <summary>
        /// Declare a scroll extent that actually covers the page's contents.
        ///
        /// AutoScroll on its own is not enough: without an explicit AutoScrollMinSize a page
        /// laid out at absolute coordinates reports nothing to scroll, so everything below the
        /// window's height is simply unreachable. Settings and FPS Tweaks both shipped that
        /// way - roughly 200px of each was impossible to reach on a default-sized window, and
        /// nobody noticed because you have to look for what is missing.
        ///
        /// Computed from where the children actually end rather than a hand-maintained
        /// number, so adding a card cannot silently put it out of reach again.
        /// </summary>
        protected void FitScrollToContent(int bottomPadding = 40)
        {
            int lowest = 0;
            foreach (Control child in Controls)
                if (child.Visible) lowest = Math.Max(lowest, child.Bottom);

            if (lowest <= 0) return;
            AutoScrollMinSize = new Size(0, lowest + bottomPadding);
        }

        // ---- centred content column ------------------------------------------------------
        //
        // Every page lays itself out with absolute coordinates against a fixed card width.
        // That was fine while the window was welded at 1040px. Now that it resizes, a wide
        // window left the content hugging the left edge with a large dead zone beside it.
        //
        // The fix is to centre the column rather than stretch it. Stretching would be worse:
        // a 1600px-wide settings row with its toggle far off on the right is harder to use
        // than a readable column, which is why almost every desktop app caps its content
        // width. Pages opt in by setting ContentWidth.
        //
        // Design-time positions are captured once, so repeated resizes always offset from the
        // original layout rather than compounding.

        private int _contentWidth;
        private Dictionary<Control, int>? _designLeft;

        /// <summary>The page's natural content width. Set it and the page centres itself in
        /// anything wider. Zero (the default) leaves layout untouched.</summary>
        protected int ContentWidth
        {
            get => _contentWidth;
            set
            {
                _contentWidth = value;
                CentreContent();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CentreContent();
        }

        private void CentreContent()
        {
            if (_contentWidth <= 0 || Width <= 0) return;

            // Captured on first use, after the page has finished building itself.
            _designLeft ??= Controls.Cast<Control>().ToDictionary(c => c, c => c.Left);

            // The native scrollbars are hidden, so their width is the page's to use.
            int offset = Math.Max(0, (Width - _contentWidth) / 2);

            foreach (Control child in Controls)
            {
                if (!_designLeft.TryGetValue(child, out int design))
                {
                    // Added after the first layout - adopt where it is now as its design
                    // position, or it would be shifted twice.
                    design = child.Left - CurrentOffset();
                    _designLeft[child] = design;
                }
                child.Left = design + offset;
            }

            _lastOffset = offset;
        }

        private int _lastOffset;
        private int CurrentOffset() => _lastOffset;

        // Pages used to fade up from the background when swapped in - a ~200ms scrim over the
        // whole page. Taken out on request: switching tabs should feel instant, and a fade
        // that plays every time you touch the nav reads as the app being slow rather than as
        // polish. Tabs now appear immediately, which is also what every other Windows app does.

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var back = new SolidBrush(Theme.Background))
                e.Graphics.FillRectangle(back, ClientRectangle);

            // The user's image (if any) sits under the plexus, drawn as this page's slice
            // of one window-sized picture so it runs continuously behind the whole app.
            Theming.AppBackground.Paint(e.Graphics, FieldOffset.X, FieldOffset.Y);

            Field?.Paint(e.Graphics, FieldOffset.X, FieldOffset.Y);
            base.OnPaint(e);
        }
    }
}
