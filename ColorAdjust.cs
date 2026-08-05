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
        /// <param name="warmth">0 = neutral, 1 = fully warm, -1 = fully cool.</param>
        public static float[] Build(float saturation, float brightness, float warmth)
            => Build(saturation, 1f, brightness, warmth);

        /// <param name="saturation">Flat saturation: every colour lifted equally.</param>
        /// <param name="vibrance">Software vibrance on top (1.0 = none). Only used past
        /// the driver's own 100% ceiling, where there's no hardware left to ask.</param>
        public static float[] Build(float saturation, float vibrance, float brightness, float warmth)
            => Build(saturation, vibrance, 1f, brightness, warmth);

        /// <summary>
        /// The full transform, in the order the eye expects it: saturate, then set the
        /// brightness and white balance, then stretch the contrast.
        /// </summary>
        /// <param name="contrast">1.0 = unchanged. Above 1 pushes lights up and darks down
        /// around mid-grey; below 1 flattens towards it.</param>
        public static float[] Build(float saturation, float vibrance, float contrast,
            float brightness, float warmth)
        {
            // The two boosts compose per channel: a luminance-preserving saturation
            // applied twice is the product of its factors, so one matrix still does it all.
            float vR = 1f + (vibrance - 1f) * VibranceRedHold;
            var m = SaturationMatrix.Build(saturation * vR, saturation * vibrance, saturation * vibrance);

            // White balance. Warm pulls green and blue down (the original eye-care curve, kept
            // exactly so a saved warmth still looks like it always did); cool is its mirror,
            // pulling red and green down instead. Both are zero at neutral, so the identity
            // check below stays honest.
            float warmT = Math.Max(0f, warmth);
            float coolT = Math.Max(0f, -warmth);

            float balR = 1f - WarmBlueCut * coolT;
            float balG = 1f - WarmGreenCut * (warmT + coolT);
            float balB = 1f - WarmBlueCut * warmT;

            // Contrast is a gain about mid-grey, which is a multiply plus a constant. The
            // constant is why this needs the matrix's translation row rather than folding
            // into the channel gains like everything else here.
            float gainR = brightness * balR * contrast;
            float gainG = brightness * balG * contrast;
            float gainB = brightness * balB * contrast;
            float offset = 0.5f * (1f - contrast);

            for (int row = 0; row < 3; row++)
            {
                m[row * 5 + 0] *= gainR;
                m[row * 5 + 1] *= gainG;
                m[row * 5 + 2] *= gainB;
            }

            // Row 4 is the translation row: newColour = oldColour * M with oldColour's last
            // component pinned at 1, so these three land as a constant added to R, G and B.
            m[20] = offset;
            m[21] = offset;
            m[22] = offset;
            return m;
        }

        /// <summary>True when the settings leave the screen untouched (skip the overlay).</summary>
        public static bool IsIdentity(float saturation, float brightness, float warmth)
            => IsIdentity(saturation, 1f, brightness, warmth);

        public static bool IsIdentity(float saturation, float vibrance, float brightness, float warmth)
            => IsIdentity(saturation, vibrance, 1f, brightness, warmth);

        public static bool IsIdentity(float saturation, float vibrance, float contrast,
            float brightness, float warmth)
            => Math.Abs(saturation - 1f) < 0.001f
            && Math.Abs(vibrance - 1f) < 0.001f
            && Math.Abs(contrast - 1f) < 0.001f
            && Math.Abs(brightness - 1f) < 0.001f
            && Math.Abs(warmth) < 0.001f;
    }
}
