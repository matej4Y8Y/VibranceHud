using System;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The advanced colour controls must not add up to night vision.
    ///
    /// Shadows, blacks, fade and gamma all lift dark areas. Pushed to their limits together
    /// they stop being a look and become an unfair advantage: a player crouched in an unlit
    /// corner, rendered by the game at near-black, pulled up to plainly visible grey.
    ///
    /// That is not what this app sells, and it is the kind of thing that gets accounts
    /// banned. The roadmap's line - nothing that risks anti-cheat - covers this as much as it
    /// covers injection, so these tests are the enforcement of a product promise rather than
    /// a check on some maths.
    ///
    /// Everything here works in 8-bit terms because that is how people think about "is this
    /// visible": 0 is black, 255 is white, and anything under about 25 is indistinguishable
    /// from black on a normal screen in a lit room.
    /// </summary>
    public sealed class NightVisionGuardTests
    {
        private const int N = GammaCurve.Entries;

        /// <summary>Green channel, as 0-255. Green carries the luminance; the tint only
        /// moves red and blue around it.</summary>
        private static int Out8(ushort[] ramp, int input8) => ramp[N + input8] >> 8;

        /// <summary>Every dark-lifting control at maximum, at once. Nobody would choose this
        /// as a look - it is what somebody reaches for when they want to see in the dark.</summary>
        private static ToneSettings WorstCase => new(
            Gamma: 150, Highlights: 100, Shadows: 100, Whites: 100, Blacks: 100,
            Fade: 100, ShadowTint: 100, MidtoneTint: 100, HighlightTint: 100);

        [Fact]
        public void PureBlackStaysBlackEnoughToHideIn()
        {
            var ramp = ToneCurve.Build(WorstCase);

            // 0.08 of full scale, ~20/255. Visibly not pure black, nowhere near enough to
            // pick a body out of an unlit corner.
            Assert.True(Out8(ramp, 0) <= 25,
                $"pure black was lifted to {Out8(ramp, 0)}/255 - that is night vision");
        }

        /// <summary>
        /// The value that actually matters. Games render a player in shadow somewhere around
        /// 8-16, not at absolute zero, so guarding only input 0 would guard nothing.
        /// </summary>
        [Theory]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(12)]
        [InlineData(16)]
        public void SomebodyHidingInShadowStaysHidden(int input8)
        {
            var ramp = ToneCurve.Build(WorstCase);
            int result = Out8(ramp, input8);

            Assert.True(result <= 60,
                $"input {input8}/255 came out at {result}/255 - clearly visible against a dark background");
        }

        [Fact]
        public void TheGuardBindsHardestExactlyWhereItMatters()
        {
            var guarded = ToneCurve.Build(WorstCase);
            var neutral = ToneCurve.Build(ToneSettings.Neutral);

            // At the very bottom the guard should be doing real work.
            int liftAtBlack = Out8(guarded, 0) - Out8(neutral, 0);
            Assert.True(liftAtBlack < 30, $"black lifted by {liftAtBlack}/255 even with the guard");
        }

        // ---- and it must not ruin the legitimate uses ------------------------------------

        /// <summary>
        /// The guard has to be invisible in the midtones and highlights, or it would be
        /// quietly deleting the grading controls people actually bought this for.
        /// </summary>
        [Theory]
        [InlineData(96)]
        [InlineData(128)]
        [InlineData(200)]
        [InlineData(255)]
        public void MidtonesAndHighlightsAreUntouchedByTheGuard(int input8)
        {
            var graded = ToneCurve.Build(new ToneSettings(Highlights: 80, Shadows: 40, Fade: 30));
            var ungraded = ToneCurve.Build(ToneSettings.Neutral);

            // The guard has fully released well below these inputs, so grading here must
            // still be free to do whatever it was asked to. If this fails, the guard has
            // crept up into the picture and is quietly deleting what people paid for.
            Assert.True(Out8(graded, input8) >= Out8(ungraded, input8),
                $"at {input8}/255 the guard is clipping legitimate grading");
        }

        [Fact]
        public void ANeutralGradeIsStillExactlyNeutral()
        {
            // The guard must not bend the identity ramp - upgrading would otherwise darken
            // everybody's shadows for no reason.
            var ramp = ToneCurve.Build(ToneSettings.Neutral);
            var identity = GammaCurve.Identity();

            for (int i = 0; i < ramp.Length; i++)
                Assert.True(Math.Abs(ramp[i] - identity[i]) <= 1,
                    $"entry {i}: guard altered a neutral grade");
        }

        [Fact]
        public void AModerateFilmicLiftStillWorks()
        {
            // Fade is a legitimate look. It should still visibly lift blacks, just not to the
            // point of revealing what the game meant to hide.
            var faded = ToneCurve.Build(ToneSettings.Neutral with { Fade = 60 });
            var flat = ToneCurve.Build(ToneSettings.Neutral);

            Assert.True(Out8(faded, 0) > Out8(flat, 0), "fade should still lift the black point");
            Assert.True(Out8(faded, 0) <= 25, "but not past the guard");
        }

        [Fact]
        public void TheCurveIsStillMonotonicWithTheGuardApplied()
        {
            // Capping the bottom could otherwise introduce a flat spot or a dip, which shows
            // up as banding across a dark sky.
            var ramp = ToneCurve.Build(WorstCase);

            for (int c = 0; c < 3; c++)
                for (int i = 1; i < N; i++)
                {
                    int o = c * N;
                    Assert.True(ramp[o + i] >= ramp[o + i - 1],
                        $"channel {c} dipped at {i} after guarding");
                }
        }

        /// <summary>
        /// No combination of the sliders may get past it. Exhaustive over the extremes rather
        /// than the one worst case, because the guard is a promise about the whole control
        /// surface, not about one preset.
        /// </summary>
        [Fact]
        public void NoCombinationOfExtremesDefeatsTheGuard()
        {
            int[] extremes = { -100, 0, 100 };

            foreach (int shadows in extremes)
            foreach (int blacks in extremes)
            foreach (int gamma in new[] { 50, 100, 150 })
            foreach (int fade in new[] { 0, 50, 100 })
            {
                var ramp = ToneCurve.Build(new ToneSettings(
                    Gamma: gamma, Shadows: shadows, Blacks: blacks, Fade: fade));

                int atBlack = Out8(ramp, 0);
                Assert.True(atBlack <= 25,
                    $"shadows {shadows}, blacks {blacks}, gamma {gamma}, fade {fade} " +
                    $"lifted black to {atBlack}/255");
            }
        }
    }
}
