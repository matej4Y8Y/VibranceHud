using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// A rounded matte "card" surface used to group content on the pages. Children should sit
    /// on it with <c>BackColor = Color.Transparent</c>.
    ///
    /// The glass is rendered once into a bitmap and blitted after that. It looks identical -
    /// the point is what it costs. Transparent children ask their parent to paint their
    /// background, so every repaint of every label, slider and chip on this card used to
    /// rebuild a rounded <see cref="System.Drawing.Drawing2D.GraphicsPath"/> the full size of
    /// the card and fill and stroke it. During a slider drag that happened over a hundred
    /// times a second, for a picture that had not changed since the window was sized.
    /// </summary>
    public sealed class CardPanel : Panel
    {
        private const int Radius = 12;

        private Bitmap? _glass;
        private Size _glassSize;
        private string _glassTheme = "";

        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var glass = EnsureGlass();
            // Honours the clip region, so a child repainting a 30px strip copies a 30px strip.
            if (glass != null) e.Graphics.DrawImageUnscaled(glass, 0, 0);
            base.OnPaint(e);
        }

        /// <summary>Rebuild the cached glass when the size or the palette changes, and only
        /// then.</summary>
        private Bitmap? EnsureGlass()
        {
            if (Width < 2 || Height < 2) return null;

            if (_glass != null && _glassSize == Size && _glassTheme == Theme.CurrentName)
                return _glass;

            _glass?.Dispose();
            _glass = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            _glassSize = Size;
            _glassTheme = Theme.CurrentName;

            using (var g = Graphics.FromImage(_glass))
            {
                // Transparent everywhere the rounded shape isn't, so the page's backdrop
                // still shows past the card's corners.
                g.Clear(Color.Transparent);
                Glass.PaintPanel(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Radius);
            }

            return _glass;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _glass?.Dispose();
                _glass = null;
            }
            base.Dispose(disposing);
        }
    }
}
