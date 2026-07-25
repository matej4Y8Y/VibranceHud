namespace VibranceHud
{
    /// <summary>
    /// Builds a luminance-preserving saturation matrix in the 5x5 row-major layout
    /// the Windows Magnification API expects (MAGCOLOREFFECT), using the row-vector
    /// convention newColor = oldColor * M.
    ///
    /// A saturation factor of 1.0 yields the identity matrix (no change); values above
    /// 1.0 oversaturate. This is pure math with no dependency on the driver or OS, so it
    /// is unit-tested directly.
    /// </summary>
    public static class SaturationMatrix
    {
        // Rec. 709 luma coefficients.
        private const float Lr = 0.2126f;
        private const float Lg = 0.7152f;
        private const float Lb = 0.0722f;

        public static float[] Build(float saturation)
            => Build(saturation, saturation, saturation);

        /// <summary>
        /// Per-channel saturation. Each output channel c is
        /// <c>luma*(1-s_c) + s_c*in_c</c>, so equal factors give the plain
        /// luminance-preserving matrix above.
        ///
        /// Unequal factors are what makes a *vibrance* boost look different from a flat
        /// saturation boost: holding the red channel back keeps skin tones from going
        /// orange while the rest of the picture still lifts. Luma is then only
        /// approximately preserved, which is the accepted trade for that look.
        /// </summary>
        public static float[] Build(float sR, float sG, float sB)
        {
            float aR = 1f - sR, aG = 1f - sG, aB = 1f - sB;

            // Row = input channel, col = output channel: newColor = oldColor * M.
            return new float[]
            {
                Lr * aR + sR, Lr * aG,      Lr * aB,      0f, 0f,
                Lg * aR,      Lg * aG + sG, Lg * aB,      0f, 0f,
                Lb * aR,      Lb * aG,      Lb * aB + sB, 0f, 0f,
                0f,           0f,           0f,           1f, 0f,
                0f,           0f,           0f,           0f, 1f,
            };
        }
    }
}
