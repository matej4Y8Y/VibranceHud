using System;
using System.Drawing;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class ThemeTests
    {
        private static double Brightness(Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;

        private static int ChannelSpread(Color c) =>
            Math.Max(Math.Max(c.R, c.G), c.B) - Math.Min(Math.Min(c.R, c.G), c.B);

        [Fact]
        public void Dark_IsLightTextOnDarkBackground_WithColoredAccent()
        {
            Theme.Apply("Violet");

            Assert.False(Theme.IsLight);
            Assert.True(Brightness(Theme.Background) < Brightness(Theme.Text));
            Assert.True(ChannelSpread(Theme.Accent) > 60); // violet is colorful
        }

        [Fact]
        public void Light_IsDarkTextOnLightBackground_AndMonochrome()
        {
            Theme.Apply("Light");
            try
            {
                Assert.True(Theme.IsLight);
                Assert.True(Brightness(Theme.Background) > Brightness(Theme.Text));
                // Black & white: accent and plexus are near-greyscale (channels close together).
                Assert.True(ChannelSpread(Theme.Accent) < 30);
                Assert.True(ChannelSpread(Theme.PlexusNodeA) < 30);
                Assert.True(ChannelSpread(Theme.PlexusLine) < 30);
            }
            finally
            {
                Theme.Apply("Violet"); // restore the default for any other tests
            }
        }

        [Fact]
        public void Apply_ByName_SwitchesTheAccentColour()
        {
            Theme.Apply("Emerald");
            try
            {
                Assert.Equal("Emerald", Theme.CurrentName);
                Assert.True(Theme.Accent.G > Theme.Accent.R); // green-dominant
            }
            finally
            {
                Theme.Apply("Violet");
            }
        }
    }
}
