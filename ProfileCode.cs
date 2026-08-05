using System;
using System.Linq;
using System.Text;

namespace VibranceHud
{
    /// <summary>
    /// Someone's whole look, in a string short enough to type into Discord.
    ///
    /// This is how the app's name travels. "What are your settings" gets answered with
    /// PX-something instead of a screenshot, and every copy of that code is the product
    /// advertising itself for free.
    ///
    /// Because it gets read off streams and retyped by hand, two things matter more than
    /// compactness: the alphabet has no characters anyone confuses, and a mistyped code is
    /// rejected rather than quietly applying a stranger's screen.
    /// </summary>
    public readonly record struct ProfileCode(
        int Vibrance, int Saturation, int Brightness, int Gamma,
        int Contrast = 100, int Temperature = 0)
    {
        private const string Prefix = "PX-";

        /// <summary>Body length of a code written before contrast and temperature existed:
        /// four values plus a check character.</summary>
        private const int LegacyLength = 9;

        /// <summary>Body length now: six values plus a check character.</summary>
        private const int CurrentLength = 13;

        /// <summary>
        /// 32 characters, chosen so nothing in it can be misread: no O or 0, no I, L or 1.
        /// Someone reading a code off a stream should never have to guess.
        /// </summary>
        private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ#";

        // Slider bounds live in the engine; repeating the numbers here is how they drift apart.
        private static int ClampVibrance(int v) => Math.Clamp(v, 0, VibranceEngine.MaxVibrance);
        private static int ClampSaturation(int v) => Math.Clamp(v, 0, VibranceEngine.MaxSaturation);
        private static int ClampBrightness(int v) =>
            Math.Clamp(v, VibranceEngine.MinBrightness, VibranceEngine.MaxBrightness);
        private static int ClampGamma(int v) =>
            Math.Clamp(v, VibranceEngine.MinGamma, VibranceEngine.MaxGamma);
        private static int ClampContrast(int v) =>
            Math.Clamp(v, VibranceEngine.MinContrast, VibranceEngine.MaxContrast);
        private static int ClampTemperature(int v) =>
            Math.Clamp(v, VibranceEngine.MinTemperature, VibranceEngine.MaxTemperature);

        public static string Encode(ProfileCode profile)
        {
            // Brightness and gamma are stored as an offset from their own minimum so each one
            // fits the same byte as everything else.
            // Ints, not bytes. Two characters of a 32-symbol alphabet hold 0-1023, which
            // covers every slider; a byte topped out at 255 and would have silently truncated
            // a maxed-out vibrance into somebody else's screen.
            int[] payload =
            {
                ClampVibrance(profile.Vibrance),
                ClampSaturation(profile.Saturation),
                ClampBrightness(profile.Brightness) - VibranceEngine.MinBrightness,
                ClampGamma(profile.Gamma) - VibranceEngine.MinGamma,
                ClampContrast(profile.Contrast) - VibranceEngine.MinContrast,
                // Temperature is the only signed control, so it is offset past its own
                // minimum to keep every field a plain non-negative number.
                ClampTemperature(profile.Temperature) - VibranceEngine.MinTemperature,
            };

            var body = new StringBuilder();
            foreach (int value in payload)
            {
                body.Append(Alphabet[value / 32]);
                body.Append(Alphabet[value % 32]);
            }

            body.Append(Alphabet[Checksum(body.ToString())]);
            return Prefix + body;
        }

        public static bool TryDecode(string? code, out ProfileCode profile)
        {
            profile = default;
            if (string.IsNullOrWhiteSpace(code)) return false;

            var cleaned = code.Trim().ToUpperInvariant();
            if (!cleaned.StartsWith(Prefix, StringComparison.Ordinal)) return false;

            var body = cleaned.Substring(Prefix.Length);

            // Length tells the two generations apart. Codes shared before contrast and
            // temperature existed are still out there in Discord history and on streams, and
            // they have to keep working - they simply carry neutral values for the two fields
            // they never knew about.
            bool legacy = body.Length == LegacyLength;
            if (!legacy && body.Length != CurrentLength) return false;

            var digits = new int[body.Length];
            for (int i = 0; i < body.Length; i++)
            {
                digits[i] = Alphabet.IndexOf(body[i]);
                if (digits[i] < 0) return false;                   // not one of our characters
            }

            int fields = legacy ? 4 : 6;
            int[] payload = new int[fields];
            for (int i = 0; i < fields; i++)
                payload[i] = digits[i * 2] * 32 + digits[i * 2 + 1];

            if (digits[fields * 2] != Checksum(body.Substring(0, fields * 2)))
                return false;                                      // a typo, or somebody guessing

            profile = new ProfileCode(
                ClampVibrance(payload[0]),
                ClampSaturation(payload[1]),
                ClampBrightness(payload[2] + VibranceEngine.MinBrightness),
                ClampGamma(payload[3] + VibranceEngine.MinGamma),
                legacy ? 100 : ClampContrast(payload[4] + VibranceEngine.MinContrast),
                legacy ? 0 : ClampTemperature(payload[5] + VibranceEngine.MinTemperature));
            return true;
        }

        /// <summary>
        /// Runs over the characters, not the values behind them, and every weight is odd.
        ///
        /// Both details are load-bearing. Checksumming the reconstructed bytes missed any
        /// single-character mistake that shifted a byte by exactly 32, because 32 is the size
        /// of the alphabet and the change vanished in the modulo - a test caught a real code
        /// where changing the first character was completely invisible.
        ///
        /// Working on digits, each one is 0-31, so a wrong character shifts the sum by
        /// weight x difference with the difference smaller than the modulus. Odd weights keep
        /// that from landing back on zero, and distinct weights mean two swapped characters
        /// don't cancel each other out either. Those are the two mistakes people actually make
        /// retyping a code off a screen.
        /// </summary>
        private static int Checksum(string body)
        {
            int sum = 0;
            for (int i = 0; i < body.Length; i++)
                sum += Alphabet.IndexOf(body[i]) * (2 * i + 1);
            return sum % 32;
        }
    }
}
