using VibranceHud.Crosshair;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Crosshair share codes.
    ///
    /// Same idea as the display share code and for the same reason: somebody sees a crosshair
    /// they like and asks how it was made. A screenshot cannot answer that; a short code can.
    ///
    /// The rules that matter are the ones the display code already learned - a code must
    /// survive the round trip exactly, and a mistyped one must change nothing rather than
    /// silently applying a different crosshair.
    /// </summary>
    public sealed class CrosshairCodeTests
    {
        private static CrosshairConfig Sample() => new()
        {
            ArmTop = true,
            ArmBottom = false,
            ArmLeft = true,
            ArmRight = true,
            ShowCircle = true,
            CentreDot = true,
            Outline = false,
            SizeTenths = 87,
            ThicknessTenths = 23,
            GapTenths = 41,
            DotSizeTenths = 65,
            CircleRadiusTenths = 312,
            Opacity = 73,
            ColourArgb = unchecked((int)0xFF12C8F0),
        };

        [Fact]
        public void ACrosshairSurvivesTheRoundTrip()
        {
            var original = Sample();

            Assert.True(CrosshairCode.TryDecode(CrosshairCode.Encode(original), out var back));

            Assert.Equal(original.ResolvedArmTop, back.ResolvedArmTop);
            Assert.Equal(original.ResolvedArmBottom, back.ResolvedArmBottom);
            Assert.Equal(original.ResolvedArmLeft, back.ResolvedArmLeft);
            Assert.Equal(original.ResolvedArmRight, back.ResolvedArmRight);
            Assert.Equal(original.ResolvedShowCircle, back.ResolvedShowCircle);
            Assert.Equal(original.ResolvedCentreDot, back.ResolvedCentreDot);
            Assert.Equal(original.Outline, back.Outline);
            Assert.Equal(original.ResolvedSize, back.ResolvedSize);
            Assert.Equal(original.ResolvedThickness, back.ResolvedThickness);
            Assert.Equal(original.ResolvedGap, back.ResolvedGap);
            Assert.Equal(original.ResolvedDotSize, back.ResolvedDotSize);
            Assert.Equal(original.ResolvedCircleRadius, back.ResolvedCircleRadius);
            Assert.Equal(original.Opacity, back.Opacity);
            Assert.Equal(original.ColourArgb & 0x00FFFFFF, back.ColourArgb & 0x00FFFFFF);
        }

        [Fact]
        public void ACodeStartsWithTheCrosshairPrefix()
        {
            Assert.StartsWith("PXC-", CrosshairCode.Encode(Sample()));
        }

        /// <summary>
        /// A display code and a crosshair code must not be interchangeable. Pasting the wrong
        /// one is the likeliest mistake somebody will make, and applying it would rewrite a
        /// crosshair from numbers that meant saturation and gamma.
        /// </summary>
        [Fact]
        public void ADisplayCodeIsNotAcceptedAsACrosshair()
        {
            string display = ProfileCode.Encode(new ProfileCode
            {
                Vibrance = 50, Saturation = 120, Brightness = 100,
                Gamma = 100, Contrast = 100, Temperature = 0,
            });

            Assert.False(CrosshairCode.TryDecode(display, out _));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("PXC-")]
        [InlineData("PXC-2222")]
        [InlineData("nonsense")]
        public void RubbishIsRefused(string? code)
        {
            Assert.False(CrosshairCode.TryDecode(code, out _));
        }

        /// <summary>
        /// The checksum's whole job, checked exhaustively rather than at one lucky position.
        ///
        /// The first version of this test changed a single character near the end and passed -
        /// while 36 other single-character typos, spread across ten positions, decoded
        /// silently into a different crosshair. The weight was even at half the positions,
        /// which destroys the guarantee. Every position and every substitution now.
        /// </summary>
        [Fact]
        public void NoSingleCharacterTypoSurvivesAnywhereInTheCode()
        {
            const string alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ#";
            string code = CrosshairCode.Encode(Sample());
            var missed = new System.Collections.Generic.List<string>();

            // Body only; the four-character prefix is checked separately.
            for (int i = 4; i < code.Length; i++)
            {
                foreach (char c in alphabet)
                {
                    if (c == code[i]) continue;

                    var chars = code.ToCharArray();
                    chars[i] = c;

                    if (CrosshairCode.TryDecode(new string(chars), out _))
                        missed.Add($"position {i - 4}: '{code[i]}' -> '{c}'");
                }
            }

            Assert.True(missed.Count == 0,
                $"{missed.Count} single-character typos decode as valid:\n  "
                + string.Join("\n  ", missed.Take(12)));
        }

        /// <summary>Transposing two adjacent characters must fail too - that is what happens
        /// when somebody retypes a code rather than pasting it.</summary>
        [Fact]
        public void SwappingTwoAdjacentCharactersIsRejected()
        {
            string code = CrosshairCode.Encode(Sample());

            for (int i = 4; i < code.Length - 1; i++)
            {
                if (code[i] == code[i + 1]) continue;

                var chars = code.ToCharArray();
                (chars[i], chars[i + 1]) = (chars[i + 1], chars[i]);

                Assert.False(CrosshairCode.TryDecode(new string(chars), out _),
                    $"swapping positions {i - 4} and {i - 3} was accepted");
            }
        }

        /// <summary>
        /// A code is not a way around the sliders' limits. A value outside a slider's range
        /// would leave the crosshair drawing at one size while the slider showed another.
        /// </summary>
        [Fact]
        public void OutOfRangeValuesAreClampedRatherThanTrusted()
        {
            var huge = new CrosshairConfig
            {
                ArmTop = true, ArmBottom = true, ArmLeft = true, ArmRight = true,
                ShowCircle = true, CentreDot = true, Outline = true,
                SizeTenths = 999, ThicknessTenths = 999, GapTenths = 999,
                DotSizeTenths = 999, CircleRadiusTenths = 999, Opacity = 100,
                ColourArgb = unchecked((int)0xFF00FF66),
            };

            Assert.True(CrosshairCode.TryDecode(CrosshairCode.Encode(huge), out var back));

            Assert.InRange(back.SizeTenths!.Value, CrosshairLimits.MinSizeTenths, CrosshairLimits.MaxSizeTenths);
            Assert.InRange(back.ThicknessTenths!.Value, CrosshairLimits.MinThicknessTenths, CrosshairLimits.MaxThicknessTenths);
            Assert.InRange(back.GapTenths!.Value, CrosshairLimits.MinGapTenths, CrosshairLimits.MaxGapTenths);
            Assert.InRange(back.DotSizeTenths!.Value, CrosshairLimits.MinDotTenths, CrosshairLimits.MaxDotTenths);
            Assert.InRange(back.CircleRadiusTenths!.Value, CrosshairLimits.MinRingTenths, CrosshairLimits.MaxRingTenths);
            Assert.InRange(back.Opacity, CrosshairLimits.MinOpacity, CrosshairLimits.MaxOpacity);
        }

        /// <summary>The legacy whole-pixel fields have to move with the tenths, or a build
        /// that only knows about whole pixels reads 8 for a crosshair shared at 3.4.</summary>
        [Fact]
        public void DecodingAlsoSetsTheLegacyWholePixelFields()
        {
            var config = Sample();
            config.SetSizeTenths(34);

            Assert.True(CrosshairCode.TryDecode(CrosshairCode.Encode(config), out var back));

            Assert.Equal(3, back.Size);
        }

        /// <summary>A channel above 255 must not bleed its high bits into the next one.</summary>
        [Fact]
        public void ColourChannelsDoNotBleedIntoEachOther()
        {
            var black = Sample();
            black.ColourArgb = unchecked((int)0xFF000000);

            Assert.True(CrosshairCode.TryDecode(CrosshairCode.Encode(black), out var back));

            Assert.Equal(0, back.ColourArgb & 0x00FFFFFF);
        }

        [Fact]
        public void CaseAndSurroundingSpaceAreForgiven()
        {
            string code = CrosshairCode.Encode(Sample());

            Assert.True(CrosshairCode.TryDecode("  " + code.ToLowerInvariant() + "  ", out var back));
            Assert.Equal(73, back.Opacity);
        }

        /// <summary>A decoded crosshair must be usable straight away, not a half-built object
        /// that still depends on whatever the legacy Shape field happened to be.</summary>
        [Fact]
        public void ADecodedCrosshairDoesNotDependOnTheLegacyShape()
        {
            Assert.True(CrosshairCode.TryDecode(CrosshairCode.Encode(Sample()), out var back));

            Assert.NotNull(back.ArmTop);
            Assert.NotNull(back.ShowCircle);
            Assert.NotNull(back.SizeTenths);
            Assert.NotNull(back.CircleRadiusTenths);
        }
    }
}
