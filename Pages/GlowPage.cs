using System.Drawing;
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

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Invalidate(true);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // Keyboard scrolling, and anything that moves the view without going through the
            // two overrides above, arrives here as a scroll message.
            const int WM_HSCROLL = 0x0114;
            const int WM_VSCROLL = 0x0115;
            if (m.Msg is WM_HSCROLL or WM_VSCROLL) Invalidate(true);
        }

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
