using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using VibranceHud.Theming;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The rule is "the dominant colour of the image" - but taken literally that fails:
    /// the most frequent pixel in a night-time screenshot is near-black and in a snow
    /// clip it's near-white, and either would be invisible as an accent on the matte
    /// black UI. So it's the most frequent colour that can actually serve as an accent.
    /// </summary>
    public class ImagePaletteTests
    {
        private static readonly Color Fallback = Color.FromArgb(167, 139, 250); // Violet

        private static Color[] Pixels(params (Color Colour, int Count)[] parts)
            => parts.SelectMany(p => Enumerable.Repeat(p.Colour, p.Count)).ToArray();

        [Fact]
        public void Extract_PicksTheMostCommonColour()
        {
            // Mostly orange, some blue.
            var px = Pixels((Color.FromArgb(220, 120, 40), 300),
                            (Color.FromArgb(60, 110, 220), 100));

            var theme = ImagePalette.Extract(px, Fallback);

            Assert.True(theme.Accent.R > theme.Accent.G, "expected an orange-ish accent");
            Assert.True(theme.Accent.G > theme.Accent.B, "expected an orange-ish accent");
        }

        [Fact]
        public void Extract_IgnoresDarkPixels_EvenWhenTheyDominate()
        {
            // A night scene: mostly near-black, with a small bright orange fire.
            var px = Pixels((Color.FromArgb(8, 9, 12), 900),
                            (Color.FromArgb(230, 130, 40), 100));

            var theme = ImagePalette.Extract(px, Fallback);

            Assert.True(theme.Accent.R > theme.Accent.B, "the fire should win, not the night");
            Assert.True(Brightness(theme.Accent) > 0.4f, "accent must be visible on matte black");
        }

        [Fact]
        public void Extract_IgnoresGreyPixels_EvenWhenTheyDominate()
        {
            // A snow scene: mostly washed-out grey, with a little saturated green.
            var px = Pixels((Color.FromArgb(205, 208, 212), 900),
                            (Color.FromArgb(40, 200, 90), 100));

            var theme = ImagePalette.Extract(px, Fallback);

            Assert.True(theme.Accent.G > theme.Accent.R, "the green should win, not the snow");
            Assert.True(theme.Accent.G > theme.Accent.B);
        }

        [Fact]
        public void Extract_GreyscaleImage_GoesNeutral_NotSomeUnrelatedColour()
        {
            // A black-and-white wallpaper has no colour to borrow. Falling back to the
            // default violet would clash badly with the picture, so it goes silver instead.
            var px = Pixels((Color.FromArgb(20, 20, 20), 200),
                            (Color.FromArgb(128, 128, 128), 200),
                            (Color.FromArgb(240, 240, 240), 200));

            var theme = ImagePalette.Extract(px, Fallback);

            Assert.True(Saturation(theme.Accent) < 0.12f, "a mono image must give a mono accent");
            Assert.True(Brightness(theme.Accent) > 0.5f, "and it still has to be visible");
        }

        [Fact]
        public void Extract_EmptyInput_FallsBack()
        {
            var theme = ImagePalette.Extract(new Color[0], Fallback);

            Assert.Equal(Fallback, theme.Accent);
        }

        // ---- The whole surface palette is tinted by the image, not just the accent ----

        [Fact]
        public void Extract_TintsSurfacesTowardTheImageHue()
        {
            var blue = ImagePalette.Extract(Pixels((Color.FromArgb(60, 110, 220), 400)), Fallback);
            var orange = ImagePalette.Extract(Pixels((Color.FromArgb(220, 120, 40), 400)), Fallback);

            // A blue picture gives a cool-tinted shell, an orange one a warm-tinted shell.
            Assert.True(blue.Background.B > blue.Background.R);
            Assert.True(orange.Background.R > orange.Background.B);
        }

        [Fact]
        public void Extract_SurfacesStayDarkEnoughForWhiteText()
        {
            // Whatever the image, the shell must stay dark - the text colour doesn't move,
            // so this is what actually guarantees the UI stays readable.
            foreach (var c in new[] { Color.FromArgb(255, 240, 0), Color.White,
                                      Color.FromArgb(0, 200, 255), Color.Black })
            {
                var t = ImagePalette.Extract(Pixels((c, 400)), Fallback);

                Assert.True(Brightness(t.Background) < 0.16f, $"background too bright for {c}");
                Assert.True(Brightness(t.Surface) < 0.26f, $"surface too bright for {c}");
                Assert.True(Brightness(t.Surface) > Brightness(t.Background),
                    "cards still have to lift off the background");
            }
        }

        [Fact]
        public void Extract_BorderSitsBetweenSurfaceAndText()
        {
            var t = ImagePalette.Extract(Pixels((Color.FromArgb(220, 120, 40), 400)), Fallback);

            Assert.True(Brightness(t.Border) > Brightness(t.Surface));
        }

        private static float Saturation(Color c)
        {
            float max = Math.Max(c.R, Math.Max(c.G, c.B)) / 255f;
            float min = Math.Min(c.R, Math.Min(c.G, c.B)) / 255f;
            return max <= 0f ? 0f : (max - min) / max;
        }

        [Fact]
        public void Extract_LiftsAnAccentThatWouldBeTooDarkToSee()
        {
            // Saturated but very dim - passes the saturation test, fails on visibility.
            var px = Pixels((Color.FromArgb(70, 20, 20), 500));

            var theme = ImagePalette.Extract(px, Fallback);

            Assert.True(Brightness(theme.Accent) >= ImagePalette.MinAccentBrightness,
                "a dim source colour must be lifted until it reads on matte black");
        }

        [Fact]
        public void Extract_DerivedColoursAreDistinct()
        {
            var px = Pixels((Color.FromArgb(220, 120, 40), 500));

            var t = ImagePalette.Extract(px, Fallback);

            // AccentDim is a darker accent; NodeB is hue-shifted off NodeA.
            Assert.True(Brightness(t.AccentDim) < Brightness(t.Accent));
            Assert.Equal(t.Accent, t.NodeA);
            Assert.NotEqual(t.NodeA, t.NodeB);
            Assert.NotEqual(t.NodeA, t.Line);
        }

        [Fact]
        public void SuggestedDim_IsHigherForABrighterImage()
        {
            var dark = ImagePalette.Extract(Pixels((Color.FromArgb(18, 18, 22), 500)), Fallback);
            var bright = ImagePalette.Extract(Pixels((Color.FromArgb(240, 240, 245), 500)), Fallback);

            Assert.True(bright.SuggestedDim > dark.SuggestedDim,
                "a bright wallpaper has to be dimmed harder than a dark one");
        }

        [Fact]
        public void SuggestedDim_StaysWithinTheSliderRange()
        {
            foreach (var shade in new[] { 0, 40, 128, 200, 255 })
            {
                var t = ImagePalette.Extract(
                    Pixels((Color.FromArgb(shade, shade, shade), 100)), Fallback);

                Assert.InRange(t.SuggestedDim, ImagePalette.MinDim, ImagePalette.MaxDim);
            }
        }

        private static float Brightness(Color c)
            => (0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B) / 255f;
    }
}
