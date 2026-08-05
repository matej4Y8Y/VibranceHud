using System;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The colour maths is the one part of this app where being subtly wrong does not throw.
    /// It just makes everyone's screen slightly off and every shared code slightly wrong on
    /// the recipient's machine - so these tests are the specification, not a safety net.
    /// </summary>
    public sealed class ToneCurveTests
    {
        private const int N = GammaCurve.Entries;

        private static ushort R(ushort[] ramp, int i) => ramp[i];
        private static ushort G(ushort[] ramp, int i) => ramp[N + i];
        private static ushort B(ushort[] ramp, int i) => ramp[N * 2 + i];

        [Fact]
        public void NeutralProducesTheIdentityRamp()
        {
            var ramp = ToneCurve.Build(ToneSettings.Neutral);
            var identity = GammaCurve.Identity();

            Assert.Equal(identity.Length, ramp.Length);
            for (int i = 0; i < ramp.Length; i++)
                Assert.True(Math.Abs(ramp[i] - identity[i]) <= 1,
                    $"entry {i}: {ramp[i]} vs identity {identity[i]}");
        }

        [Fact]
        public void RampIsAlwaysTheRightShape()
        {
            var ramp = ToneCurve.Build(ToneSettings.Neutral with { Highlights = 60, Shadows = -40 });
            Assert.Equal(N * 3, ramp.Length);
        }

        [Fact]
        public void EveryEntryStaysInRangeAtTheExtremes()
        {
            var extreme = new ToneSettings(
                Gamma: 150, Highlights: 100, Shadows: -100, Whites: 100, Blacks: -100,
                Fade: 100, ShadowTint: -100, MidtoneTint: 100, HighlightTint: -100);

            foreach (var v in ToneCurve.Build(extreme))
                Assert.InRange(v, (ushort)0, (ushort)65535);
        }

        [Fact]
        public void OutOfRangeInputsAreClampedRatherThanExploding()
        {
            var silly = new ToneSettings(
                Gamma: 9999, Highlights: 9999, Shadows: -9999, Whites: 9999,
                Blacks: -9999, Fade: 9999, ShadowTint: 9999, MidtoneTint: -9999,
                HighlightTint: 9999);

            foreach (var v in ToneCurve.Build(silly))
                Assert.InRange(v, (ushort)0, (ushort)65535);
        }

        [Fact]
        public void RaisingHighlightsBrightensTheTopEnd()
        {
            var flat = ToneCurve.Build(ToneSettings.Neutral);
            var lifted = ToneCurve.Build(ToneSettings.Neutral with { Highlights = 80 });

            int top = N - 20;
            Assert.True(G(lifted, top) > G(flat, top), "highlights up must brighten near-white");
        }

        [Fact]
        public void LoweringShadowsDarkensTheBottomEnd()
        {
            var flat = ToneCurve.Build(ToneSettings.Neutral);
            var crushed = ToneCurve.Build(ToneSettings.Neutral with { Shadows = -80 });

            Assert.True(G(crushed, 24) < G(flat, 24), "shadows down must darken near-black");
        }

        /// <summary>
        /// Highlights must not quietly drag the shadows with them. This is what the region
        /// weights exist for, and it is the difference between a grading control and a
        /// second brightness slider.
        /// </summary>
        [Fact]
        public void HighlightsLeaveTheDeepShadowsAlone()
        {
            var flat = ToneCurve.Build(ToneSettings.Neutral);
            var lifted = ToneCurve.Build(ToneSettings.Neutral with { Highlights = 100 });

            Assert.True(Math.Abs(G(lifted, 4) - G(flat, 4)) < 600,
                "a highlights change moved the black end far more than it should");
        }

        [Fact]
        public void ShadowsLeaveTheBrightestHighlightsAlone()
        {
            var flat = ToneCurve.Build(ToneSettings.Neutral);
            var crushed = ToneCurve.Build(ToneSettings.Neutral with { Shadows = -100 });

            Assert.True(Math.Abs(G(crushed, N - 4) - G(flat, N - 4)) < 600,
                "a shadows change moved the white end far more than it should");
        }

        [Fact]
        public void FadeRaisesTheBlackPoint()
        {
            var flat = ToneCurve.Build(ToneSettings.Neutral);
            var faded = ToneCurve.Build(ToneSettings.Neutral with { Fade = 60 });

            Assert.True(G(faded, 0) > G(flat, 0), "fade must lift pure black off zero");
        }

        [Fact]
        public void WarmHighlightTintPushesRedAboveBlue()
        {
            var warm = ToneCurve.Build(ToneSettings.Neutral with { HighlightTint = 80 });
            int i = N - 16;

            Assert.True(R(warm, i) > B(warm, i), "a warm highlight tint must leave red above blue");
        }

        [Fact]
        public void CoolShadowTintPushesBlueAboveRed()
        {
            var cool = ToneCurve.Build(ToneSettings.Neutral with { ShadowTint = -80 });
            int i = 24;

            Assert.True(B(cool, i) > R(cool, i), "a cool shadow tint must leave blue above red");
        }

        /// <summary>Split toning is the point: warm highlights with cool shadows in one
        /// curve, each acting only on its own end.</summary>
        [Fact]
        public void SplitToningActsInOppositeDirectionsAtOppositeEnds()
        {
            var split = ToneCurve.Build(
                ToneSettings.Neutral with { ShadowTint = -80, HighlightTint = 80 });

            Assert.True(B(split, 24) > R(split, 24), "shadows should have gone cool");
            Assert.True(R(split, N - 16) > B(split, N - 16), "highlights should have gone warm");
        }

        [Fact]
        public void GreenIsNeverTintedOnlyRedAndBlue()
        {
            var tinted = ToneCurve.Build(ToneSettings.Neutral with { MidtoneTint = 100 });
            var flat = ToneCurve.Build(ToneSettings.Neutral);

            for (int i = 0; i < N; i++)
                Assert.True(Math.Abs(G(tinted, i) - G(flat, i)) <= 1,
                    $"green moved at {i}; tint must ride on red and blue only");
        }

        [Theory]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(150)]
        public void CurveNeverGoesBackwards(int gamma)
        {
            var graded = ToneCurve.Build(new ToneSettings(
                Gamma: gamma, Highlights: 40, Shadows: -30, Whites: 20, Blacks: -20,
                Fade: 15, ShadowTint: -60, MidtoneTint: 30, HighlightTint: 60));

            for (int c = 0; c < 3; c++)
                for (int i = 1; i < N; i++)
                {
                    int o = c * N;
                    Assert.True(graded[o + i] >= graded[o + i - 1],
                        $"channel {c} dipped at {i} - a tone curve must be monotonic");
                }
        }

        [Fact]
        public void NeutralIsRecognisedWithoutBuildingARamp()
        {
            Assert.True(ToneSettings.Neutral.IsNeutral);
            Assert.False((ToneSettings.Neutral with { Fade = 1 }).IsNeutral);
            Assert.False((ToneSettings.Neutral with { Gamma = 101 }).IsNeutral);
            Assert.False((ToneSettings.Neutral with { HighlightTint = -1 }).IsNeutral);
        }

        /// <summary>
        /// A record struct's parameterless constructor zeroes every field and skips the
        /// defaults on the primary constructor, so `default`, `new()` and JSON with no Tone
        /// field all arrive with Gamma = 0. Untreated that clamps to 50 and darkens
        /// everybody's screen on upgrade - so zero has to mean untouched.
        /// </summary>
        [Fact]
        public void ADefaultInitialisedToneSettingsIsNeutral()
        {
            ToneSettings zeroed = default;

            Assert.Equal(100, zeroed.ResolvedGamma);
            Assert.True(zeroed.IsNeutral, "a zero-initialised grade must mean 'do nothing'");

            var ramp = ToneCurve.Build(zeroed);
            var identity = GammaCurve.Identity();
            for (int i = 0; i < ramp.Length; i++)
                Assert.True(Math.Abs(ramp[i] - identity[i]) <= 1,
                    $"entry {i}: zero-initialised grade altered the ramp");
        }

        [Fact]
        public void NormalizedNeverCarriesTheAmbiguousZeroGamma()
        {
            ToneSettings zeroed = default;
            Assert.Equal(100, zeroed.Normalized.Gamma);
            Assert.Equal(130, (ToneSettings.Neutral with { Gamma = 130 }).Normalized.Gamma);
        }

        [Fact]
        public void GammaOnlyIsDistinguishedFromARealGrade()
        {
            Assert.True((ToneSettings.Neutral with { Gamma = 130 }).IsGammaOnly);
            Assert.False((ToneSettings.Neutral with { Fade = 5 }).IsGammaOnly);
        }

        /// <summary>
        /// The old single-gamma path has to survive, or everyone's saved gamma silently
        /// changes meaning.
        ///
        /// Above the guarded region it must match exactly. Inside it - the darkest quarter -
        /// the night-vision guard is deliberately allowed to hold gamma down, because
        /// cranking gamma is the oldest way there is to brighten a dark game and exempting it
        /// would leave the guard trivially bypassable. The difference is a fraction of a
        /// percent and invisible on anything but a test.
        /// </summary>
        [Theory]
        [InlineData(70)]
        [InlineData(100)]
        [InlineData(130)]
        [InlineData(150)]
        public void MatchesLegacyGammaCurveOutsideTheGuardedShadows(int gamma)
        {
            var mine = ToneCurve.Build(ToneSettings.Neutral with { Gamma = gamma });
            var legacy = GammaCurve.Build(gamma / 100f);

            // The guard has fully released by 25% of the range.
            int guarded = (int)(N * 0.25);

            for (int c = 0; c < 3; c++)
                for (int i = guarded; i < N; i++)
                {
                    int idx = c * N + i;
                    Assert.True(Math.Abs(mine[idx] - legacy[idx]) <= 2,
                        $"entry {i} drifted from the legacy gamma curve: {mine[idx]} vs {legacy[idx]}");
                }
        }

        /// <summary>Inside the guarded region a raised gamma may be held down, but never
        /// pushed up - the guard only ever darkens.</summary>
        [Theory]
        [InlineData(130)]
        [InlineData(150)]
        public void TheGuardOnlyEverHoldsGammaDownNeverUp(int gamma)
        {
            var mine = ToneCurve.Build(ToneSettings.Neutral with { Gamma = gamma });
            var legacy = GammaCurve.Build(gamma / 100f);

            for (int i = 0; i < N; i++)
                Assert.True(mine[N + i] <= legacy[N + i] + 2,
                    $"entry {i}: the guard brightened rather than darkened");
        }

        [Fact]
        public void BuildIsDeterministic()
        {
            var settings = new ToneSettings(120, 40, -30, 20, -20, 15, -40, 10, 60);
            Assert.Equal(ToneCurve.Build(settings), ToneCurve.Build(settings));
        }
    }
}
