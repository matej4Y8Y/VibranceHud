using System;

namespace VibranceHud
{
    /// <summary>
    /// Builds the single 5x5 screen color matrix that combines every software adjustment:
    /// saturation (vibrance above 100%), brightness calibration, and the eye-care warmth
    /// (blue-light reduction). One matrix means one cheap pass over the screen.
    ///
    /// Pure math - unit-tested without a GPU.
    /// </summary>
    public static class ColorAdjust
    {
        /// <summary>How much green/blue are pulled down at full eye-care warmth.</summary>
        public const float WarmGreenCut = 0.12f;
        public const float WarmBlueCut = 0.38f;

        /// <summary>
        /// How much of a software vibrance boost reaches the red channel. Vibrance is
        /// meant to spare skin tones, so red lifts less than green and blue - that's the
        /// whole reason it reads as "richer but still natural" rather than "sunburnt".
        /// </summary>
        public const float VibranceRedHold = 0.55f;

        /// <param name="saturation">1.0 = unchanged, &gt;1 oversaturates.</param>
        /// <param name="brightness">1.0 = unchanged (0.5 = half, 1.5 = brighter).</param>
        /// <param name="warmth">0 = off, 1 = maximum eye-care warmth.</param>
        public static float[] Build(float saturation, float brightness, float warmth)
            => Build(saturation, 1f, brightness, warmth);

        /// <param name="saturation">Flat saturation: every colour lifted equally.</param>
        /// <param name="vibrance">Software vibrance on top (1.0 = none). Only used past
        /// the driver's own 100% ceiling, where there's no hardware left to ask.</param>
        public static float[] Build(float saturation, float vibrance, float brightness, float warmth)
        {
            // The two boosts compose per channel: a luminance-preserving saturation
            // applied twice is the product of its factors, so one matrix still does it all.
            float vR = 1f + (vibrance - 1f) * VibranceRedHold;
            var m = SaturationMatrix.Build(saturation * vR, saturation * vibrance, saturation * vibrance);

            float gainR = brightness;
            float gainG = brightness * (1f - WarmGreenCut * warmth);
            float gainB = brightness * (1f - WarmBlueCut * warmth);

            for (int row = 0; row < 3; row++)
            {
                m[row * 5 + 0] *= gainR;
                m[row * 5 + 1] *= gainG;
                m[row * 5 + 2] *= gainB;
            }
            return m;
        }

        /// <summary>True when the settings leave the screen untouched (skip the overlay).</summary>
        public static bool IsIdentity(float saturation, float brightness, float warmth)
            => IsIdentity(saturation, 1f, brightness, warmth);

        public static bool IsIdentity(float saturation, float vibrance, float brightness, float warmth)
            => Math.Abs(saturation - 1f) < 0.001f
            && Math.Abs(vibrance - 1f) < 0.001f
            && Math.Abs(brightness - 1f) < 0.001f
            && Math.Abs(warmth) < 0.001f;
    }
}
