using System;
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
        /// <summary>
        /// A rounded rectangle, safe for any radius.
        ///
        /// AddArc throws ArgumentException - surfaced to the user as "Parameter is not valid"
        /// - when the arc's diameter is zero, and it produces a corrupt path when the radius
        /// is larger than the rectangle it is rounding. Both were reachable: a square-cornered
        /// caller passing 0 crashed the app on paint, and because it threw from inside OnPaint
        /// the control was left unpainted, so the crash arrived alongside a blank white box
        /// where the control should have been.
        ///
        /// A zero radius is a legitimate request for square corners, and a radius larger than
        /// the box is a legitimate request for a pill.
        /// </summary>
        public static GraphicsPath RoundedPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();

            if (rect.Width <= 0 || rect.Height <= 0) return path;

            // Never wider than the box can take, and never negative.
            radius = Math.Max(0f, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f));

            if (radius <= 0.01f)
            {
                path.AddRectangle(rect);
                return path;
            }

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
