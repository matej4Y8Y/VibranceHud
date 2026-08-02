using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Share codes: a short string carrying someone's whole look.
    ///
    /// This is how the app's name travels. "What are your settings" gets answered with a code
    /// instead of a screenshot, and the code has PlexusX written on it. It gets typed into
    /// Discord, read off streams, and retyped wrong - so it has to be short, unambiguous, and
    /// able to say no to a typo rather than silently applying someone else's screen.
    /// </summary>
    public sealed class ProfileCodeTests
    {
        private static ProfileCode Sample => new(Vibrance: 145, Saturation: 120,
                                                 Brightness: 95, Gamma: 106);

        [Fact]
        public void A_code_survives_the_round_trip_intact()
        {
            Assert.True(ProfileCode.TryDecode(ProfileCode.Encode(Sample), out var back));
            Assert.Equal(Sample, back);
        }

        [Theory]
        [InlineData(0, 0, 50, 50)]
        [InlineData(200, 200, 150, 150)]
        [InlineData(50, 100, 100, 100)]
        [InlineData(1, 199, 51, 149)]
        public void Every_corner_of_every_slider_survives(int v, int s, int b, int g)
        {
            var profile = new ProfileCode(v, s, b, g);
            Assert.True(ProfileCode.TryDecode(ProfileCode.Encode(profile), out var back));
            Assert.Equal(profile, back);
        }

        [Fact]
        public void The_same_settings_always_produce_the_same_code()
        {
            // Two people on identical settings must be able to compare codes and see they
            // match. A code that drifts is a code nobody trusts.
            Assert.Equal(ProfileCode.Encode(Sample), ProfileCode.Encode(Sample));
        }

        [Fact]
        public void A_code_is_short_enough_to_read_out_loud()
        {
            var code = ProfileCode.Encode(Sample);
            Assert.True(code.Length <= 16, $"'{code}' is {code.Length} characters");
        }

        [Fact]
        public void It_says_plexusx_on_it()
        {
            // The reason this feature exists. The code is the advertising.
            Assert.StartsWith("PX-", ProfileCode.Encode(Sample));
        }

        [Fact]
        public void No_characters_anyone_can_mistake_for_each_other()
        {
            // These get read off a stream and retyped. O/0 and I/1/L are where that goes wrong.
            foreach (char c in ProfileCode.Encode(Sample).Replace("PX-", ""))
                Assert.DoesNotContain(c, "OIL01");
        }

        [Fact]
        public void Lower_case_works_because_nobody_types_capitals()
        {
            var code = ProfileCode.Encode(Sample);
            Assert.True(ProfileCode.TryDecode(code.ToLowerInvariant(), out var back));
            Assert.Equal(Sample, back);
        }

        [Fact]
        public void Spaces_around_a_pasted_code_are_forgiven()
        {
            Assert.True(ProfileCode.TryDecode("  " + ProfileCode.Encode(Sample) + "  ", out _));
        }

        // ---- saying no ---------------------------------------------------------------------

        [Fact]
        public void A_single_mistyped_character_is_caught()
        {
            // Without this, one wrong keystroke silently applies a stranger's settings and the
            // user thinks the app is broken.
            var code = ProfileCode.Encode(Sample);
            var body = code.Substring(3);
            var broken = "PX-" + (body[0] == '2' ? '3' : '2') + body.Substring(1);

            Assert.False(ProfileCode.TryDecode(broken, out _));
        }

        [Fact]
        public void Two_swapped_characters_are_caught()
        {
            // The most common retyping mistake after a plain typo.
            var body = ProfileCode.Encode(Sample).Substring(3);
            if (body[0] == body[1]) return;

            var swapped = "PX-" + body[1] + body[0] + body.Substring(2);
            Assert.False(ProfileCode.TryDecode(swapped, out _));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("hello")]
        [InlineData("PX-")]
        [InlineData("PX-!!!!!!")]
        [InlineData("2K7M-Q8XR-T9WD-N3FG")]   // a licence key, not a profile
        public void Anything_that_isnt_a_code_is_refused_without_throwing(string input)
        {
            Assert.False(ProfileCode.TryDecode(input, out _));
        }

        [Fact]
        public void Values_outside_the_sliders_never_come_back_out
            ()
        {
            // A hand-made code must not be able to push the engine somewhere its own UI can't.
            var silly = new ProfileCode(Vibrance: 9999, Saturation: -5, Brightness: 999, Gamma: 0);
            Assert.True(ProfileCode.TryDecode(ProfileCode.Encode(silly), out var back));

            Assert.InRange(back.Vibrance, 0, VibranceEngine.MaxVibrance);
            Assert.InRange(back.Saturation, 0, VibranceEngine.MaxSaturation);
            Assert.InRange(back.Brightness, VibranceEngine.MinBrightness, VibranceEngine.MaxBrightness);
            Assert.InRange(back.Gamma, VibranceEngine.MinGamma, VibranceEngine.MaxGamma);
        }
    }
}
