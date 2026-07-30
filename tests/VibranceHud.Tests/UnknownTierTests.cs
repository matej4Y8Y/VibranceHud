// An unrecognised tier marker must be refused, not quietly treated as a 12-month licence.
//
// GetKind() fell through to Kind.Free for anything it didn't recognise, and free is the
// 365-day tier. So a key carrying a marker this build doesn't know - a newer tier, a typo, a
// hand-crafted character - activated as a full year. That's the opposite of failing safe, and
// it bit for real: the week tier ('W') was added after 0.9.7 shipped, so a week key handed to
// anyone on 0.9.7 would have granted them a year.
//
// The signature check still protects against forged keys; this is about a *validly signed* key
// whose tier this build doesn't understand. Refusing is the only honest answer - the build
// genuinely cannot know how long that licence is meant to last.

using VibranceHud.License;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class UnknownTierTests
    {
        [Theory]
        [InlineData('F', LicenseKey.Kind.Free)]
        [InlineData('T', LicenseKey.Kind.Trial)]
        [InlineData('P', LicenseKey.Kind.Paid)]
        [InlineData('H', LicenseKey.Kind.Temp)]
        [InlineData('W', LicenseKey.Kind.Week)]
        public void KnownMarkers_MapToTheirTier(char marker, LicenseKey.Kind expected)
        {
            var key = new LicenseKey("AACO", 'R', marker, "ABCDEFGH", "IJKLMNOP");
            Assert.True(key.TryGetKind(out var kind));
            Assert.Equal(expected, kind);
        }

        /// <summary>The core fix: unknown means unknown, not "free for a year".</summary>
        [Theory]
        [InlineData('X')]
        [InlineData('Z')]
        [InlineData('Q')]
        [InlineData('A')]
        public void UnknownMarker_IsRejected(char marker)
        {
            var key = new LicenseKey("AACO", 'R', marker, "ABCDEFGH", "IJKLMNOP");
            Assert.False(key.TryGetKind(out _),
                $"marker '{marker}' is unrecognised - it must be refused, not granted a tier");
        }

        /// <summary>The exact scenario that made this matter: an older build meeting a marker
        /// added after it shipped. It must refuse rather than hand out the longest tier.</summary>
        [Fact]
        public void MarkerFromANewerBuild_DoesNotGrantAYear()
        {
            var futureTier = new LicenseKey("AACO", 'R', 'Q', "ABCDEFGH", "IJKLMNOP");

            Assert.False(futureTier.TryGetKind(out var kind),
                "an unknown tier must not resolve at all");
            // Kind.Unknown is the enum's zero value specifically so a failed resolve can't
            // land on a real tier - Free used to sit here, which is the 365-day one.
            Assert.Equal(LicenseKey.Kind.Unknown, kind);
        }

        /// <summary>Activation has to refuse it too, not just the parser - otherwise the tier
        /// still reaches the licence file.</summary>
        [Fact]
        public void ActivatingAnUnknownTier_IsRefused()
        {
            using var dir = new TempLicenseDir();
            var service = new LicenseService(dir.Path);

            // Correctly signed, so this isn't the signature check doing the work - only the
            // tier marker is unrecognised.
            var masterKey = LicenseKeyDerivation.DeriveMasterKey();
            var yearMonth = LicenseKeyDerivation.EncodeYearMonth(
                System.DateTime.UtcNow.Year, System.DateTime.UtcNow.Month);
            var payload = $"{yearMonth}-R-Q-ABCDEFGH";
            var signed = $"{payload}-{LicenseKeyDerivation.SignPayload(payload, masterKey)}";

            Assert.NotEqual(LicenseState.Valid, service.TryActivate(signed));
        }
    }
}
