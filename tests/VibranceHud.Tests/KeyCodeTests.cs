using System.Collections.Generic;
using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class KeyCodeTests
    {
        [Fact]
        public void GeneratedKeyIsFourGroupsOfFour()
        {
            var parts = KeyCode.Generate().Split('-');
            Assert.Equal(4, parts.Length);
            foreach (var p in parts) Assert.Equal(4, p.Length);
        }

        [Fact]
        public void GeneratedKeysAreWellFormed()
        {
            for (int i = 0; i < 200; i++)
                Assert.True(KeyCode.IsWellFormed(KeyCode.Generate()));
        }

        [Fact]
        public void GeneratedKeysAreUnique()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < 500; i++) Assert.True(seen.Add(KeyCode.Generate()));
        }

        /// <summary>Nothing ambiguous when read off a screen or spoken aloud.</summary>
        [Fact]
        public void GeneratedKeysAvoidAmbiguousCharacters()
        {
            for (int i = 0; i < 200; i++)
                foreach (var c in KeyCode.Generate().Replace("-", ""))
                    Assert.DoesNotContain(c, "OIL01");
        }

        [Fact]
        public void NormaliseAcceptsLowercase()
        {
            var key = KeyCode.Generate();
            Assert.Equal(key, KeyCode.Normalise(key.ToLowerInvariant()));
        }

        [Fact]
        public void NormaliseAcceptsSurroundingWhitespace()
        {
            var key = KeyCode.Generate();
            Assert.Equal(key, KeyCode.Normalise("   " + key + "  "));
        }

        [Fact]
        public void NormaliseAcceptsMissingDashes()
        {
            var key = KeyCode.Generate();
            Assert.Equal(key, KeyCode.Normalise(key.Replace("-", "")));
        }

        [Fact]
        public void NormaliseIsIdempotent()
        {
            var key = KeyCode.Generate();
            Assert.Equal(key, KeyCode.Normalise(KeyCode.Normalise(key)));
        }

        /// <summary>A single mistyped character is caught locally rather than becoming a
        /// failed redemption the user can't explain.</summary>
        [Fact]
        public void SingleCharacterTypoIsRejected()
        {
            var key = KeyCode.Generate();
            var chars = key.ToCharArray();
            chars[0] = chars[0] == 'A' ? 'B' : 'A';
            Assert.False(KeyCode.IsWellFormed(new string(chars)));
        }

        /// <summary>Transposing two characters is what people do when copying by hand, so the
        /// check digit is position-weighted to catch it.</summary>
        [Fact]
        public void TransposedCharactersAreRejected()
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var raw = KeyCode.Generate().Replace("-", "").ToCharArray();
                if (raw[0] == raw[1]) continue; // swapping identical chars changes nothing
                (raw[0], raw[1]) = (raw[1], raw[0]);
                Assert.False(KeyCode.IsWellFormed(new string(raw)));
                return;
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("2K7M-Q8XR-T9WD")]
        [InlineData("2K7M-Q8XR-T9WD-N3FG-EXTRA")]
        [InlineData("2K7M-Q8XR-T9WD-N3F!")]
        [InlineData("2K7M-Q8XR-T9WD-N3FO")]
        public void MalformedInputIsRejected(string? input) =>
            Assert.False(KeyCode.IsWellFormed(input));

        [Fact]
        public void NormaliseOfGarbageReturnsEmpty()
        {
            Assert.Equal("", KeyCode.Normalise(null));
            Assert.Equal("", KeyCode.Normalise("   "));
            Assert.Equal("", KeyCode.Normalise("not a key at all"));
        }
    }
}
