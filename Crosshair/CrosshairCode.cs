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

                Opacity = Math.Clamp(values[6], CrosshairLimits.MinOpacity, CrosshairLimits.MaxOpacity),

                // Masked to a byte each. Without this a value above 255 - reachable, since two
                // symbols hold up to 1023 - bleeds its high bits into the next channel, so a
                // code with no red at all could decode to red 1.
                ColourArgb = unchecked((int)0xFF000000)
                           | ((values[7] & 0xFF) << 16)
                           | ((values[8] & 0xFF) << 8)
                           | (values[9] & 0xFF),
            };

            // Clamped into the same ranges the sliders use, and set through the Set*Tenths
            // methods rather than the raw fields.
            //
            // Clamped, because a decoded value outside a slider's range leaves the page
            // disagreeing with itself: the crosshair draws at the decoded size while the
            // slider shows its own maximum, and saving then persists the number nobody can see.
            //
            // Through the setters, because those also write the legacy whole-pixel fields - a
            // build that only knows about whole pixels would otherwise read 8 for a crosshair
            // somebody had shared at 3.4.
            crosshair.SetSizeTenths(Math.Clamp(values[1], CrosshairLimits.MinSizeTenths, CrosshairLimits.MaxSizeTenths));
            crosshair.SetThicknessTenths(Math.Clamp(values[2], CrosshairLimits.MinThicknessTenths, CrosshairLimits.MaxThicknessTenths));
            crosshair.SetGapTenths(Math.Clamp(values[3], CrosshairLimits.MinGapTenths, CrosshairLimits.MaxGapTenths));
            crosshair.DotSizeTenths = Math.Clamp(values[4], CrosshairLimits.MinDotTenths, CrosshairLimits.MaxDotTenths);
            crosshair.CircleRadiusTenths = Math.Clamp(values[5], CrosshairLimits.MinRingTenths, CrosshairLimits.MaxRingTenths);

            return true;
        }

        /// <summary>Float to tenths, clamped into the two-symbol range.</summary>
        private static int Tenths(float value) =>
            Math.Clamp((int)Math.Round(value * 10f), 0, 1023);

        /// <summary>
        /// Position-weighted with ODD weights, so every single-character substitution is
        /// caught as well as every transposition.
        ///
        /// The weight has to be odd. With a 32-symbol alphabet and a modulus of 32, an odd
        /// weight is coprime to 32, so a change of delta at position i shifts the checksum by
        /// delta*weight mod 32, which is zero only when delta is zero. An even weight breaks
        /// that: this first shipped as (i + 1), which is even at half the positions, and left
        /// 36 single-character typos undetectable. A real pair -
        ///
        ///     PXC-3Y4U2U3B43BV4B2P8A9MG   ring ON
        ///     PXC-3F4U2U3B43BV4B2P8A9MG   ring OFF
        ///
        /// - differ by one character and produce the same checksum, so the second decoded
        /// silently into a different crosshair. ProfileCode already used (2*i + 1) and says
        /// why; this is the same reasoning, arrived at the hard way.
        /// </summary>
        private static int Checksum(string body)
        {
            int sum = 0;
            for (int i = 0; i < body.Length; i++)
                sum += (Alphabet.IndexOf(body[i]) + 1) * (2 * i + 1);
            return sum % 32;
        }
    }
}
