// Reported by a user: on step 2 of onboarding, "Start PlexusX when Windows starts" was
// invisible - a toggle with no label, so you couldn't tell what you were turning on. It only
// happened after picking a dark theme on step 1.
//
// Cause: stock WinForms controls keep whatever colour they were constructed with. The
// owner-drawn controls here sample Theme inside OnPaint and follow a theme change for free;
// Labels, LinkLabels and Buttons don't. Since step 1 IS a theme picker, those were left
// painting the palette the form opened with - dark text on a now-dark background.
//
// These assert readability rather than a specific colour value, so they'd catch the same class
// of bug in any theme, including ones added later.

using System.Drawing;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class OnboardingThemeTests
    {
        /// <summary>Rough perceptual brightness, 0-255. Enough to tell "readable" from
        /// "invisible" without pulling in a colour-science dependency.</summary>
        private static double Luminance(Color c) => 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;

        private static double Contrast(Color a, Color b) => System.Math.Abs(Luminance(a) - Luminance(b));

        /// <summary>The exact reported bug: text the same brightness as what's behind it.</summary>
        [Theory]
        [InlineData("Violet")]
        [InlineData("Emerald")]
        [InlineData("Crimson")]
        [InlineData("Light")]
        public void BodyText_IsReadableAgainstTheBackground_InEveryTheme(string themeName)
        {
            Theme.Apply(themeName);

            double contrast = Contrast(Theme.Text, Theme.Background);

            Assert.True(contrast > 40,
                $"'{themeName}': body text (lum {Luminance(Theme.Text):F0}) is too close to the " +
                $"background (lum {Luminance(Theme.Background):F0}) - contrast {contrast:F0}. " +
                "This is what made the onboarding label invisible.");
        }

        /// <summary>Dimmed text is used for hints and links; it's allowed to be quieter than
        /// body text but still has to be legible.</summary>
        [Theory]
        [InlineData("Violet")]
        [InlineData("Emerald")]
        [InlineData("Crimson")]
        [InlineData("Light")]
        public void DimText_IsStillLegible_InEveryTheme(string themeName)
        {
            Theme.Apply(themeName);

            Assert.True(Contrast(Theme.TextDim, Theme.Background) > 20,
                $"'{themeName}': dim text is indistinguishable from the background.");
        }

        /// <summary>Themes must differ in background brightness - otherwise a control that
        /// cached one theme's text colour would coincidentally still be readable in another,
        /// and this whole class of bug would go unnoticed until a user hit it.</summary>
        [Fact]
        public void LightAndDarkThemes_HaveGenuinelyDifferentBackgrounds()
        {
            Theme.Apply("Light");
            var light = Theme.Background;
            Theme.Apply("Violet");
            var dark = Theme.Background;

            Assert.True(Contrast(light, dark) > 80,
                "Light and Violet backgrounds are too similar for the light/dark split to mean anything.");
        }

        /// <summary>The concrete failure mode: hold onto one theme's text colour, switch theme,
        /// and it becomes unreadable. Proves the bug is real and why re-applying is required.</summary>
        [Fact]
        public void TextColourFromOneTheme_IsUnreadableInTheOther()
        {
            Theme.Apply("Light");
            var lightThemeText = Theme.Text;

            Theme.Apply("Violet");
            var darkBackground = Theme.Background;

            Assert.True(Contrast(lightThemeText, darkBackground) < 40,
                "expected the light theme's text to be unreadable on a dark background - if this " +
                "fails the palettes changed and the reasoning behind ReapplyThemeColors needs a look");
        }
    }
}
