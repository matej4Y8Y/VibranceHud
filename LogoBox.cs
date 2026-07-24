using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// Draws a logo image scaled to fit its height (aspect preserved), left-aligned, over a
    /// transparent background so the plexus shows through the PNG's transparent areas. Owner
    /// drawn like the app's other controls so it repaints cleanly with the animation.
    /// </summary>
    public sealed class LogoBox : Control
    {
        private Image? _image;

        public Image? Image
        {
            get => _image;
            set { _image = value; Invalidate(); }
        }

        public LogoBox()
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
            if (_image == null) return;
            var g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            float aspect = (float)_image.Width / _image.Height;
            int h = Height;
            int w = (int)(h * aspect);
            g.DrawImage(_image, new Rectangle(0, 0, w, h));
        }
    }
}
