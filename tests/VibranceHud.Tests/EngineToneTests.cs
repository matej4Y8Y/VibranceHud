using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class EngineToneTests
    {
        /// <summary>
        /// Going back to neutral resets the ramp rather than pushing an identity one.
        /// Windows does not restore a gamma ramp when a process exits, so an identity ramp
        /// left applied is both pointless work and something that can be left behind.
        /// </summary>
        [Fact]
        public void ReturningToNeutralResetsTheRampRatherThanApplyingOne()
        {
            var ramp = new RecordingGammaRamp();
            var engine = TestEngine(ramp);

            engine.Tone = ToneSettings.Neutral with { Shadows = -50 };
            ramp.Clear();

            engine.Tone = ToneSettings.Neutral;

            Assert.Null(ramp.LastApplied);
            Assert.True(ramp.WasReset, "a neutral grade must reset, not push a redundant ramp");
        }

        /// <summary>Setting neutral on an already-neutral engine is a no-op - it must not
        /// issue a syscall to say nothing changed.</summary>
        [Fact]
        public void SettingNeutralOnAnUntouchedEngineDoesNothing()
        {
            var ramp = new RecordingGammaRamp();
            var engine = TestEngine(ramp);
            ramp.Clear();

            engine.Tone = ToneSettings.Neutral;

            Assert.Null(ramp.LastApplied);
            Assert.False(ramp.WasReset);
        }

        [Fact]
        public void NonNeutralToneAppliesARamp()
        {
            var ramp = new RecordingGammaRamp();
            var engine = TestEngine(ramp);
            ramp.Clear();

            engine.Tone = ToneSettings.Neutral with { Highlights = 50 };

            Assert.NotNull(ramp.LastApplied);
            Assert.Equal(GammaCurve.Entries * 3, ramp.LastApplied!.Length);
        }

        [Fact]
        public void TheLegacyGammaPropertyStillDrivesTheRamp()
        {
            var ramp = new RecordingGammaRamp();
            var engine = TestEngine(ramp);
            ramp.Clear();

            engine.Gamma = 130;

            Assert.NotNull(ramp.LastApplied);
        }

        /// <summary>Gamma and the grade are one thing; reading Tone must reflect whatever
        /// the standalone Gamma property was last set to.</summary>
        [Fact]
        public void GammaAndToneStayInAgreement()
        {
            var engine = TestEngine(new RecordingGammaRamp());

            engine.Gamma = 120;
            Assert.Equal(120, engine.Tone.Gamma);

            engine.Tone = engine.Tone with { Gamma = 90 };
            Assert.Equal(90, engine.Gamma);
        }

        [Fact]
        public void SettingTheSameToneTwiceDoesNotReapply()
        {
            var ramp = new RecordingGammaRamp();
            var engine = TestEngine(ramp);

            var graded = ToneSettings.Neutral with { Shadows = -40 };
            engine.Tone = graded;
            ramp.Clear();

            engine.Tone = graded;

            Assert.Null(ramp.LastApplied);
            Assert.False(ramp.WasReset);
        }

        /// <summary>
        /// A zero-initialised grade arrives from any settings file that predates advanced
        /// colour. It must read as neutral, not as gamma 0.
        /// </summary>
        [Fact]
        public void ADefaultGradeLeavesTheScreenAlone()
        {
            var ramp = new RecordingGammaRamp();
            var engine = TestEngine(ramp);

            engine.Tone = ToneSettings.Neutral with { Fade = 40 };
            ramp.Clear();

            // What a settings file predating advanced colour deserialises to.
            engine.Tone = default;

            Assert.True(ramp.WasReset, "a zero-initialised grade must mean 'do nothing'");
            Assert.Null(ramp.LastApplied);
            Assert.Equal(100, engine.Gamma);
        }

        [Fact]
        public void SettingsWithNoSavedGradeResolveToNeutral()
        {
            var settings = new AppSettings();
            Assert.True(settings.ResolvedTone.IsNeutral);
        }

        private static VibranceEngine TestEngine(IGammaRamp ramp) =>
            new(new Controller(), new Overlay(), ramp);

        private sealed class RecordingGammaRamp : IGammaRamp
        {
            public ushort[]? LastApplied { get; private set; }
            public bool WasReset { get; private set; }

            public void Apply(ushort[] ramp) => LastApplied = ramp;
            public void Reset() => WasReset = true;

            public void Clear() { LastApplied = null; WasReset = false; }
        }

        private sealed class Controller : IVibranceController
        {
            public int CurrentLevel { get; set; } = 50;
            public int DefaultLevel => 50;
            public bool IsAvailable => true;
            public void SetLevel(int level) => CurrentLevel = level;
        }

        private sealed class Overlay : ISaturationOverlay
        {
            public void Apply(float[] matrix) { }
            public void Clear() { }
        }
    }
}
