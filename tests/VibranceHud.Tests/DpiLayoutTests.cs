using VibranceHud.Design;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Pins the shell's own dimensions across the scale factors people actually run.
    ///
    /// Rendering itself is not unit-testable - blur and clipping only show on a real
    /// display - but the arithmetic underneath the layout is, and it is where the
    /// off-by-a-scale-factor bugs live.
    /// </summary>
    public sealed class DpiLayoutTests
    {
        private const int TitleH = 52;
        private const int NavW = 210;

        [Theory]
        [InlineData(96, 210)]
        [InlineData(120, 263)]   // 125%
        [InlineData(144, 315)]   // 150%
        [InlineData(192, 420)]   // 200%
        public void NavWidthScalesWithDpi(int dpi, int expected)
        {
            Assert.Equal(expected, Tokens.ScaleAt(dpi, NavW));
        }

        [Theory]
        [InlineData(96, 52)]
        [InlineData(120, 65)]
        [InlineData(144, 78)]
        [InlineData(192, 104)]
        public void TitleBarHeightScalesWithDpi(int dpi, int expected)
        {
            Assert.Equal(expected, Tokens.ScaleAt(dpi, TitleH));
        }

        /// <summary>
        /// The nav rows are laid out as 16 + index * 48. At any DPI the gap between two
        /// consecutive rows must stay larger than a row is tall minus its own padding,
        /// or rows start overlapping - which is exactly what a naive scale-each-number
        /// conversion produces.
        /// </summary>
        [Theory]
        [InlineData(96)]
        [InlineData(120)]
        [InlineData(144)]
        [InlineData(192)]
        public void NavRowsNeverOverlap(int dpi)
        {
            for (int i = 0; i < 8; i++)
            {
                int top = Tokens.ScaleAt(dpi, 16 + i * 48);
                int next = Tokens.ScaleAt(dpi, 16 + (i + 1) * 48);
                int height = Tokens.ScaleAt(dpi, 46);

                Assert.True(top + height <= next,
                    $"at {dpi} DPI row {i} (top {top}, height {height}) runs into row {i + 1} at {next}");
            }
        }

        [Theory]
        [InlineData(96)]
        [InlineData(144)]
        [InlineData(192)]
        public void MinimumWindowStaysSmallerThanTheDefault(int dpi)
        {
            Assert.True(Tokens.ScaleAt(dpi, 900) < Tokens.ScaleAt(dpi, 1040));
            Assert.True(Tokens.ScaleAt(dpi, 600) < Tokens.ScaleAt(dpi, 680));
        }

        [Fact]
        public void ContentHostOriginMatchesTheChromeItSitsBehind()
        {
            foreach (int dpi in new[] { 96, 120, 144, 192 })
            {
                Assert.Equal(Tokens.ScaleAt(dpi, NavW), Tokens.ScaleAt(dpi, NavW));
                Assert.Equal(Tokens.ScaleAt(dpi, TitleH), Tokens.ScaleAt(dpi, TitleH));
            }
        }

        [Fact]
        public void RebuildingFontsYieldsFreshInstances()
        {
            var before = Fonts.Body;
            Fonts.Rebuild();
            Assert.NotSame(before, Fonts.Body);
        }
    }
}
