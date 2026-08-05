using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Profile codes gained contrast and temperature after they shipped. Two things have to
    /// hold: the new fields survive a round trip, and codes people already pasted into Discord
    /// keep working.
    /// </summary>
    public sealed class ProfileCodeContrastTests
    {
        [Fact]
        public void Contrast_and_temperature_survive_a_round_trip()
        {
            var original = new ProfileCode(140, 180, 95, 110, Contrast: 128, Temperature: -44);

            Assert.True(ProfileCode.TryDecode(ProfileCode.Encode(original), out var back));

            Assert.Equal(128, back.Contrast);
            Assert.Equal(-44, back.Temperature);
        }

        [Fact]
        public void The_original_four_still_survive_alongside_them()
        {
            var original = new ProfileCode(140, 180, 95, 110, Contrast: 128, Temperature: -44);

            Assert.True(ProfileCode.TryDecode(ProfileCode.Encode(original), out var back));

            Assert.Equal(original, back);
        }

        [Theory]
        [InlineData(-100)]
        [InlineData(0)]
        [InlineData(100)]
        public void Temperature_survives_at_both_ends_and_neutral(int temperature)
        {
            // The only signed field, so it is offset before encoding - the ends are where an
            // off-by-one in that offset would show up.
            var original = new ProfileCode(100, 100, 100, 100, Temperature: temperature);

            Assert.True(ProfileCode.TryDecode(ProfileCode.Encode(original), out var back));

            Assert.Equal(temperature, back.Temperature);
        }

        [Theory]
        [InlineData(VibranceEngine.MinContrast)]
        [InlineData(VibranceEngine.MaxContrast)]
        public void Contrast_survives_at_both_ends(int contrast)
        {
            var original = new ProfileCode(100, 100, 100, 100, Contrast: contrast);

            Assert.True(ProfileCode.TryDecode(ProfileCode.Encode(original), out var back));

            Assert.Equal(contrast, back.Contrast);
        }

        [Fact]
        public void A_code_shared_before_these_controls_existed_still_works()
        {
            // Built the way the old encoder built them: four values plus a check character.
            // These are still in Discord history and read off streams; rejecting them would
            // break every look anyone has ever shared.
            var legacy = LegacyEncode(140, 180, 95, 110);

            Assert.True(ProfileCode.TryDecode(legacy, out var back));

            Assert.Equal(140, back.Vibrance);
            Assert.Equal(180, back.Saturation);
            Assert.Equal(95, back.Brightness);
            Assert.Equal(110, back.Gamma);
            // The two it never knew about come back neutral rather than as junk.
            Assert.Equal(100, back.Contrast);
            Assert.Equal(0, back.Temperature);
        }

        [Fact]
        public void A_mistyped_new_code_is_still_refused()
        {
            var code = ProfileCode.Encode(new ProfileCode(140, 180, 95, 110, 128, -44));
            // Change one character in the payload; the check character should catch it.
            var broken = code.Substring(0, 4) + NextChar(code[4]) + code.Substring(5);

            Assert.False(ProfileCode.TryDecode(broken, out _));
        }

        [Fact]
        public void A_code_of_the_wrong_length_is_refused()
        {
            var code = ProfileCode.Encode(new ProfileCode(140, 180, 95, 110, 128, -44));

            Assert.False(ProfileCode.TryDecode(code + "A", out _));
            Assert.False(ProfileCode.TryDecode(code.Substring(0, code.Length - 1), out _));
        }

        private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ#";

        private static char NextChar(char c) =>
            Alphabet[(Alphabet.IndexOf(c) + 1) % Alphabet.Length];

        /// <summary>Reproduces the pre-contrast encoder exactly.</summary>
        private static string LegacyEncode(int vibrance, int saturation, int brightness, int gamma)
        {
            int[] payload =
            {
                vibrance,
                saturation,
                brightness - VibranceEngine.MinBrightness,
                gamma - VibranceEngine.MinGamma,
            };

            var body = new System.Text.StringBuilder();
            foreach (int value in payload)
            {
                body.Append(Alphabet[value / 32]);
                body.Append(Alphabet[value % 32]);
            }

            int sum = 0;
            var s = body.ToString();
            for (int i = 0; i < s.Length; i++) sum += Alphabet.IndexOf(s[i]) * (2 * i + 1);
            body.Append(Alphabet[sum % 32]);

            return "PX-" + body;
        }
    }
}
