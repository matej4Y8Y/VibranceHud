using System;
using System.Collections.Generic;
using System.Drawing;

namespace VibranceHud.Crosshair
{
    /// <summary>The shapes to draw, all positioned around the origin.</summary>
    public sealed record CrosshairShapes(
        IReadOnlyList<RectangleF> Bars, RectangleF? Circle, RectangleF Bounds);

    /// <summary>
    /// Turns a config into the rectangles to draw. Pure maths built around the origin, so
    /// the overlay window only has to translate to the centre of the screen.
    ///
    /// Kept away from any window on purpose: a crosshair with one arm a pixel longer than
    /// its opposite, or sitting half a pixel off centre, is invisible in code review and
    /// very obvious when you're trying to aim with it.
    /// </summary>
    public static class CrosshairGeometry
    {
        public static CrosshairShapes Build(CrosshairConfig c)
        {
            // Resolved, not the legacy whole-pixel fields: these carry tenths of a pixel, so
            // a crosshair set to 3.4 thick draws at 3.4 rather than snapping to 3.
            float t = Math.Max(0.5f, c.ResolvedThickness);
            float size = Math.Max(0.5f, c.ResolvedSize);
            float gap = Math.Max(0, c.ResolvedGap);
            float half = t / 2f;   // everything is centred on the origin, so arms straddle it

            var bars = new List<RectangleF>();
            RectangleF? circle = null;

            switch (c.Shape)
            {
                case CrosshairShape.Dot:
                    // Already only a dot - the centre-dot toggle must not double it up.
                    bars.Add(new RectangleF(-half, -half, t, t));
                    break;

                case CrosshairShape.Circle:
                    float r = gap + size;
                    circle = new RectangleF(-r, -r, r * 2, r * 2);
                    break;

                case CrosshairShape.Cross:
                case CrosshairShape.T:
                    bars.Add(new RectangleF(-(gap + size), -half, size, t)); // left
                    bars.Add(new RectangleF(gap, -half, size, t));           // right
                    bars.Add(new RectangleF(-half, gap, t, size));           // below
                    if (c.Shape == CrosshairShape.Cross)
                        bars.Add(new RectangleF(-half, -(gap + size), t, size)); // above
                    break;
            }

            if (c.CentreDot && c.Shape != CrosshairShape.Dot)
                bars.Add(new RectangleF(-half, -half, t, t));

            return new CrosshairShapes(bars, circle, Bounds(bars, circle));
        }

        private static RectangleF Bounds(IReadOnlyList<RectangleF> bars, RectangleF? circle)
        {
            if (bars.Count == 0 && circle == null) return RectangleF.Empty;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            void Take(RectangleF r)
            {
                minX = Math.Min(minX, r.X); minY = Math.Min(minY, r.Y);
                maxX = Math.Max(maxX, r.Right); maxY = Math.Max(maxY, r.Bottom);
            }

            foreach (var b in bars) Take(b);
            if (circle != null) Take(circle.Value);

            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
