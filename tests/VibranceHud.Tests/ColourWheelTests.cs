using System.Drawing;
using System.Windows.Forms;
using VibranceHud.Controls;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The colour wheel's maths and its keyboard.
    ///
    /// The conversions get their own tests because a wheel that hands back a slightly wrong
    /// colour is close to impossible to catch by eye - it still looks like a plausible colour,
    /// just not the one under the cursor.
    ///
    /// Painting is not tested here; ColourWheel is registered in ControlPaintTests instead, so
    /// it is rendered focused, in every theme and at degenerate sizes alongside every other
    /// owner-drawn control.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class ColourWheelTests
    {
        // ---- conversions -----------------------------------------------------------------

        [Theory]
        [InlineData(0, 255, 0, 0)]      // red
        [InlineData(120, 0, 255, 0)]    // green
        [InlineData(240, 0, 0, 255)]    // blue
        [InlineData(60, 255, 255, 0)]   // yellow
        [InlineData(180, 0, 255, 255)]  // cyan
        [InlineData(300, 255, 0, 255)]  // magenta
        public void TheHueCornersAreTheColoursTheyShouldBe(float hue, int r, int g, int b)
        {
            var colour = ColourWheel.FromHsv(hue, 1f, 1f);

            Assert.Equal(Color.FromArgb(255, r, g, b), colour);
        }

        [Fact]
        public void NoSaturationIsGreyRegardlessOfHue()
        {
            for (float hue = 0; hue < 360; hue += 37)
                Assert.Equal(Color.FromArgb(255, 255, 255, 255), ColourWheel.FromHsv(hue, 0f, 1f));
        }

        [Fact]
        public void NoBrightnessIsBlackRegardlessOfEverythingElse()
        {
            Assert.Equal(Color.FromArgb(255, 0, 0, 0), ColourWheel.FromHsv(200f, 1f, 0f));
        }

        /// <summary>
        /// Every hue has to survive the trip out to RGB and back, because that round trip is
        /// exactly what happens when a saved crosshair is reloaded into the picker.
        /// </summary>
        [Fact]
        public void EveryHueSurvivesTheRoundTrip()
        {
            for (float expected = 0; expected < 360; expected += 1)
            {
                var colour = ColourWheel.FromHsv(expected, 1f, 1f);
                ColourWheel.ToHsv(colour, out float actual, out _, out _);

                // 8-bit channels cannot express every degree exactly; a degree of slack is the
                // quantisation, not a mistake.
                float drift = System.Math.Abs(actual - expected);
                if (drift > 180) drift = 360 - drift;   // 359.6 and 0.1 are neighbours

                Assert.True(drift <= 1.5f, $"hue {expected} came back as {actual}");
            }
        }

        [Theory]
        [InlineData(255, 0, 102)]
        [InlineData(0, 255, 102)]
        [InlineData(18, 200, 240)]
        [InlineData(255, 220, 0)]
        [InlineData(7, 7, 9)]
        public void AColourGoesInAndTheSameColourComesOut(int r, int g, int b)
        {
            using var wheel = new ColourWheel { Colour = Color.FromArgb(255, r, g, b) };

            var back = wheel.Colour;

            Assert.InRange(back.R, r - 1, r + 1);
            Assert.InRange(back.G, g - 1, g + 1);
            Assert.InRange(back.B, b - 1, b + 1);
        }

        [Fact]
        public void HueWrapsRatherThanRunningOffTheEnd()
        {
            Assert.Equal(ColourWheel.FromHsv(10f, 1f, 1f), ColourWheel.FromHsv(370f, 1f, 1f));
            Assert.Equal(ColourWheel.FromHsv(350f, 1f, 1f), ColourWheel.FromHsv(-10f, 1f, 1f));
        }

        // ---- hex -------------------------------------------------------------------------

        [Fact]
        public void HexIsSixDigitsWithNoHash()
        {
            Assert.Equal("00FF66", ColourWheel.ToHex(Color.FromArgb(255, 0, 255, 102)));
            Assert.Equal("000000", ColourWheel.ToHex(Color.Black));
            Assert.Equal("FFFFFF", ColourWheel.ToHex(Color.White));
        }

        /// <summary>Alpha is the opacity slider's, so it must not ride along in the hex - or
        /// pasting a friend's colour would quietly change how transparent your crosshair is.</summary>
        [Fact]
        public void HexCarriesNoAlpha()
        {
            Assert.Equal("00FF66", ColourWheel.ToHex(Color.FromArgb(40, 0, 255, 102)));
        }

        [Theory]
        [InlineData("00FF66")]
        [InlineData("#00FF66")]
        [InlineData("00ff66")]
        [InlineData("  #00ff66  ")]
        [InlineData("0F6")]
        [InlineData("#0f6")]
        public void EveryReasonableWayOfWritingAColourIsAccepted(string text)
        {
            Assert.True(ColourWheel.TryParseHex(text, out var colour), $"rejected '{text}'");
            Assert.Equal(Color.FromArgb(255, 0, 255, 102), colour);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("#")]
        [InlineData("00FF6")]      // five digits - mid-type
        [InlineData("00FF666")]    // seven
        [InlineData("GGHHII")]     // right length, not hex
        [InlineData("00 F66")]
        [InlineData("rebeccapurple")]
        public void AnythingElseIsRefusedRatherThanGuessedAt(string? text)
        {
            Assert.False(ColourWheel.TryParseHex(text, out _), $"accepted '{text}'");
        }

        [Fact]
        public void HexSurvivesTheRoundTrip()
        {
            foreach (var original in new[]
            {
                Color.FromArgb(255, 0, 255, 102), Color.FromArgb(255, 255, 220, 0),
                Color.FromArgb(255, 18, 200, 240), Color.FromArgb(255, 1, 2, 3),
            })
            {
                Assert.True(ColourWheel.TryParseHex(ColourWheel.ToHex(original), out var back));
                Assert.Equal(original, back);
            }
        }

        // ---- keyboard --------------------------------------------------------------------

        /// <summary>
        /// The regression test for a bug that made the whole keyboard path dead code.
        ///
        /// OnKeyDown handled the arrows, but WinForms never delivered them: arrows are dialog
        /// navigation keys unless a control claims them through IsInputKey, so focusing the
        /// wheel and pressing Right moved focus to the next control instead. Everything looked
        /// correct in the source and nothing worked on screen.
        /// </summary>
        [Theory]
        [InlineData(Keys.Left)]
        [InlineData(Keys.Right)]
        [InlineData(Keys.Up)]
        [InlineData(Keys.Down)]
        [InlineData(Keys.PageUp)]
        [InlineData(Keys.PageDown)]
        public void TheWheelClaimsTheKeysItHandles(Keys key)
        {
            using var wheel = new ColourWheel();

            Assert.True(wheel.TestClaimsKey(key),
                $"{key} is left to dialog navigation, so OnKeyDown will never see it");
        }

        /// <summary>Claiming everything would trap focus on the wheel - Tab still has to leave.</summary>
        [Fact]
        public void TabIsLeftAloneSoFocusCanEscape()
        {
            using var wheel = new ColourWheel();

            Assert.False(wheel.TestClaimsKey(Keys.Tab));
        }

        [Fact]
        public void TheArrowsMoveTheHueInOppositeDirections()
        {
            using var wheel = new ColourWheel { Colour = Color.FromArgb(255, 0, 255, 102) };

            var start = wheel.Colour;
            wheel.TestPressKey(Keys.Right);
            var right = wheel.Colour;

            Assert.NotEqual(start, right);

            wheel.TestPressKey(Keys.Left);
            Assert.Equal(start, wheel.Colour);
        }

        [Fact]
        public void UpBrightensAndDownDarkens()
        {
            using var wheel = new ColourWheel { Colour = Color.FromArgb(255, 120, 60, 30) };

            int before = Brightness(wheel.Colour);
            wheel.TestPressKey(Keys.Up);
            Assert.True(Brightness(wheel.Colour) > before, "up did not brighten the colour");

            wheel.TestPressKey(Keys.Down);
            wheel.TestPressKey(Keys.Down);
            Assert.True(Brightness(wheel.Colour) < before, "down did not darken the colour");
        }

        [Fact]
        public void BrightnessStopsAtBlackAndAtFull()
        {
            using var wheel = new ColourWheel { Colour = Color.FromArgb(255, 0, 255, 102) };

            for (int i = 0; i < 60; i++) wheel.TestPressKey(Keys.Down);
            Assert.Equal(Color.FromArgb(255, 0, 0, 0), wheel.Colour);

            for (int i = 0; i < 60; i++) wheel.TestPressKey(Keys.Up);
            Assert.Equal(Color.FromArgb(255, 0, 255, 102), wheel.Colour);
        }

        [Fact]
        public void AKeyPressAnnouncesTheNewColour()
        {
            using var wheel = new ColourWheel();
            int raised = 0;
            wheel.ColourChanged += (_, _) => raised++;

            wheel.TestPressKey(Keys.Right);

            Assert.Equal(1, raised);
        }

        [Fact]
        public void AKeyTheWheelDoesNotUseChangesNothing()
        {
            using var wheel = new ColourWheel { Colour = Color.FromArgb(255, 0, 255, 102) };
            int raised = 0;
            wheel.ColourChanged += (_, _) => raised++;

            wheel.TestPressKey(Keys.A);

            Assert.Equal(0, raised);
            Assert.Equal(Color.FromArgb(255, 0, 255, 102), wheel.Colour);
        }

        /// <summary>
        /// Setting the colour from outside must stay silent.
        ///
        /// The crosshair page sets this whenever a swatch is clicked. If that raised
        /// ColourChanged the page would immediately write the wheel's colour back over the
        /// swatch's, and clicking a swatch would land on a colour slightly beside the one
        /// printed on it.
        /// </summary>
        [Fact]
        public void SettingTheColourFromOutsideDoesNotAnnounceIt()
        {
            using var wheel = new ColourWheel();
            int raised = 0;
            wheel.ColourChanged += (_, _) => raised++;

            wheel.Colour = Color.FromArgb(255, 255, 0, 170);

            Assert.Equal(0, raised);
        }

        private static int Brightness(Color c) => System.Math.Max(c.R, System.Math.Max(c.G, c.B));
    }
}
