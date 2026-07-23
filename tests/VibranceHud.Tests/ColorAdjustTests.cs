using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class ColorAdjustTests
    {
        private static float At(float[] m, int row, int col) => m[row * 5 + col];

        [Fact]
        public void Neutral_IsIdentity()
        {
            Assert.True(ColorAdjust.IsIdentity(1f, 1f, 0f));

            var m = ColorAdjust.Build(1f, 1f, 0f);
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 5; col++)
                    Assert.Equal(row == col ? 1f : 0f, At(m, row, col), 4);
        }

        [Fact]
        public void AnyAdjustment_IsNotIdentity()
        {
            Assert.False(ColorAdjust.IsIdentity(1.5f, 1f, 0f));   // vibrance boost
            Assert.False(ColorAdjust.IsIdentity(1f, 0.8f, 0f));   // dimmed
            Assert.False(ColorAdjust.IsIdentity(1f, 1f, 0.5f));   // eye care on
        }

        [Fact]
        public void Brightness_ScalesEveryChannelEqually()
        {
            var m = ColorAdjust.Build(1f, 0.5f, 0f);

            Assert.Equal(0.5f, At(m, 0, 0), 4);
            Assert.Equal(0.5f, At(m, 1, 1), 4);
            Assert.Equal(0.5f, At(m, 2, 2), 4);
            // Alpha and the offset row are never scaled.
            Assert.Equal(1f, At(m, 3, 3), 4);
            Assert.Equal(1f, At(m, 4, 4), 4);
        }

        [Fact]
        public void EyeCare_CutsBlueMostAndLeavesRedAlone()
        {
            var m = ColorAdjust.Build(1f, 1f, 1f);

            Assert.Equal(1f, At(m, 0, 0), 4);                              // red untouched
            Assert.Equal(1f - ColorAdjust.WarmGreenCut, At(m, 1, 1), 4);   // green slightly down
            Assert.Equal(1f - ColorAdjust.WarmBlueCut, At(m, 2, 2), 4);    // blue down the most
            Assert.True(At(m, 2, 2) < At(m, 1, 1));
        }

        [Fact]
        public void EyeCare_ScalesWithWarmth()
        {
            var half = ColorAdjust.Build(1f, 1f, 0.5f);
            var full = ColorAdjust.Build(1f, 1f, 1f);

            // Half warmth cuts blue half as much as full warmth.
            Assert.Equal(1f - ColorAdjust.WarmBlueCut * 0.5f, At(half, 2, 2), 4);
            Assert.True(At(half, 2, 2) > At(full, 2, 2));
        }

        [Fact]
        public void SaturationOnly_MatchesSaturationMatrix()
        {
            var expected = SaturationMatrix.Build(2f);
            var actual = ColorAdjust.Build(2f, 1f, 0f);

            for (int i = 0; i < 25; i++)
                Assert.Equal(expected[i], actual[i], 4);
        }

        [Fact]
        public void Combined_AppliesSaturationThenGains()
        {
            const float sat = 1.6f, bright = 0.75f, warm = 1f;
            var s = SaturationMatrix.Build(sat);
            var m = ColorAdjust.Build(sat, bright, warm);

            float gR = bright;
            float gG = bright * (1f - ColorAdjust.WarmGreenCut);
            float gB = bright * (1f - ColorAdjust.WarmBlueCut);

            for (int row = 0; row < 3; row++)
            {
                Assert.Equal(At(s, row, 0) * gR, At(m, row, 0), 4);
                Assert.Equal(At(s, row, 1) * gG, At(m, row, 1), 4);
                Assert.Equal(At(s, row, 2) * gB, At(m, row, 2), 4);
            }
        }
    }
}
