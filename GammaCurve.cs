using System;

namespace VibranceHud
{
    /// <summary>
    /// Builds a Windows gamma ramp: 3 x 256 16-bit entries (R, G, B lookup tables).
    /// Gamma is a non-linear curve (out = in^(1/gamma)) so it can't be expressed as a
    /// color matrix - it needs the display's gamma ramp instead.
    ///
    /// Pure math, unit-tested without a display.
    /// </summary>
    public static class GammaCurve
    {
        public const int Entries = 256;

        /// <param name="gamma">1.0 = untouched, &gt;1 lifts midtones, &lt;1 deepens them.</param>
        public static ushort[] Build(float gamma)
        {
            if (gamma <= 0f) gamma = 1f;
            var ramp = new ushort[Entries * 3];
            double invGamma = 1.0 / gamma;

            for (int i = 0; i < Entries; i++)
            {
                double normalized = i / (double)(Entries - 1);
                double corrected = Math.Pow(normalized, invGamma);
                var value = (ushort)Math.Clamp(Math.Round(corrected * 65535.0), 0, 65535);

                ramp[i] = value;                    // red
                ramp[Entries + i] = value;          // green
                ramp[Entries * 2 + i] = value;      // blue
            }
            return ramp;
        }

        /// <summary>The untouched (linear) ramp.</summary>
        public static ushort[] Identity() => Build(1f);
    }
}
