using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VibranceHud.Controls
{
    /// <summary>
    /// A scrolling panel with no Win32 scrollbar.
    ///
    /// The bar is the one part of a scrolling area that cannot be themed - a flat grey-and-
    /// white strip down the side of a dark glass card - and the crosshair gallery was showing
    /// one. <see cref="Pages.GlowPage"/> already solved this for whole pages; this is the same
    /// solution for an inner panel, in one place rather than two.
    ///
    /// Hiding the bar must not take the scrolling with it. ScrollableControl only acts on the
    /// wheel while its scrollbar is visible - it checks VScroll first - so the wheel is
    /// handled here instead, and every descendant forwards to it, because Windows delivers the
    /// wheel to whatever sits under the cursor and on a full gallery that is always a cell.
    /// </summary>
    public sealed class QuietScrollPanel : Panel
    {
        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        private const int SB_BOTH = 3;

        public QuietScrollPanel()
        {
            AutoScroll = true;
            BackColor = System.Drawing.Color.Transparent;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int extent = AutoScrollMinSize.Height - ClientSize.Height;

            if (AutoScroll && extent > 0)
            {
                int lines = SystemInformation.MouseWheelScrollLines;
                if (lines <= 0) lines = 3;

                int step = e.Delta * lines * 22 / 120;
                int target = Math.Clamp(-AutoScrollPosition.Y - step, 0, extent);

                AutoScrollPosition = new System.Drawing.Point(-AutoScrollPosition.X, target);

                if (e is HandledMouseEventArgs handled) handled.Handled = true;
            }
            else base.OnMouseWheel(e);

            Invalidate(true);
        }

        /// <summary>Test seam: OnMouseWheel is protected and a unit test has no message loop.</summary>
        internal void TestScrollWheel(int delta) =>
            OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 0, 0, delta));

        private void ForwardWheel(object? sender, MouseEventArgs e) => OnMouseWheel(e);

        private void Hook(Control root)
        {
            foreach (Control child in root.Controls)
            {
                child.MouseWheel -= ForwardWheel;
                child.MouseWheel += ForwardWheel;

                child.ControlAdded -= OnDescendantAdded;
                child.ControlAdded += OnDescendantAdded;

                Hook(child);
            }
        }

        private void OnDescendantAdded(object? sender, ControlEventArgs e)
        {
            e.Control.MouseWheel -= ForwardWheel;
            e.Control.MouseWheel += ForwardWheel;
            Hook(e.Control);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            OnDescendantAdded(this, e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Hook(this);
            HideBars();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            HideBars();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // Windows puts the bars back whenever it recalculates the non-client area, so they
            // have to be hidden again after it does.
            const int WM_NCCALCSIZE = 0x0083;
            const int WM_HSCROLL = 0x0114;
            const int WM_VSCROLL = 0x0115;

            if (m.Msg is WM_NCCALCSIZE or WM_HSCROLL or WM_VSCROLL) HideBars();
        }

        private void HideBars()
        {
            if (!IsHandleCreated || IsDisposed) return;
            ShowScrollBar(Handle, SB_BOTH, false);
        }
    }
}
