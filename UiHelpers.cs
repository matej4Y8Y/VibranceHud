using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// Lets a borderless window be dragged by its own body.
    ///
    /// PlexusX's secondary windows all use <see cref="FormBorderStyle.None"/> so they can
    /// paint their own rounded glass frame - which also removes the title bar Windows would
    /// normally give them to drag by. MainWindow wires this up by hand for its custom title
    /// bar; the popup, the onboarding screen and the What's New notice never did, so they sat
    /// wherever they opened and could not be moved off whatever they were covering. The
    /// always-on-top quick-vibrance popup is the one that hurts.
    /// </summary>
    internal static class WindowDrag
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        /// <summary>
        /// Left-dragging anywhere on <paramref name="source"/> that isn't covered by a child
        /// control moves <paramref name="form"/>.
        ///
        /// Behaviour only - deliberately no cursor change and no drawn handle. Those were
        /// tried and taken back out: on a small floating window they read as clutter, and
        /// the windows are supposed to look untouched.
        /// </summary>
        public static void Enable(Control source, Form form)
        {
            source.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(form.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            };
        }
    }

    /// <summary>Small shared builders so pages stay consistent and DRY.</summary>
    internal static class UiHelpers
    {
        /// <summary>
        /// The app's focus ring, so every control shows focus the same way.
        ///
        /// Three controls out of roughly twenty drew one before, which meant tabbing through
        /// the app was invisible: the focus existed, you just could not see where it was.
        /// That fails the first thing any accessibility check looks for, and it is also
        /// simply how a keyboard user finds their place.
        ///
        /// Inset by one pixel so it sits inside the control's own rim rather than on top of
        /// it, and dotted so it reads as focus rather than as a selected state - several of
        /// these controls already use a solid accent border to mean "active".
        /// </summary>
        public static void DrawFocusRing(System.Drawing.Graphics g,
            System.Drawing.Rectangle bounds, float radius)
        {
            var r = System.Drawing.Rectangle.Inflate(bounds, -2, -2);
            if (r.Width <= 0 || r.Height <= 0) return;

            using var pen = new System.Drawing.Pen(Theme.Accent, 1.4f)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dot,
            };
            using var path = Glass.RoundedPath(
                new System.Drawing.RectangleF(r.X, r.Y, r.Width, r.Height), radius);
            g.DrawPath(pen, path);
        }

        /// <summary>"PRESETS" -> "P R E S E T S" with wider gaps between words.</summary>
        public static string Spaced(string text) =>
            string.Join("   ", text.Split(' ').Select(w => string.Join(" ", w.ToCharArray())));

        public static Label Caption(string text, int x, int y, int width,
            ContentAlignment align = ContentAlignment.MiddleLeft) => new()
        {
            Text = Spaced(text),
            ForeColor = Theme.TextDim,
            Font = new Font(Theme.FontFamily, 8f, FontStyle.Bold),
            Location = new Point(x, y),
            Size = new Size(width, 16),
            TextAlign = align,
            BackColor = Color.Transparent
        };
    }
}
