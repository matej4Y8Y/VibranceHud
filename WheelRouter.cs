using System;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// Sends the mouse wheel to whatever is under the pointer, rather than to whatever has
    /// focus.
    ///
    /// Windows delivers WM_MOUSEWHEEL to the FOCUSED window. WinForms does not re-route it, so
    /// after clicking a nav button - which takes focus - the wheel travelled up the navigation
    /// bar's parent chain and never reached the page at all. Scrolling only worked if you
    /// happened to click inside the page first, which is not something anybody would think to
    /// try; the page simply looked stuck.
    ///
    /// Every other Windows application scrolls what the pointer is over, so this is restoring
    /// the behaviour people already expect rather than inventing one.
    /// </summary>
    public sealed class WheelRouter : IMessageFilter
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(System.Drawing.Point point);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>Install for the process. Safe to call more than once.</summary>
        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            Application.AddMessageFilter(new WheelRouter());
        }

        private static bool _installed;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL) return false;

            try
            {
                // lParam holds the screen position the wheel happened at.
                int x = unchecked((short)(long)m.LParam);
                int y = unchecked((short)((long)m.LParam >> 16));

                IntPtr under = WindowFromPoint(new System.Drawing.Point(x, y));
                if (under == IntPtr.Zero || under == m.HWnd) return false;

                // Only re-route inside our own windows. A wheel over another application's
                // window is none of our business, and forwarding into one would be worse than
                // doing nothing.
                var control = Control.FromChildHandle(under);
                if (control == null || control.FindForm() == null) return false;

                SendMessage(under, WM_MOUSEWHEEL, m.WParam, m.LParam);
                return true;   // handled; do not also deliver it to the focused control
            }
            catch
            {
                // A window can go away between the hit test and the send. Falling through to
                // the default routing is always safe.
                return false;
            }
        }
    }
}
