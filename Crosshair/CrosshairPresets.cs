using System.Collections.Generic;

namespace VibranceHud.Crosshair
{
    /// <summary>One ready-made crosshair shape, and what to call it.</summary>
    public sealed record CrosshairPreset(
        string Name,
        CrosshairShape Shape,
        int SizeTenths,
        int ThicknessTenths,
        int GapTenths,
        bool CentreDot = false);

    /// <summary>
    /// The shapes people actually use, as one-click starting points.
    ///
    /// Every preset is WHITE, and applying one deliberately leaves colour and outline alone.
    /// Colour is the most personal setting on the page and the one most likely to be tuned to
    /// a particular game's background - throwing it away because somebody wanted a different
    /// shape would be the single most annoying thing this feature could do.
    ///
    /// Named descriptively rather than after any commercial crosshair overlay's own preset
    /// names. The shapes themselves are universal across every shooter; the names those
    /// products use are their branding, and borrowing them would be both unnecessary and
    /// somebody else's.
    /// </summary>
    public static class CrosshairPresets
    {
        /// <summary>The colour every preset starts at: plain white, maximum alpha.</summary>
        public const int White = unchecked((int)0xFFFFFFFF);

        public static IReadOnlyList<CrosshairPreset> All { get; } = new[]
        {
            new CrosshairPreset("Classic",   CrosshairShape.Cross, SizeTenths:  80, ThicknessTenths: 20, GapTenths: 40),
            new CrosshairPreset("Small",     CrosshairShape.Cross, SizeTenths:  40, ThicknessTenths: 15, GapTenths: 20),
            new CrosshairPreset("Wide",      CrosshairShape.Cross, SizeTenths: 140, ThicknessTenths: 25, GapTenths: 80),
            new CrosshairPreset("Precise",   CrosshairShape.Cross, SizeTenths:  55, ThicknessTenths: 10, GapTenths: 25),
            new CrosshairPreset("T-Shape",   CrosshairShape.T,     SizeTenths:  80, ThicknessTenths: 20, GapTenths: 40),
            new CrosshairPreset("Dot",       CrosshairShape.Dot,   SizeTenths:  20, ThicknessTenths: 20, GapTenths:  0),
            new CrosshairPreset("Cross+Dot", CrosshairShape.Cross, SizeTenths:  70, ThicknessTenths: 18, GapTenths: 45, CentreDot: true),
            new CrosshairPreset("Circle",    CrosshairShape.Circle,SizeTenths: 100, ThicknessTenths: 15, GapTenths:  0),
        };

        /// <summary>
        /// Apply a preset to a config in place.
        ///
        /// Shape, dimensions and the centre dot only. Colour, outline and the crosshair's name
        /// are the user's and are left exactly as they were.
        /// </summary>
        public static void Apply(CrosshairConfig config, CrosshairPreset preset)
        {
            config.Shape = preset.Shape;
            config.SetSizeTenths(preset.SizeTenths);
            config.SetThicknessTenths(preset.ThicknessTenths);
            config.SetGapTenths(preset.GapTenths);
            config.CentreDot = preset.CentreDot;
        }

        /// <summary>A standalone config for this preset, in white. Used to draw the chips, so
        /// each one previews itself rather than a shared placeholder.</summary>
        public static CrosshairConfig ToConfig(CrosshairPreset preset)
        {
            var config = new CrosshairConfig { Name = preset.Name, ColourArgb = White, Outline = true };
            Apply(config, preset);
            return config;
        }

        /// <summary>Which preset a config currently matches, or null when the user has moved
        /// off all of them. Compares shape and dimensions, not colour.</summary>
        public static CrosshairPreset? Matching(CrosshairConfig config)
        {
            foreach (var preset in All)
            {
                if (config.Shape != preset.Shape) continue;
                if (config.CentreDot != preset.CentreDot) continue;
                if (Tenths(config.ResolvedSize) != preset.SizeTenths) continue;
                if (Tenths(config.ResolvedThickness) != preset.ThicknessTenths) continue;
                if (Tenths(config.ResolvedGap) != preset.GapTenths) continue;
                return preset;
            }
            return null;
        }

        private static int Tenths(float pixels) => (int)System.Math.Round(pixels * 10);
    }
}
