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

        /// <param name="saturation">1.0 = unchanged, &gt;1 oversaturates.</param>
        /// <param name="brightness">1.0 = unchanged (0.5 = half, 1.5 = brighter).</param>
        /// <param name="warmth">0 = off, 1 = maximum eye-care warmth.</param>
        public static float[] Build(float saturation, float brightness, float warmth)
        {
            // Start from the luminance-preserving saturation matrix, then scale each
            // output channel: brightness hits all three equally, warmth pulls green and
            // blue down so the picture shifts amber.
            var m = SaturationMatrix.Build(saturation);

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
            => Math.Abs(saturation - 1f) < 0.001f
            && Math.Abs(brightness - 1f) < 0.001f
            && Math.Abs(warmth) < 0.001f;
    }
}
