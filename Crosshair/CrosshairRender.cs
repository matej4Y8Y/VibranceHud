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

            var colour = Color.FromArgb(config.ColourArgb);
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
                float t = Math.Max(1, config.Thickness);
                if (config.Outline)
                    using (var haloPen = new Pen(halo.Color, t + 2f))
                        g.DrawEllipse(haloPen, c);
                using var ring = new Pen(colour, t);
                g.DrawEllipse(ring, c);
            }
        }
    }
}
