using System.Collections.Generic;
using System.Linq;

namespace VibranceHud.Crosshair
{
    /// <summary>
    /// The ready-made crosshairs, as a browsable library.
    ///
    /// Thirty, built from the shapes competitive players actually run: thin and thick crosses
    /// at various gaps, crosses with a centre dot, bare dots, T-shapes, circles, and the small
    /// "pixel" crosshairs people use when they want the least screen covered.
    ///
    /// **Not** a copy of any commercial crosshair overlay's preset list. That product's usage
    /// data is not something this project can see, and shipping thirty entries under a claim
    /// of "their top thirty" would be inventing a fact - the same overclaim this codebase has
    /// already had to unpick once over screen capture. The shapes themselves are universal
    /// across every shooter; only the names would have been borrowed, and those are theirs.
    ///
    /// Every entry is white. Picking a crosshair hands over a shape, not a look - colour is
    /// the most personal setting on the page and the one most likely to be tuned to a
    /// particular game's background.
    /// </summary>
    public static class CrosshairGallery
    {
        /// <summary>Broad families, so the grid can be grouped and a user can find "the dot
        /// ones" without reading thirty names.</summary>
        public enum Family { Cross, CrossDot, Dot, TShape, Circle, Pixel }

        public sealed record GalleryItem(
            string Id,
            string Name,
            Family Group,
            CrosshairConfig Config);

        private static CrosshairConfig Make(
            int sizeTenths, int thicknessTenths, int gapTenths,
            bool top = true, bool bottom = true, bool left = true, bool right = true,
            bool dot = false, int? dotTenths = null,
            bool circle = false, int? circleTenths = null,
            bool outline = true)
        {
            var c = new CrosshairConfig
            {
                ColourArgb = CrosshairPresets.White,
                Outline = outline,
                Opacity = 100,
                ArmTop = top,
                ArmBottom = bottom,
                ArmLeft = left,
                ArmRight = right,
                CentreDot = dot,
                DotSizeTenths = dotTenths,
                ShowCircle = circle,
                CircleRadiusTenths = circleTenths,
            };

            c.SetSizeTenths(sizeTenths);
            c.SetThicknessTenths(thicknessTenths);
            c.SetGapTenths(gapTenths);
            return c;
        }

        public static IReadOnlyList<GalleryItem> All { get; } = new[]
        {
            // ---- plain crosses: the bulk of what people actually use ----
            new GalleryItem("cross-thin-tight",   "Thin Tight",     Family.Cross,    Make(50, 10, 15)),
            new GalleryItem("cross-thin",         "Thin",           Family.Cross,    Make(70, 10, 30)),
            new GalleryItem("cross-thin-wide",    "Thin Wide",      Family.Cross,    Make(90, 10, 60)),
            new GalleryItem("cross-classic",      "Classic",        Family.Cross,    Make(80, 20, 40)),
            new GalleryItem("cross-short",        "Short",          Family.Cross,    Make(45, 20, 25)),
            new GalleryItem("cross-long",         "Long",           Family.Cross,    Make(140, 20, 45)),
            new GalleryItem("cross-thick",        "Thick",          Family.Cross,    Make(70, 35, 35)),
            new GalleryItem("cross-thick-tight",  "Thick Tight",    Family.Cross,    Make(55, 35, 15)),
            new GalleryItem("cross-heavy",        "Heavy",          Family.Cross,    Make(60, 50, 30)),
            new GalleryItem("cross-nogap",        "Closed",         Family.Cross,    Make(70, 20, 0)),

            // ---- cross plus a centre dot ----
            new GalleryItem("dotcross-classic",   "Classic + Dot",  Family.CrossDot, Make(70, 18, 45, dot: true)),
            new GalleryItem("dotcross-thin",      "Thin + Dot",     Family.CrossDot, Make(80, 10, 50, dot: true)),
            new GalleryItem("dotcross-wide",      "Wide + Dot",     Family.CrossDot, Make(90, 20, 70, dot: true)),
            new GalleryItem("dotcross-bigdot",    "Cross + Big Dot",Family.CrossDot, Make(70, 15, 45, dot: true, dotTenths: 35)),
            new GalleryItem("dotcross-short",     "Short + Dot",    Family.CrossDot, Make(40, 15, 30, dot: true)),

            // ---- dots ----
            new GalleryItem("dot-tiny",           "Tiny Dot",       Family.Dot,      Make(10, 10, 0, false, false, false, false, dot: true, dotTenths: 10)),
            new GalleryItem("dot-small",          "Small Dot",      Family.Dot,      Make(10, 10, 0, false, false, false, false, dot: true, dotTenths: 20)),
            new GalleryItem("dot-medium",         "Dot",            Family.Dot,      Make(10, 10, 0, false, false, false, false, dot: true, dotTenths: 30)),
            new GalleryItem("dot-large",          "Big Dot",        Family.Dot,      Make(10, 10, 0, false, false, false, false, dot: true, dotTenths: 45)),

            // ---- T-shapes: no top arm, so the target is never covered ----
            new GalleryItem("t-classic",          "T",              Family.TShape,   Make(80, 20, 40, top: false)),
            new GalleryItem("t-thin",             "Thin T",         Family.TShape,   Make(90, 10, 45, top: false)),
            new GalleryItem("t-thick",            "Thick T",        Family.TShape,   Make(65, 32, 30, top: false)),
            new GalleryItem("t-dot",              "T + Dot",        Family.TShape,   Make(75, 18, 45, top: false, dot: true)),

            // ---- circles ----
            new GalleryItem("circle-thin",        "Ring",           Family.Circle,   Make(10, 12, 0, false, false, false, false, circle: true, circleTenths: 70)),
            new GalleryItem("circle-thick",       "Thick Ring",     Family.Circle,   Make(10, 25, 0, false, false, false, false, circle: true, circleTenths: 70)),
            new GalleryItem("circle-dot",         "Ring + Dot",     Family.Circle,   Make(10, 12, 0, false, false, false, false, dot: true, dotTenths: 20, circle: true, circleTenths: 75)),
            new GalleryItem("circle-cross",       "Ring + Cross",   Family.Circle,   Make(45, 15, 55, circle: true, circleTenths: 100)),

            // ---- pixel: the least screen covered ----
            new GalleryItem("pixel-cross",        "Pixel Cross",    Family.Pixel,    Make(25, 10, 10)),
            new GalleryItem("pixel-plus",         "Pixel Plus",     Family.Pixel,    Make(20, 10, 0)),
            new GalleryItem("pixel-dot-cross",    "Pixel + Dot",    Family.Pixel,    Make(25, 10, 15, dot: true, dotTenths: 10)),
        };

        /// <summary>Apply a gallery entry, keeping the user's colour, opacity and outline.</summary>
        public static void Apply(CrosshairConfig target, GalleryItem item)
        {
            var s = item.Config;

            target.ArmTop = s.ResolvedArmTop;
            target.ArmBottom = s.ResolvedArmBottom;
            target.ArmLeft = s.ResolvedArmLeft;
            target.ArmRight = s.ResolvedArmRight;
            target.CentreDot = s.ResolvedCentreDot;
            target.DotSizeTenths = s.DotSizeTenths;
            target.ShowCircle = s.ResolvedShowCircle;
            target.CircleRadiusTenths = s.CircleRadiusTenths;

            target.SetSizeTenths(s.SizeTenths ?? 80);
            target.SetThicknessTenths(s.ThicknessTenths ?? 20);
            target.SetGapTenths(s.GapTenths ?? 40);

            // Shape is legacy, but keep it roughly honest so a downgrade lands somewhere sane.
            target.Shape = s.ResolvedShowCircle && !s.ResolvedArmLeft ? CrosshairShape.Circle
                : !s.ResolvedArmLeft && !s.ResolvedArmRight ? CrosshairShape.Dot
                : !s.ResolvedArmTop ? CrosshairShape.T
                : CrosshairShape.Cross;
        }

        /// <summary>Which entry the current crosshair matches, or null once the user has
        /// nudged a slider off all of them. Compares shape only - a red Classic is still a
        /// Classic.</summary>
        public static GalleryItem? Matching(CrosshairConfig c) =>
            All.FirstOrDefault(item =>
                item.Config.ResolvedArmTop == c.ResolvedArmTop &&
                item.Config.ResolvedArmBottom == c.ResolvedArmBottom &&
                item.Config.ResolvedArmLeft == c.ResolvedArmLeft &&
                item.Config.ResolvedArmRight == c.ResolvedArmRight &&
                item.Config.ResolvedCentreDot == c.ResolvedCentreDot &&
                item.Config.ResolvedShowCircle == c.ResolvedShowCircle &&
                Near(item.Config.ResolvedSize, c.ResolvedSize) &&
                Near(item.Config.ResolvedThickness, c.ResolvedThickness) &&
                Near(item.Config.ResolvedGap, c.ResolvedGap) &&
                // Dot and circle sizes too. Without them the four dot entries - identical
                // except for how big the dot is - all matched the first one, so picking
                // "Big Dot" highlighted "Tiny Dot".
                (!c.ResolvedCentreDot || Near(item.Config.ResolvedDotSize, c.ResolvedDotSize)) &&
                (!c.ResolvedShowCircle || Near(item.Config.ResolvedCircleRadius, c.ResolvedCircleRadius)));

        private static bool Near(float a, float b) => System.Math.Abs(a - b) < 0.05f;
    }
}
