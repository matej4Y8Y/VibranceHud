using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace VibranceHud.Crosshair
{
    /// <summary>
    /// Shared drawing for the overlay window and the settings preview, so what the card
    /// shows is exactly what lands on screen. Expects the Graphics to already be
    /// translated so the crosshair centre sits at the origin.
    /// </summary>
    public static class CrosshairRender
    {
        public static void Draw(Graphics g, CrosshairConfig config)
        {
            var shapes = CrosshairGeometry.Build(config);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Resolved, so the opacity slider is applied. Folding opacity into ColourArgb
            // instead would mean picking a new colour silently reset it.
            var colour = Color.FromArgb(config.ResolvedColourArgb);
            using var fill = new SolidBrush(colour);
            using var halo = new SolidBrush(Color.FromArgb(190, 0, 0, 0));

            foreach (var bar in shapes.Bars)
            {
                // The outline is a backing shape inflated behind the bar, NOT a stroked
                // pen around it: a 1px pen outline around a 1px bar covers the bar
                // entirely and the crosshair renders black no matter what colour is picked.
                if (config.Outline) g.FillRectangle(halo, RectangleF.Inflate(bar, 1, 1));
                g.FillRectangle(fill, bar);
            }

            if (shapes.Circle is { } c)
            {
                float t = Math.Max(0.5f, config.ResolvedThickness);
                if (config.Outline)
                    using (var haloPen = new Pen(halo.Color, t + 2f))
                        g.DrawEllipse(haloPen, c);
                using var ring = new Pen(colour, t);
                g.DrawEllipse(ring, c);
            }
        }

        /// <summary>Draw scaled and centred to fit inside <paramref name="target"/> - for
        /// small thumbnails (saved-chip previews) where a full-size crosshair, drawn at its
        /// real pixel dimensions, would overflow a tiny box.</summary>
        public static void Draw(Graphics g, CrosshairConfig config, Rectangle target)
        {
            var bounds = CrosshairGeometry.Build(config).Bounds;
            float scale = 1f;
            if (bounds.Width > 0 && bounds.Height > 0)
                scale = Math.Min(1f, 0.82f * Math.Min(target.Width / bounds.Width, target.Height / bounds.Height));

            var state = g.Save();
            g.SetClip(target);
            g.TranslateTransform(target.X + target.Width / 2f, target.Y + target.Height / 2f);
            g.ScaleTransform(scale, scale);
            Draw(g, config);
            g.Restore(state);
        }

        /// <summary>Checkerboard backdrop so both light and dark crosshair colours read
        /// clearly - shared between the full-size preview and the saved-chip thumbnails.</summary>
        public static void DrawCheckerboard(Graphics g, Rectangle rect, int cell)
        {
            var clip = g.Clip;
            g.SetClip(rect);
            using (var a = new SolidBrush(Color.FromArgb(255, 58, 58, 64)))
            using (var b = new SolidBrush(Color.FromArgb(255, 78, 78, 86)))
                for (int yy = rect.Top; yy < rect.Bottom; yy += cell)
                    for (int xx = rect.Left; xx < rect.Right; xx += cell)
                        g.FillRectangle(((xx - rect.Left) / cell + (yy - rect.Top) / cell) % 2 == 0 ? a : b,
                            xx, yy, cell, cell);
            g.Clip = clip;
        }
    }
}
