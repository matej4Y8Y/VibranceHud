using System;
using System.Security.Cryptography;
using System.Text;

namespace VibranceHud.Licensing
{
    /// <summary>
    /// The short code a customer receives, e.g. 2K7M-Q8XR-T9WD-N3FG.
    ///
    /// It carries no permissions and no duration - it is only an identifier redeemed once for
    /// a signed licence. That is deliberate: because the code grants nothing by itself,
    /// knowing the format buys an attacker nothing, and the code can stay short enough to read
    /// aloud or quote in a support message.
    ///
    /// The final character is a check digit, so a single mistyped or misread character is
    /// caught locally instead of becoming a failed redemption the user can't explain.
    /// </summary>
    public static class KeyCode
    {
        /// <summary>Crockford-style: no O, I, L, 0 or 1, so nothing is ambiguous when read off
        /// a screen or spoken aloud. 31 characters - prime, which is what makes the check digit
        /// catch every single-character change.</summary>
        private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

        private const int Groups = 4;
        private const int GroupSize = 4;
        private const int TotalChars = Groups * GroupSize; // 16; the last is the check digit

        public static string Generate()
        {
            var body = new char[TotalChars - 1];
            var buffer = new byte[body.Length];
            RandomNumberGenerator.Fill(buffer);
            for (int i = 0; i < body.Length; i++)
                body[i] = Alphabet[buffer[i] % Alphabet.Length];

            var raw = new string(body);
            return Format(raw + CheckDigit(raw));
        }

        /// <summary>Accepts what a user actually pastes - lowercase, stray spaces, missing
        /// dashes - and returns the canonical form, or "" if it can't be made sense of.</summary>
        public static string Normalise(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            var sb = new StringBuilder(TotalChars);
            foreach (var c in input.ToUpperInvariant())
            {
                if (c == '-' || char.IsWhiteSpace(c)) continue;
                if (Alphabet.IndexOf(c) < 0) return "";
                sb.Append(c);
                if (sb.Length > TotalChars) return "";
            }

            return sb.Length == TotalChars ? Format(sb.ToString()) : "";
        }

        public static bool IsWellFormed(string? input)
        {
            var normalised = Normalise(input);
            if (normalised.Length == 0) return false;

            var raw = normalised.Replace("-", "");
            var body = raw.Substring(0, raw.Length - 1);
            return raw[raw.Length - 1] == CheckDigit(body);
        }

        private static string Format(string raw)
        {
            var sb = new StringBuilder(TotalChars + Groups - 1);
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && i % GroupSize == 0) sb.Append('-');
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }

        /// <summary>Position-weighted sum, so transposing two characters is caught as well as
        /// changing one. A plain sum would miss swaps entirely, and swapping two characters is
        /// exactly what people do when copying a code by hand.</summary>
        private static char CheckDigit(string body)
        {
            int sum = 0;
            for (int i = 0; i < body.Length; i++)
                sum += (Alphabet.IndexOf(body[i]) + 1) * (i + 1);
            return Alphabet[sum % Alphabet.Length];
        }
    }
}
