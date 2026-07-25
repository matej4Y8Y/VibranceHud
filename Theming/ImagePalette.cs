using System;
using System.Drawing;

namespace VibranceHud.Theming
{
    /// <summary>
    /// A whole theme derived from an image: the accent and plexus colours, plus a shell
    /// (background, cards, borders) tinted toward the picture so the app reads as one
    /// piece with it rather than a black box sitting on top.
    ///
    /// Text colours are deliberately NOT derived - they stay the fixed near-white, and
    /// the shell is held dark, which is what guarantees the UI stays readable whatever
    /// gets dropped in.
    /// </summary>
    public sealed record ImageTheme(
        Color Accent, Color AccentDim, Color NodeA, Color NodeB, Color Line,
        Color Background, Color Surface, Color SurfaceHover, Color Border,
        Color GlassFill, Color GlassEdge,
        int SuggestedDim);

    /// <summary>
    /// Derives an accent from an image's dominant colour.
    ///
    /// "Dominant" cannot be taken literally: the most frequent pixel in a night scene is
    /// near-black and in a snow scene near-white, and either would be invisible as an
    /// accent on the matte-black UI. So pixels that cannot serve as an accent - too dark
    /// or too grey - are excluded before the vote, and the winner is lifted if it still
    /// reads too dark. An image with no usable colour at all falls back rather than
    /// emitting an accent the user can't see.
    ///
    /// Pure maths over a pixel array: no file I/O, no GDI, so every rule is unit-tested.
    /// </summary>
    public static class ImagePalette
    {
        /// <summary>Below this value a pixel is too dark to vote.</summary>
        public const float MinValue = 0.25f;
        /// <summary>Below this saturation a pixel is too grey to vote.</summary>
        public const float MinSaturation = 0.20f;
        /// <summary>The winning colour is lifted until it is at least this bright.</summary>
        public const float MinAccentBrightness = 0.45f;

        public const int MinDim = 0;
        public const int MaxDim = 80;

        private const int HueBuckets = 24; // 15 degrees each - all the oranges vote together

        public static ImageTheme Extract(Color[] pixels, Color fallbackAccent)
        {
            var counts = new int[HueBuckets];
            var sumS = new float[HueBuckets];
            var sumV = new float[HueBuckets];
            float sumLuma = 0f;

            foreach (var p in pixels)
            {
                ToHsv(p, out float h, out float s, out float v);
                sumLuma += (0.2126f * p.R + 0.7152f * p.G + 0.0722f * p.B) / 255f;

                if (v < MinValue || s < MinSaturation) continue; // can't be an accent
                int b = Math.Min(HueBuckets - 1, (int)(h / 360f * HueBuckets));
                counts[b]++;
                sumS[b] += s;
                sumV[b] += v;
            }

            int best = -1;
            for (int i = 0; i < HueBuckets; i++)
                if (counts[i] > 0 && (best < 0 || counts[i] > counts[best])) best = i;

            int dim = SuggestedDim(pixels.Length == 0 ? 0f : sumLuma / pixels.Length);

            if (pixels.Length == 0) return Derive(fallbackAccent, dim);

            Color accent;
            if (best < 0)
            {
                // A black-and-white picture has no colour to borrow. Falling back to the
                // app's default violet would clash with it badly, so go silver: a mono
                // image gets a mono theme.
                accent = FromHsv(0f, 0f, 0.86f);
            }
            else
            {
                float hue = (best + 0.5f) * (360f / HueBuckets);
                accent = Lift(FromHsv(hue, sumS[best] / counts[best], sumV[best] / counts[best]));
            }

            return Derive(accent, dim);
        }

        /// <summary>
        /// Build the full set from a known accent. Split out so a saved theme can be
        /// rebuilt at startup from the cached accent without re-reading the image, and so
        /// the derivation itself is testable on its own.
        ///
        /// The shape mirrors the built-in palettes: one accent, a darker variant, and two
        /// related plexus node colours a short hue apart (Violet is violet + magenta).
        /// </summary>
        public static ImageTheme Derive(Color accent, int dim)
        {
            var nodeB = ShiftHue(accent, 40f);
            ToHsv(accent, out float h, out float s, out _);

            // The shell borrows the image's hue but almost none of its saturation, and is
            // pinned to fixed dark values. That's what stops a yellow or white picture
            // producing a shell the fixed near-white text can't sit on.
            float tint = Math.Min(s, 0.65f);

            return new ImageTheme(
                Accent: accent,
                AccentDim: Scale(accent, 0.60f),
                NodeA: accent,
                NodeB: nodeB,
                Line: Blend(accent, nodeB, 0.5f),
                Background: FromHsv(h, tint * 0.34f, 0.055f),
                Surface: FromHsv(h, tint * 0.30f, 0.130f),
                SurfaceHover: FromHsv(h, tint * 0.30f, 0.190f),
                Border: FromHsv(h, tint * 0.28f, 0.265f),
                GlassFill: FromHsv(h, tint * 0.45f, 0.050f),
                GlassEdge: FromHsv(h, tint * 0.16f, 0.600f),
                SuggestedDim: dim);
        }

        /// <summary>Bright wallpapers have to be dimmed harder or they swamp the UI.</summary>
        public static int SuggestedDim(float meanLuma)
            => Math.Clamp((int)Math.Round(meanLuma * 85f), MinDim, MaxDim);

        /// <summary>Raise a too-dark colour until it reads against the matte-black base.</summary>
        private static Color Lift(Color c)
        {
            ToHsv(c, out float h, out float s, out float v);
            for (int i = 0; i < 24 && Luma(FromHsv(h, s, v)) < MinAccentBrightness && v < 1f; i++)
                v = Math.Min(1f, v + 0.05f);
            return FromHsv(h, s, v);
        }

        private static float Luma(Color c)
            => (0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B) / 255f;

        private static Color Scale(Color c, float factor)
            => Color.FromArgb(
                (int)Math.Clamp(c.R * factor, 0, 255),
                (int)Math.Clamp(c.G * factor, 0, 255),
                (int)Math.Clamp(c.B * factor, 0, 255));

        private static Color Blend(Color a, Color b, float t)
            => Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));

        private static Color ShiftHue(Color c, float degrees)
        {
            ToHsv(c, out float h, out float s, out float v);
            return FromHsv((h + degrees) % 360f, s, v);
        }

        // System.Drawing's GetBrightness is HSL lightness, not HSV value, so these are
        // written out rather than borrowed.
        private static void ToHsv(Color c, out float h, out float s, out float v)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float d = max - min;

            v = max;
            s = max <= 0f ? 0f : d / max;

            if (d <= 0f) h = 0f;
            else if (max == r) h = 60f * (((g - b) / d + 6f) % 6f);
            else if (max == g) h = 60f * ((b - r) / d + 2f);
            else h = 60f * ((r - g) / d + 4f);
        }

        private static Color FromHsv(float h, float s, float v)
        {
            float c = v * s;
            float x = c * (1f - Math.Abs((h / 60f % 2f) - 1f));
            float m = v - c;

            float r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromArgb(
                (int)Math.Round((r + m) * 255f),
                (int)Math.Round((g + m) * 255f),
                (int)Math.Round((b + m) * 255f));
        }
    }
}
