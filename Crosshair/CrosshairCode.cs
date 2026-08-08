using System;
using System.Text;

namespace VibranceHud.Crosshair
{
    /// <summary>
    /// A crosshair as a short code somebody can paste into Discord.
    ///
    /// Same scheme as <see cref="ProfileCode"/> - two symbols per value from a 32-character
    /// alphabet, one checksum character on the end - because a second encoding to get wrong
    /// helps nobody. The prefix differs so a display code and a crosshair code cannot be
    /// mistaken for each other, which is the likeliest mistake anyone will actually make.
    ///
    /// Everything is written from the Resolved* accessors, so a code never carries the legacy
    /// Shape field's ambiguity: what goes in is what the crosshair currently draws, and what
    /// comes out is fully specified without needing to know what Shape it came from.
    /// </summary>
    public static class CrosshairCode
    {
        private const string Prefix = "PXC-";

        /// <summary>Same 32 symbols as the display code: no O or 0, no I, L or 1, so nothing
        /// can be misread off a stream.</summary>
        private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ#";

        /// <summary>Ten values, two characters each, plus the checksum.</summary>
        private const int ValueCount = 10;
        private const int BodyLength = ValueCount * 2 + 1;

        // Bit positions inside the flags value. Adding one here changes every existing code,
        // so append rather than insert.
        private const int ArmTop = 1 << 0;
        private const int ArmBottom = 1 << 1;
        private const int ArmLeft = 1 << 2;
        private const int ArmRight = 1 << 3;
        private const int Circle = 1 << 4;
        private const int Dot = 1 << 5;
        private const int Outline = 1 << 6;

        public static string Encode(CrosshairConfig c)
        {
            int flags = 0;
            if (c.ResolvedArmTop) flags |= ArmTop;
            if (c.ResolvedArmBottom) flags |= ArmBottom;
            if (c.ResolvedArmLeft) flags |= ArmLeft;
            if (c.ResolvedArmRight) flags |= ArmRight;
            if (c.ResolvedShowCircle) flags |= Circle;
            if (c.ResolvedCentreDot) flags |= Dot;
            if (c.Outline) flags |= Outline;

            int argb = c.ColourArgb;

            var payload = new[]
            {
                flags,
                Tenths(c.ResolvedSize),
                Tenths(c.ResolvedThickness),
                Tenths(c.ResolvedGap),
                Tenths(c.ResolvedDotSize),
                Tenths(c.ResolvedCircleRadius),
                Math.Clamp(c.Opacity, 0, 100),
                (argb >> 16) & 0xFF,
                (argb >> 8) & 0xFF,
                argb & 0xFF,
            };

            var body = new StringBuilder(BodyLength);
            foreach (int raw in payload)
            {
                // Two symbols hold 0-1023, which covers every slider on the page. Clamped
                // rather than trusted: a value past the top would wrap into a different
                // crosshair rather than failing.
                int value = Math.Clamp(raw, 0, 1023);
                body.Append(Alphabet[value / 32]);
                body.Append(Alphabet[value % 32]);
            }

            body.Append(Alphabet[Checksum(body.ToString())]);
            return Prefix + body;
        }

        public static bool TryDecode(string? code, out CrosshairConfig crosshair)
        {
            crosshair = new CrosshairConfig();

            if (string.IsNullOrWhiteSpace(code)) return false;

            var text = code.Trim().ToUpperInvariant();
            if (!text.StartsWith(Prefix, StringComparison.Ordinal)) return false;

            var body = text.Substring(Prefix.Length);
            if (body.Length != BodyLength) return false;

            var digits = new int[body.Length];
            for (int i = 0; i < body.Length; i++)
            {
                digits[i] = Alphabet.IndexOf(body[i]);
                if (digits[i] < 0) return false;
            }

            if (digits[ValueCount * 2] != Checksum(body.Substring(0, ValueCount * 2)))
                return false;

            var values = new int[ValueCount];
            for (int i = 0; i < ValueCount; i++)
                values[i] = digits[i * 2] * 32 + digits[i * 2 + 1];

            int flags = values[0];

            crosshair = new CrosshairConfig
            {
                // Every field set explicitly, so the result never falls back to the legacy
                // Shape defaults for anything.
                ArmTop = (flags & ArmTop) != 0,
                ArmBottom = (flags & ArmBottom) != 0,
                ArmLeft = (flags & ArmLeft) != 0,
                ArmRight = (flags & ArmRight) != 0,
                ShowCircle = (flags & Circle) != 0,
                CentreDot = (flags & Dot) != 0,
                Outline = (flags & Outline) != 0,

                SizeTenths = values[1],
                ThicknessTenths = values[2],
                GapTenths = values[3],
                DotSizeTenths = values[4],
                CircleRadiusTenths = values[5],
                Opacity = Math.Clamp(values[6], 0, 100),

                ColourArgb = unchecked((int)0xFF000000)
                           | (values[7] << 16) | (values[8] << 8) | values[9],
            };

            return true;
        }

        /// <summary>Float to tenths, clamped into the two-symbol range.</summary>
        private static int Tenths(float value) =>
            Math.Clamp((int)Math.Round(value * 10f), 0, 1023);

        /// <summary>
        /// Position-weighted, so transposing two characters fails.
        ///
        /// A plain sum would accept a swap, and swapping two characters is exactly what
        /// happens when somebody retypes a code rather than pasting it.
        /// </summary>
        private static int Checksum(string body)
        {
            int sum = 0;
            for (int i = 0; i < body.Length; i++)
                sum += (Alphabet.IndexOf(body[i]) + 1) * (i + 1);
            return sum % 32;
        }
    }
}
