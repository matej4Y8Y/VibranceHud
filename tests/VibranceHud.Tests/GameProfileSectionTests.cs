using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// "Use my current look for this game", which replaced the Profile Editor page.
    ///
    /// The old page had its own sliders, so a per-game look had to be configured twice - once
    /// on Display and again in the editor. Nobody did that, which is why the feature went
    /// unused. Capturing the live engine instead means there is only ever one place to set a
    /// look, and these tests pin that the capture is complete: a profile that quietly drops
    /// contrast or the advanced grade would apply a different screen than the one the user
    /// was looking at when they pressed the button.
    /// </summary>
    public sealed class GameProfileSectionTests
    {
        [Fact]
        public void CaptureTakesEverythingTheDisplayPageCanSet()
        {
            var engine = new FakeEngine
            {
                Vibrance = 140,
                Saturation = 165,
                Brightness = 96,
                Gamma = 112,
                Contrast = 118,
                Temperature = -35,
                Tone = new ToneSettings(Gamma: 112, Highlights: 40, Shadows: -25, Fade: 15,
                                        HighlightTint: 60),
            };

            var profile = GameProfileSection.Capture("rust", "Rust", engine, existing: null);

            Assert.Equal("rust", profile.GameId);
            Assert.Equal("Rust", profile.DisplayName);
            Assert.Equal(140, profile.Vibrance);
            Assert.Equal(165, profile.Saturation);
            Assert.Equal(96, profile.Brightness);
            Assert.Equal(112, profile.Gamma);
            Assert.Equal(118, profile.ResolvedContrast);
            Assert.Equal(-35, profile.ResolvedTemperature);
            Assert.Equal(40, profile.ResolvedTone.Highlights);
            Assert.Equal(60, profile.ResolvedTone.HighlightTint);
        }

        /// <summary>
        /// A profile carries game-hub options too - graphics quality, FPS cap, tweaks. Those
        /// are set elsewhere, so pressing a colour button must not wipe them.
        /// </summary>
        [Fact]
        public void CapturePreservesTheGameHubHalfOfAnExistingProfile()
        {
            var existing = new GameProfile
            {
                GameId = "rust",
                GameHub = new GameHubOptions
                {
                    GraphicsQuality = "3",
                    FpsCap = 144,
                    EffectToggles = new[] { "shadows-off" },
                },
            };

            var profile = GameProfileSection.Capture("rust", "Rust", new FakeEngine(), existing);

            Assert.Equal("3", profile.GameHub.GraphicsQuality);
            Assert.Equal(144, profile.GameHub.FpsCap);
            Assert.Equal(new[] { "shadows-off" }, profile.GameHub.EffectToggles);
        }

        [Fact]
        public void CaptureWithNoExistingProfileStillGetsAGameHubObject()
        {
            var profile = GameProfileSection.Capture("cs2", "Counter-Strike 2",
                new FakeEngine(), existing: null);

            Assert.NotNull(profile.GameHub);
        }

        /// <summary>
        /// A profile saved before contrast, warmth and the grade existed reads as "not set"
        /// rather than as a deliberate zero. For contrast a zero would mean a grey screen.
        /// </summary>
        [Fact]
        public void AProfileFromBeforeTheseFieldsExistedResolvesToNeutral()
        {
            var old = new GameProfile { GameId = "rust", Vibrance = 130 };

            Assert.Equal(100, old.ResolvedContrast);
            Assert.Equal(0, old.ResolvedTemperature);
            Assert.True(old.ResolvedTone.IsGammaOnly);
        }

        // ---- the summary line ------------------------------------------------------------

        [Fact]
        public void DescribeNamesOnlyWhatIsActuallyDoingSomething()
        {
            var profile = new GameProfile
            {
                Saturation = 140, Vibrance = 100, Brightness = 100,
                Gamma = 100, Contrast = 106,
            };

            var text = GameProfileSection.Describe(profile);

            Assert.Contains("saturation 140", text);
            Assert.Contains("contrast 106", text);
            // Neutral values are noise and bury the one or two the user actually chose.
            Assert.DoesNotContain("vibrance", text);
            Assert.DoesNotContain("brightness", text);
            Assert.DoesNotContain("gamma", text);
        }

        [Fact]
        public void DescribeSaysWarmOrCoolRatherThanASignedNumber()
        {
            Assert.Contains("warm 30",
                GameProfileSection.Describe(new GameProfile { Temperature = 30 }));
            Assert.Contains("cool 45",
                GameProfileSection.Describe(new GameProfile { Temperature = -45 }));
        }

        [Fact]
        public void DescribeMentionsAnAdvancedGradeWithoutListingEveryField()
        {
            var graded = new GameProfile
            {
                Tone = new ToneSettings(Gamma: 100, Highlights: 40, ShadowTint: -30),
            };

            var text = GameProfileSection.Describe(graded);

            Assert.Contains("advanced colour", text);
            Assert.DoesNotContain("Highlights", text);
        }

        [Fact]
        public void DescribeHandlesAnEntirelyNeutralProfile()
        {
            var text = GameProfileSection.Describe(new GameProfile());

            Assert.Contains("neutral", text);
            Assert.DoesNotContain(",", text);
        }

        private sealed class FakeEngine : IVibranceEngine
        {
            public int Vibrance { get; set; } = 100;
            public int Saturation { get; set; } = 100;
            public int Brightness { get; set; } = 100;
            public int Gamma { get; set; } = 100;
            public int Contrast { get; set; } = 100;
            public int Temperature { get; set; }
            public ToneSettings Tone { get; set; } = ToneSettings.Neutral;

            public void BeginDrag() { }
            public void EndDrag() { }
            public void Reset() { }
            public void SuspendOverlay() { }
            public void ResumeOverlay() { }
        }
    }
}
