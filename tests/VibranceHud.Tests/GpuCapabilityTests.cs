using VibranceHud.Nvidia;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The Rust audience runs a wide spread of cards, plenty of them GTX 16-series and
    /// older. A tweak that silently does nothing on those is worse than no tweak at all,
    /// so the card only ever offers what the detected GPU can actually do.
    /// </summary>
    public class GpuCapabilityTests
    {
        [Theory]
        [InlineData("NVIDIA GeForce RTX 4070", GpuTier.Rtx40)]
        [InlineData("NVIDIA GeForce RTX 5080", GpuTier.Rtx40)]
        [InlineData("NVIDIA GeForce RTX 4090 Laptop GPU", GpuTier.Rtx40)]
        public void RecognisesRtx40AndNewer(string name, GpuTier expected)
            => Assert.Equal(expected, GpuCapability.FromName(name));

        [Theory]
        [InlineData("NVIDIA GeForce RTX 3060 Ti", GpuTier.Rtx)]
        [InlineData("NVIDIA GeForce RTX 2070 SUPER", GpuTier.Rtx)]
        public void RecognisesOlderRtx(string name, GpuTier expected)
            => Assert.Equal(expected, GpuCapability.FromName(name));

        [Theory]
        [InlineData("NVIDIA GeForce GTX 1660 SUPER", GpuTier.Gtx)]
        [InlineData("NVIDIA GeForce GTX 1080 Ti", GpuTier.Gtx)]
        [InlineData("NVIDIA GeForce GTX 970", GpuTier.Gtx)]
        public void RecognisesGtx(string name, GpuTier expected)
            => Assert.Equal(expected, GpuCapability.FromName(name));

        [Theory]
        [InlineData("AMD Radeon RX 7800 XT")]
        [InlineData("Intel Arc A770")]
        [InlineData("")]
        [InlineData(null)]
        public void NonNvidiaOrUnknown_IsNone(string? name)
            => Assert.Equal(GpuTier.None, GpuCapability.FromName(name));

        [Fact]
        public void CatalogHidesEverythingWithoutAnNvidiaGpu()
        {
            Assert.Empty(NvidiaTweakCatalog.Available(GpuTier.None));
        }

        [Fact]
        public void EveryTweakInTheCatalogRunsOnAPlainGtxCard()
        {
            // Nothing currently shipped requires RTX. If an RTX-only tweak is added
            // later this test is the reminder to gate it, not to quietly ship it.
            var gtx = NvidiaTweakCatalog.Available(GpuTier.Gtx);

            Assert.NotEmpty(gtx);
            Assert.Equal(NvidiaTweakCatalog.All.Count, gtx.Count);
        }

        [Fact]
        public void BetterCardsNeverGetFewerTweaksThanWorseOnes()
        {
            int gtx = NvidiaTweakCatalog.Available(GpuTier.Gtx).Count;
            int rtx = NvidiaTweakCatalog.Available(GpuTier.Rtx).Count;
            int rtx40 = NvidiaTweakCatalog.Available(GpuTier.Rtx40).Count;

            Assert.True(rtx >= gtx);
            Assert.True(rtx40 >= rtx);
        }

        [Fact]
        public void EveryTweakHasAnIdLabelAndExplanation()
        {
            foreach (var t in NvidiaTweakCatalog.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(t.Id));
                Assert.False(string.IsNullOrWhiteSpace(t.Label));
                Assert.False(string.IsNullOrWhiteSpace(t.Description));
            }
        }

        [Fact]
        public void TweakIdsAreUnique()
        {
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var t in NvidiaTweakCatalog.All)
                Assert.True(ids.Add(t.Id), $"duplicate id: {t.Id}");
        }
    }
}
