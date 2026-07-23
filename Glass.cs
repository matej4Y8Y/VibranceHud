using System.Drawing;
using System.Drawing.Drawing2D;

namespace VibranceHud
{
    /// <summary>
    /// Shared "matte glass" painting: a rounded, translucent matte-black panel with a soft
    /// grey rounded edge. Translucent so the plexus shows faintly through it; matte (no
    /// white gloss) so it reads as dark frosted glass, not shiny plastic.
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
            int fillAlpha = 140, int rimAlpha = 105)
        {
            if (rect.Width < 2 || rect.Height < 2) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var path = RoundedPath(rect, radius);
            using (var fill = new SolidBrush(Color.FromArgb(fillAlpha, Theme.GlassFill)))
                g.FillPath(fill, path);
            using (var rim = new Pen(Color.FromArgb(rimAlpha, Theme.GlassEdge), 1.2f))
                g.DrawPath(rim, path);
        }

        /// <summary>Accent-tinted fill for a selected/active pill (keeps the purple pop).</summary>
        public static void PaintAccent(Graphics g, RectangleF rect, float radius, Color accent, int alpha = 210)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedPath(rect, radius);
            using (var fill = new SolidBrush(Color.FromArgb(alpha, accent)))
                g.FillPath(fill, path);
            using (var rim = new Pen(Color.FromArgb(120, Theme.GlassEdge), 1.2f))
                g.DrawPath(rim, path);
        }
    }
}
