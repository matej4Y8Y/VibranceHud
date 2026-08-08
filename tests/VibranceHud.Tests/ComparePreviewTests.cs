using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The compare preview, at the engine level.
    ///
    /// This is the property that actually matters and that the AbCompare tests do not cover:
    /// AbCompare holds no colour values at all, so every one of its tests still passed while
    /// the restore path was capable of losing the user's settings permanently.
    ///
    /// The rule is that a preview changes what is APPLIED and nothing else. Everything the
    /// engine reports must keep describing the user's own look, because a page rebuilt during
    /// a preview - a theme switch does exactly that - seeds itself from these values and then
    /// saves them.
    /// </summary>
    public sealed class ComparePreviewTests
    {
        private sealed class Controller : IVibranceController
        {
            public int CurrentLevel { get; set; } = 50;
            public int DefaultLevel => 50;
            public bool IsAvailable => true;
            public void SetLevel(int level) => CurrentLevel = level;
        }

        private sealed class Overlay : ISaturationOverlay
        {
            public float[]? Last { get; private set; }
            public bool Cleared { get; private set; }
            public void Apply(float[] matrix) { Last = matrix; Cleared = false; }
            public void Clear() { Last = null; Cleared = true; }
        }

        private sealed class Gamma : IGammaRamp
        {
            public bool IsAvailable => true;
            public ushort[]? Last { get; private set; }
            public bool WasReset { get; private set; }
            public void Apply(ushort[] ramp) { Last = ramp; WasReset = false; }
            public void Reset() { Last = null; WasReset = true; }
        }

        private static (VibranceEngine Engine, Overlay Overlay, Gamma Gamma) Build()
        {
            var overlay = new Overlay();
            var gamma = new Gamma();
            var engine = new VibranceEngine(new Controller(), overlay, gamma)
            {
                Saturation = 130,
                Vibrance = 80,
                Brightness = 104,
                Contrast = 106,
                Temperature = -6,
            };
            engine.Tone = engine.Tone with { Shadows = 20 };
            return (engine, overlay, gamma);
        }

        /// <summary>
        /// The whole point. Previewing must not disturb a single reported value - the sliders,
        /// the save path and every other surface read these.
        /// </summary>
        [Fact]
        public void PreviewingNeutralDoesNotChangeAnyReportedValue()
        {
            var (engine, _, _) = Build();

            engine.PreviewNeutral(true);

            Assert.Equal(130, engine.Saturation);
            Assert.Equal(80, engine.Vibrance);
            Assert.Equal(104, engine.Brightness);
            Assert.Equal(106, engine.Contrast);
            Assert.Equal(-6, engine.Temperature);
            Assert.Equal(20, engine.Tone.Shadows);
        }

        [Fact]
        public void PreviewingNeutralClearsWhatIsOnScreen()
        {
            var (engine, overlay, gamma) = Build();

            engine.PreviewNeutral(true);

            Assert.True(overlay.Cleared);
            Assert.True(gamma.WasReset);
        }

        [Fact]
        public void EndingThePreviewPutsTheLookBackOnScreen()
        {
            var (engine, overlay, gamma) = Build();

            engine.PreviewNeutral(true);
            engine.PreviewNeutral(false);

            Assert.NotNull(overlay.Last);
            Assert.NotNull(gamma.Last);
        }

        [Fact]
        public void ItReportsWhetherAPreviewIsRunning()
        {
            var (engine, _, _) = Build();

            Assert.False(engine.IsPreviewingNeutral);
            engine.PreviewNeutral(true);
            Assert.True(engine.IsPreviewingNeutral);
            engine.PreviewNeutral(false);
            Assert.False(engine.IsPreviewingNeutral);
        }

        /// <summary>
        /// The data-loss scenario, written out.
        ///
        /// Start a preview, then simulate what a theme switch does: build a brand new page
        /// from the engine's reported values while the preview is still running. Those values
        /// have to be the user's look, or the new page seeds itself with neutral and the first
        /// save writes it over their settings permanently.
        /// </summary>
        [Fact]
        public void APageRebuiltDuringAPreviewStillSeesTheUsersLook()
        {
            var (engine, _, _) = Build();

            engine.PreviewNeutral(true);

            // Exactly what VibrancePage's constructor reads.
            var seeded = new
            {
                engine.Saturation,
                engine.Vibrance,
                engine.Brightness,
                engine.Contrast,
                engine.Temperature,
                engine.Tone,
            };

            Assert.Equal(130, seeded.Saturation);
            Assert.Equal(80, seeded.Vibrance);
            Assert.Equal(20, seeded.Tone.Shadows);
        }

        /// <summary>Changing a value mid-preview must be kept, and must show up once the
        /// preview ends rather than being reverted by it.</summary>
        [Fact]
        public void AnEditDuringAPreviewSurvivesTheEndOfIt()
        {
            var (engine, _, _) = Build();

            engine.PreviewNeutral(true);
            engine.Saturation = 155;
            engine.PreviewNeutral(false);

            Assert.Equal(155, engine.Saturation);
        }

        [Fact]
        public void TurningThePreviewOnTwiceIsHarmless()
        {
            var (engine, _, _) = Build();

            engine.PreviewNeutral(true);
            engine.PreviewNeutral(true);
            engine.PreviewNeutral(false);

            Assert.False(engine.IsPreviewingNeutral);
            Assert.Equal(130, engine.Saturation);
        }
    }
}
