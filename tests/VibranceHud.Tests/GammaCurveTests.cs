using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class GammaCurveTests
    {
        [Fact]
        public void Build_ReturnsThreeChannelsOf256()
        {
            Assert.Equal(768, GammaCurve.Build(1f).Length);
        }

        [Fact]
        public void Gamma1_IsLinear()
        {
            var ramp = GammaCurve.Build(1f);

            for (int i = 0; i < 256; i++)
            {
                ushort expected = (ushort)(i * 257); // 0 -> 0, 255 -> 65535
                Assert.Equal(expected, ramp[i]);
                Assert.Equal(expected, ramp[256 + i]);
                Assert.Equal(expected, ramp[512 + i]);
            }
        }

        [Fact]
        public void EndpointsAreAlwaysBlackAndWhite()
        {
            foreach (var g in new[] { 0.5f, 1f, 1.8f })
            {
                var ramp = GammaCurve.Build(g);
                for (int ch = 0; ch < 3; ch++)
                {
                    Assert.Equal(0, ramp[ch * 256]);
                    Assert.Equal(65535, ramp[ch * 256 + 255]);
                }
            }
        }

        [Fact]
        public void HigherGamma_LiftsMidtones_LowerGammaDeepensThem()
        {
            var dark = GammaCurve.Build(0.7f);
            var neutral = GammaCurve.Build(1f);
            var bright = GammaCurve.Build(1.5f);

            Assert.True(bright[128] > neutral[128]);
            Assert.True(dark[128] < neutral[128]);
        }

        [Fact]
        public void RampIsMonotonic()
        {
            foreach (var g in new[] { 0.6f, 1f, 1.6f })
            {
                var ramp = GammaCurve.Build(g);
                for (int ch = 0; ch < 3; ch++)
                    for (int i = 1; i < 256; i++)
                        Assert.True(ramp[ch * 256 + i] >= ramp[ch * 256 + i - 1]);
            }
        }

        [Fact]
        public void AllChannelsMatch_SoGammaDoesNotTintTheScreen()
        {
            var ramp = GammaCurve.Build(1.4f);

            for (int i = 0; i < 256; i++)
            {
                Assert.Equal(ramp[i], ramp[256 + i]);
                Assert.Equal(ramp[i], ramp[512 + i]);
            }
        }
    }
}
