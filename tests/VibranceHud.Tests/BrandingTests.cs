using System;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class BrandingTests
    {
        [Theory]
        [InlineData(0, 2, 3, "v0.2.3")]
        [InlineData(1, 0, 0, "v1.0.0")]
        [InlineData(0, 10, 12, "v0.10.12")]
        public void FormatVersion_RendersVPrefixedMajorMinorBuild(int major, int minor, int build, string expected)
        {
            Assert.Equal(expected, AppInfo.FormatVersion(new Version(major, minor, build)));
        }

        [Fact]
        public void FormatVersion_TreatsMissingBuildAsZero()
        {
            Assert.Equal("v0.2.0", AppInfo.FormatVersion(new Version(0, 2)));
        }

        [Fact]
        public void LogoResourceName_PicksWhiteForDark_AndBlackForLight()
        {
            Assert.Equal("logo-horizontal-white.png", BrandAssets.LogoResourceName(light: false));
            Assert.Equal("logo-horizontal-black.png", BrandAssets.LogoResourceName(light: true));
        }
    }
}
