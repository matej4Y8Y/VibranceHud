using System;

namespace VibranceHud
{
    /// <summary>
    /// Builds the 3x256 display gamma ramp from every tone control at once.
    ///
    /// This is the whole reason advanced colour is possible without DX11. The colour matrix
    /// in <see cref="ColorAdjust"/> is linear, so it cannot express highlights, shadows,
    /// fade or a split tone no matter how it is arranged. The display's gamma ramp is a
    /// per-channel lookup table, so it can express any monotonic curve — and unlike the DX11
    /// path it already works on every GPU in the field.
    ///
    /// <see cref="GammaCurve"/> was doing one power function and copying it to all three
    /// channels, which is roughly five percent of what the hardware allows. This replaces
    /// that, and reduces to exactly the old curve when only gamma is set.
    ///
    /// Pure maths, unit-tested without a display.
    /// </summary>
    public static class ToneCurve
    {
        // How far each control can push, chosen so a slider at 100 is strong but still
        // usable rather than destroying the image.
        private const double EndpointRange = 0.25;   // whites / blacks
        private const double FadeRange = 0.25;       // lifted black point
        private const double RegionRange = 0.35;     // highlights / shadows
        private const double TintRange = 0.12;       // split toning

        // ---- night-vision guard ----------------------------------------------------------
        //
        // Shadows, blacks and fade all lift dark areas. Taken to their limits together they
        // stop being a look and become night vision: a player crouched in an unlit corner,
        // rendered by the game at near-black, gets pulled up to plainly visible grey.
        //
        // That is not what this app is for. It is an unfair advantage, it is the kind of
        // thing that gets people banned, and "we sell a legal edge" stops being true the
        // moment the sliders can do it. The product line in docs/ROADMAP.md - nothing that
        // risks anti-cheat accounts - covers this as much as it covers injection.
        //
        // So the very bottom of the curve is capped. The cap is hard at true black and
        // releases quickly, so a filmic lifted-black look still works while genuinely dark
        // content stays dark.

        /// <summary>Most a pure-black pixel may be lifted to. Roughly 20/255 - visibly not
        /// pure black, nowhere near enough to pick a body out of an unlit corner.</summary>
        internal const double NightGuardFloor = 0.08;

        /// <summary>How much the cap rises with the input while it is still binding.</summary>
        private const double NightGuardSlope = 1.0;

        /// <summary>
        /// Where the guard has fully released. About 64/255 - by then the picture is visible
        /// terrain rather than somewhere to hide, so restricting it would only be deleting
        /// grading the user paid for.
        /// </summary>
        private const double NightGuardRange = 0.25;

        /// <summary>The highest output the cap alone would permit at this input.</summary>
        internal static double NightGuardCap(double input) =>
            NightGuardFloor + input * NightGuardSlope;

        /// <summary>
        /// Apply the guard, fading its influence out as the input brightens.
        ///
        /// Blended rather than switched off at a threshold. A hard cut-off puts a visible
        /// edge in the curve, which shows up as a band across anything with a smooth dark
        /// gradient - a night sky, smoke, a dim wall. Smoothstep keeps the first derivative
        /// continuous so there is nothing to see.
        /// </summary>
        private static double ApplyNightGuard(double value, double input)
        {
            if (input >= NightGuardRange) return value;

            double capped = Math.Min(value, NightGuardCap(input));

            // smoothstep: 0 at the black end, 1 where the guard lets go.
            double t = input / NightGuardRange;
            t = t * t * (3 - 2 * t);

            return capped * (1 - t) + value * t;
        }

        public static ushort[] Build(ToneSettings t)
        {
            var ramp = new ushort[GammaCurve.Entries * 3];

            // ResolvedGamma, not Gamma: a zero-initialised or JSON-defaulted ToneSettings
            // carries Gamma = 0, which would clamp to 50 and darken the screen on upgrade.
            double gamma = Math.Clamp(t.ResolvedGamma,
                VibranceEngine.MinGamma, VibranceEngine.MaxGamma) / 100.0;
            double invGamma = 1.0 / (gamma <= 0 ? 1 : gamma);

            double black = Norm(t.Blacks) * EndpointRange;
            double white = 1.0 + Norm(t.Whites) * EndpointRange;
            double fade = Math.Clamp(t.Fade, 0, 100) / 100.0 * FadeRange;
            double hi = Norm(t.Highlights);
            double sh = Norm(t.Shadows);

            double tintS = Norm(t.ShadowTint) * TintRange;
            double tintM = Norm(t.MidtoneTint) * TintRange;
            double tintH = Norm(t.HighlightTint) * TintRange;

            double span = white - black;

            for (int i = 0; i < GammaCurve.Entries; i++)
            {
                double x = i / (double)(GammaCurve.Entries - 1);

                // 1. Endpoints first, before anything reshapes the middle.
                double v = span <= 0.0001 ? 0 : (x - black) / span;
                v = Math.Clamp(v, 0, 1);

                // 2. Gamma. With everything else neutral this is identical to
                //    GammaCurve.Build, which is what keeps saved gamma values meaning the
                //    same thing after this shipped.
                v = Math.Pow(v, invGamma);

                // 3. Region weights. Shadows bite hardest at black, highlights at white, and
                //    midtones peak in the middle. Each falls to zero at the end it does not
                //    own, so one control never quietly drags another's range with it.
                double wS = (1 - v) * (1 - v);
                double wH = v * v;
                double wM = 4 * v * (1 - v);

                v += sh * wS * RegionRange;
                v += hi * wH * RegionRange;
                v = Math.Clamp(v, 0, 1);

                // 4. Fade last, so the raised black point survives everything above it.
                v = fade + v * (1 - fade);

                // 5. The night-vision guard, before the tint so it caps the luminance the
                //    tint then rides on rather than being applied per channel.
                v = ApplyNightGuard(v, x);

                // 6. Split tone. Warm pushes red up and blue down, cool the reverse, each
                //    weighted to the tonal region it belongs to.
                double tint = tintS * wS + tintM * wM + tintH * wH;

                ramp[i] = Q(v + tint);                                  // red
                ramp[GammaCurve.Entries + i] = Q(v);                    // green
                ramp[GammaCurve.Entries * 2 + i] = Q(v - tint);         // blue
            }

            EnforceMonotonic(ramp);
            return ramp;
        }

        private static double Norm(int slider) => Math.Clamp(slider, -100, 100) / 100.0;

        private static ushort Q(double v) =>
            (ushort)Math.Clamp(Math.Round(Math.Clamp(v, 0, 1) * 65535.0), 0, 65535);

        /// <summary>
        /// A ramp that dips produces visible banding and posterisation, and an aggressive
        /// tint on a steep curve can dip. Clamping each entry to at least its predecessor
        /// costs nothing and makes the output safe for any combination of settings.
        /// </summary>
        private static void EnforceMonotonic(ushort[] ramp)
        {
            for (int c = 0; c < 3; c++)
            {
                int o = c * GammaCurve.Entries;
                for (int i = 1; i < GammaCurve.Entries; i++)
                    if (ramp[o + i] < ramp[o + i - 1]) ramp[o + i] = ramp[o + i - 1];
            }
        }
    }
}
