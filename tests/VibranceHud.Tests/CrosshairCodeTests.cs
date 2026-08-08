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
        /// The checksum's whole job. One wrong character has to fail rather than decode into a
        /// different, plausible-looking crosshair.
        /// </summary>
        [Fact]
        public void ASingleWrongCharacterIsRejected()
        {
            string code = CrosshairCode.Encode(Sample());

            // Change one body character to a different valid symbol.
            var chars = code.ToCharArray();
            int i = code.Length - 3;
            chars[i] = chars[i] == '2' ? '3' : '2';

            Assert.False(CrosshairCode.TryDecode(new string(chars), out _));
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
