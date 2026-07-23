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
        {
            float s = saturation;
            float a = 1f - s;

            // Row = input channel, col = output channel: newColor = oldColor * M.
            return new float[]
            {
                Lr * a + s, Lr * a,     Lr * a,     0f, 0f,
                Lg * a,     Lg * a + s, Lg * a,     0f, 0f,
                Lb * a,     Lb * a,     Lb * a + s, 0f, 0f,
                0f,         0f,         0f,         1f, 0f,
                0f,         0f,         0f,         0f, 1f,
            };
        }
    }
}
