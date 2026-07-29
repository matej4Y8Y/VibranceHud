// Covers the pure revocation logic: hashing, parsing, and membership.
//
// The behaviour that actually matters to a paying user is "a malformed or empty list
// must never lock anyone out" - a typo in the published JSON, or a truncated download,
// must degrade to "nothing is revoked" rather than "everything is revoked". Several of
// these tests exist specifically to pin that down.

using System.Collections.Generic;
using VibranceHud.License;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class RevocationListTests
    {
        private const string SampleSerial = "AACO-R-P-IYNIVVT6-DMRFQIQU";

        [Fact]
        public void HashSerial_IsStable_ForTheSameInput()
        {
            Assert.Equal(RevocationList.HashSerial(SampleSerial), RevocationList.HashSerial(SampleSerial));
        }

        [Fact]
        public void HashSerial_IgnoresCaseAndSurroundingWhitespace()
        {
            var canonical = RevocationList.HashSerial(SampleSerial);
            Assert.Equal(canonical, RevocationList.HashSerial("  " + SampleSerial.ToLowerInvariant() + "  "));
        }

        [Fact]
        public void HashSerial_DiffersBetweenDifferentSerials()
        {
            Assert.NotEqual(
                RevocationList.HashSerial(SampleSerial),
                RevocationList.HashSerial("AACO-R-P-RAKJ3KMM-PWEAXBMW"));
        }

        [Fact]
        public void HashSerial_DoesNotLeakThePlaintextSerial()
        {
            // The whole point of publishing hashes rather than serials is that the
            // published file must not be a directory of usable keys.
            var hash = RevocationList.HashSerial(SampleSerial);
            Assert.DoesNotContain("IYNIVVT6", hash);
            Assert.DoesNotContain("AACO", hash);
        }

        [Fact]
        public void IsRevoked_FindsAListedSerial()
        {
            var set = RevocationList.Parse(
                RevocationList.Serialize(new[] { RevocationList.HashSerial(SampleSerial) }));
            Assert.True(RevocationList.IsRevoked(SampleSerial, set));
        }

        [Fact]
        public void IsRevoked_IsFalse_ForAnUnlistedSerial()
        {
            var set = RevocationList.Parse(
                RevocationList.Serialize(new[] { RevocationList.HashSerial("AACO-R-P-RAKJ3KMM-PWEAXBMW") }));
            Assert.False(RevocationList.IsRevoked(SampleSerial, set));
        }

        [Fact]
        public void RoundTrip_PreservesEveryHash()
        {
            var hashes = new[]
            {
                RevocationList.HashSerial(SampleSerial),
                RevocationList.HashSerial("AACO-R-T-GQTG3CVK-ST2KJXC6"),
            };
            var set = RevocationList.Parse(RevocationList.Serialize(hashes));
            Assert.Equal(2, set.Count);
            foreach (var h in hashes) Assert.Contains(h, set);
        }

        // --- fail-open behaviour: bad input must never revoke anyone ---

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not json at all")]
        [InlineData("{")]
        [InlineData("[]")]
        [InlineData("{\"revokedSerialHashes\":null}")]
        [InlineData("{\"someOtherKey\":[\"abc\"]}")]
        public void Parse_ReturnsEmptySet_ForUnusableInput(string json)
        {
            var set = RevocationList.Parse(json);
            Assert.Empty(set);
            Assert.False(RevocationList.IsRevoked(SampleSerial, set));
        }

        [Fact]
        public void Parse_SkipsBlankEntries()
        {
            var set = RevocationList.Parse("{\"revokedSerialHashes\":[\"\",\"  \",\"abc123\"]}");
            Assert.Single(set);
            Assert.Contains("abc123", set);
        }

        [Fact]
        public void Parse_IsCaseInsensitive_SoAHandEditedListStillMatches()
        {
            // The developer edits this file by hand; an uppercased hash must still match.
            var hash = RevocationList.HashSerial(SampleSerial);
            var set = RevocationList.Parse(
                "{\"revokedSerialHashes\":[\"" + hash.ToUpperInvariant() + "\"]}");
            Assert.True(RevocationList.IsRevoked(SampleSerial, set));
        }

        [Fact]
        public void EmptyList_RevokesNobody()
        {
            var set = RevocationList.Parse(RevocationList.Serialize(new string[0]));
            Assert.False(RevocationList.IsRevoked(SampleSerial, set));
        }
    }
}
