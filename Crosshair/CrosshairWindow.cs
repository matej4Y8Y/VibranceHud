using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VibranceHud.Crosshair
{
    /// <summary>
    /// The on-screen crosshair: a layered, click-through, always-on-top window.
    ///
    /// It never touches the game process - no injection, no hooks, no memory access. It is
    /// a window sitting on top, the same category as the Discord overlay, which is why it
    /// stays on the right side of anti-cheat (Facepunch permits third-party crosshairs).
    ///
    /// Drawn with UpdateLayeredWindow and a 32-bit ARGB bitmap rather than a colour key,
    /// so edges are properly anti-aliased instead of fringed with the key colour.
    /// </summary>
    public sealed class CrosshairWindow : Form
    {
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;  // clicks pass through
        private const int WS_EX_TOOLWINDOW = 0x00000080;   // keeps it out of Alt-Tab
        private const int WS_EX_NOACTIVATE = 0x08000000;   // never steals focus
        private const int WS_EX_TOPMOST = 0x00000008;

        private const int ULW_ALPHA = 0x02;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc,
            int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr ho);

        private CrosshairConfig _config = new();

        public CrosshairWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            // A layered window's visible pixels come entirely from UpdateLayeredWindow;
            // the normal WM_PAINT path must never run, or a stray frame of the Form's
            // default (light grey/white) background can flash through before the first
            // Redraw() overwrites it.
            SetStyle(ControlStyles.Opaque, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { /* layered window only */ }
        protected override void OnPaint(PaintEventArgs e) { /* layered window only */ }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT
                            | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
                return cp;
            }
        }

        /// <summary>Never take focus when shown - the game must keep it.</summary>
        protected override bool ShowWithoutActivation => true;

        public void Apply(CrosshairConfig config)
        {
            _config = config;
            Redraw();
        }

        public void Redraw()
        {
            if (!IsHandleCreated) return;

            var shapes = CrosshairGeometry.Build(_config);
            if (shapes.Bounds.IsEmpty) return;

            // Pad so the outline and anti-aliasing have room at the edges.
            int pad = 4;
            int w = (int)Math.Ceiling(shapes.Bounds.Width) + pad * 2;
            int h = (int)Math.Ceiling(shapes.Bounds.Height) + pad * 2;

            var screen = Screen.FromPoint(Cursor.Position).Bounds;
            int left = screen.Left + (screen.Width - w) / 2;
            int top = screen.Top + (screen.Height - h) / 2;

            // Format32bppPArgb (premultiplied), not Format32bppArgb: UpdateLayeredWindow's
            // AC_SRC_ALPHA blend expects premultiplied colour, and GDI+ only writes that
            // correctly when the destination surface itself is premultiplied.
            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TranslateTransform(w / 2f, h / 2f); // geometry is built around the origin

                var colour = Color.FromArgb(_config.ColourArgb);
                using var fill = new SolidBrush(colour);
                using var outline = new Pen(Color.FromArgb(190, 0, 0, 0), 1f);

                foreach (var bar in shapes.Bars)
                {
                    g.FillRectangle(fill, bar);
                    if (_config.Outline) g.DrawRectangle(outline, bar.X, bar.Y, bar.Width, bar.Height);
                }

                if (shapes.Circle is { } c)
                {
                    using var ring = new Pen(colour, Math.Max(1, _config.Thickness));
                    g.DrawEllipse(ring, c);
                    if (_config.Outline)
                    {
                        float o = Math.Max(1, _config.Thickness) / 2f;
                        g.DrawEllipse(outline, c.X - o, c.Y - o, c.Width + o * 2, c.Height + o * 2);
                        g.DrawEllipse(outline, c.X + o, c.Y + o, c.Width - o * 2, c.Height - o * 2);
                    }
                }
            }

            Push(bmp, left, top);
        }

        /// <summary>Hand the ARGB bitmap to Windows as this window's contents.</summary>
        private void Push(Bitmap bmp, int left, int top)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero, old = IntPtr.Zero;

            try
            {
                // GetHbitmap(Color) is the wrong overload here: it discards alpha and
                // flattens every transparent pixel into an OPAQUE fill of that colour -
                // which turned the whole transparent canvas into a solid black rectangle
                // the size of the crosshair's bounds. The parameterless overload keeps
                // per-pixel alpha, which is what a layered window needs.
                hBitmap = bmp.GetHbitmap();
                old = SelectObject(memDc, hBitmap);

                var size = new SIZE { cx = bmp.Width, cy = bmp.Height };
                var src = new POINT { X = 0, Y = 0 };
                var dst = new POINT { X = left, Y = top };
                var blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA
                };

                UpdateLayeredWindow(Handle, screenDc, ref dst, ref size,
                    memDc, ref src, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    SelectObject(memDc, old);
                    DeleteObject(hBitmap);
                }
                DeleteDC(memDc);
            }
        }
    }
}
