using System.Drawing;
using System.Drawing.Drawing2D;

namespace VibranceHud
{
    /// <summary>
    /// Shared "frosted glass" painting: a rounded panel with a faint dark base (dims the
    /// plexus behind it for readability), a soft top-down light sheen, and a light rim -
    /// the iOS-style translucent-glass look, adapted for a dark theme over GDI.
    /// </summary>
    public static class Glass
    {
        public static GraphicsPath RoundedPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void PaintPanel(Graphics g, RectangleF rect, float radius,
            int baseAlpha = 70, int rimAlpha = 48, int sheenTop = 30)
        {
            if (rect.Width < 2 || rect.Height < 2) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var path = RoundedPath(rect, radius);

            using (var basefill = new SolidBrush(Color.FromArgb(baseAlpha, 14, 14, 20)))
                g.FillPath(basefill, path);

            using (var sheen = new LinearGradientBrush(
                       new RectangleF(rect.X, rect.Y, rect.Width, rect.Height),
                       Color.FromArgb(sheenTop, 255, 255, 255),
                       Color.FromArgb(4, 255, 255, 255),
                       LinearGradientMode.Vertical) { WrapMode = WrapMode.TileFlipXY })
                g.FillPath(sheen, path);

            using (var rim = new Pen(Color.FromArgb(rimAlpha, 255, 255, 255), 1f))
                g.DrawPath(rim, path);
        }

        /// <summary>An accent-tinted glass fill (for selected/active pills).</summary>
        public static void PaintAccent(Graphics g, RectangleF rect, float radius, Color accent, int alpha = 205)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedPath(rect, radius);
            using (var fill = new SolidBrush(Color.FromArgb(alpha, accent)))
                g.FillPath(fill, path);
            using (var rim = new Pen(Color.FromArgb(90, 255, 255, 255), 1f))
                g.DrawPath(rim, path);
        }
    }
}
