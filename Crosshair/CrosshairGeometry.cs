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

            // Each part is independent, so any combination is reachable - a cross with a dot,
            // a circle with a dot, a T with a circle round it. The old model could express
            // exactly four crosshairs; this one can express the lot.
            if (c.ResolvedArmLeft) bars.Add(new RectangleF(-(gap + size), -half, size, t));
            if (c.ResolvedArmRight) bars.Add(new RectangleF(gap, -half, size, t));
            if (c.ResolvedArmBottom) bars.Add(new RectangleF(-half, gap, t, size));
            if (c.ResolvedArmTop) bars.Add(new RectangleF(-half, -(gap + size), t, size));

            if (c.ResolvedShowCircle)
            {
                float r = Math.Max(0.5f, c.ResolvedCircleRadius);
                circle = new RectangleF(-r, -r, r * 2, r * 2);
            }

            if (c.ResolvedCentreDot)
            {
                // Its own size, so a small crosshair can carry a fat dot and the other way
                // round. Falls back to the arm thickness, which is what the old model drew.
                float d = Math.Max(0.5f, c.ResolvedDotSize);
                bars.Add(new RectangleF(-d / 2f, -d / 2f, d, d));
            }

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
