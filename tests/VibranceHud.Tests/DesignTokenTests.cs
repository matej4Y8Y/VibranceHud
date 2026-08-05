using VibranceHud.Design;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The token scale is what makes DPI-correct layout possible, so its maths is pinned
    /// here rather than discovered at 150% on someone's laptop.
    /// </summary>
    public sealed class DesignTokenTests
    {
        [Theory]
        [InlineData(96, 16, 16)]    // 100%
        [InlineData(120, 16, 20)]   // 125%
        [InlineData(144, 16, 24)]   // 150%
        [InlineData(192, 16, 32)]   // 200%
        public void ScaleConvertsLogicalPixelsToDevicePixels(int dpi, int logical, int expected)
        {
            Assert.Equal(expected, Tokens.ScaleAt(dpi, logical));
        }

        [Fact]
        public void SpacingScaleIsMonotonic()
        {
            int[] steps = { Tokens.XS, Tokens.S, Tokens.M, Tokens.L, Tokens.XL, Tokens.XXL, Tokens.XXXL };
            for (int i = 1; i < steps.Length; i++)
                Assert.True(steps[i] > steps[i - 1], $"step {i} must exceed step {i - 1}");
        }

        [Fact]
        public void ZeroScalesToZero()
        {
            Assert.Equal(0, Tokens.ScaleAt(144, 0));
        }

        /// <summary>
        /// A 1px divider that rounds away to nothing is how borders silently vanish at
        /// fractional scale factors. Every positive input must survive as at least 1px.
        /// </summary>
        [Theory]
        [InlineData(96)]
        [InlineData(120)]
        [InlineData(144)]
        [InlineData(192)]
        public void HairlinesNeverRoundAwayToNothing(int dpi)
        {
            Assert.True(Tokens.ScaleAt(dpi, 1) >= 1);
        }

        [Fact]
        public void NegativeOffsetsStayNegative()
        {
            Assert.True(Tokens.ScaleAt(144, -4) <= -1);
        }

        [Fact]
        public void FontRolesAreCachedNotReallocated()
        {
            Assert.Same(Fonts.Body, Fonts.Body);
            Assert.Same(Fonts.Title, Fonts.Title);
            Assert.Same(Fonts.Micro, Fonts.Micro);
        }

        [Fact]
        public void RebuildingFontsYieldsFreshInstances()
        {
            var before = Fonts.Body;
            Fonts.Rebuild();
            Assert.NotSame(before, Fonts.Body);
        }

        [Fact]
        public void EveryRoleResolvesToAUsableFont()
        {
            foreach (var f in new[] { Fonts.Display, Fonts.Title, Fonts.Heading, Fonts.Body,
                                      Fonts.Label, Fonts.Caption, Fonts.Micro,
                                      Fonts.BodyBold, Fonts.LabelBold, Fonts.CaptionBold })
            {
                Assert.NotNull(f);
                Assert.True(f.Size > 0);
            }
        }

        [Fact]
        public void TheTypeScaleDescendsFromDisplayToMicro()
        {
            Assert.True(Fonts.Display.Size > Fonts.Title.Size);
            Assert.True(Fonts.Title.Size > Fonts.Heading.Size);
            Assert.True(Fonts.Heading.Size > Fonts.Body.Size);
            Assert.True(Fonts.Body.Size > Fonts.Label.Size);
            Assert.True(Fonts.Label.Size > Fonts.Caption.Size);
            Assert.True(Fonts.Caption.Size > Fonts.Micro.Size);
        }
    }
}
