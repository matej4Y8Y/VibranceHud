using VibranceHud.Rust;
using Xunit;

namespace VibranceHud.Tests
{
    public class RustSystemBoostTests
    {
        [Fact]
        public void RamTiers_MapToIncreasingGcBuffers()
        {
            var tiers = RustSystemBoost.RamTiers;

            Assert.Equal(3, tiers.Length);
            for (int i = 1; i < tiers.Length; i++)
            {
                Assert.True(tiers[i].Gb > tiers[i - 1].Gb);
                Assert.True(tiers[i].GcBuffer > tiers[i - 1].GcBuffer);
            }
        }

        [Theory]
        [InlineData(1024, 0)]   // exactly the 8 GB tier
        [InlineData(2048, 1)]   // exactly the 16 GB tier
        [InlineData(4096, 2)]   // exactly the 32 GB+ tier
        [InlineData(4082, 2)]   // a real-world value lands on the nearest tier
        [InlineData(1100, 0)]
        public void TierIndexForBuffer_PicksNearestTier(int buffer, int expectedIndex)
        {
            Assert.Equal(expectedIndex, RustSystemBoost.TierIndexForBuffer(buffer));
        }

        [Fact]
        public void TierIndexForBuffer_HandlesOutOfRangeValues()
        {
            Assert.Equal(0, RustSystemBoost.TierIndexForBuffer(0));       // below the lowest
            Assert.Equal(2, RustSystemBoost.TierIndexForBuffer(99999));   // above the highest
        }
    }
}
