using System;
using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class LicenceDocumentTests
    {
        private static LicenceDocument Sample() => new(
            "2K7M-Q8XR-T9WD-N3FG", PlanCatalog.Monthly,
            new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc),
            "MXXBGGXAOCQP36SC");

        [Fact]
        public void RoundTripsThroughJson()
        {
            Assert.True(LicenceDocument.TryFromJson(Sample().ToCanonicalJson(), out var back));
            Assert.Equal(Sample(), back);
        }

        /// <summary>The signature covers these exact bytes, so serialising twice must produce
        /// byte-identical output or valid licences stop verifying.</summary>
        [Fact]
        public void CanonicalJsonIsStable() =>
            Assert.Equal(Sample().ToCanonicalJson(), Sample().ToCanonicalJson());

        [Fact]
        public void ExpiryIsComparedAgainstTheSuppliedClock()
        {
            Assert.False(Sample().IsExpiredAt(new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc)));
            Assert.True(Sample().IsExpiredAt(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc)));
        }

        [Fact]
        public void ExpiresExactlyAtTheStatedInstant() =>
            Assert.True(Sample().IsExpiredAt(Sample().ExpiresUtc));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{}")]
        [InlineData("""{"serial":"2K7M-Q8XR-T9WD-N3FG"}""")]
        [InlineData("""{"serial":"X","plan":"monthly","issued":"nonsense","expires":"nonsense","hardware":"Y"}""")]
        public void MalformedJsonIsRejected(string? json)
        {
            Assert.False(LicenceDocument.TryFromJson(json, out var doc));
            Assert.Null(doc);
        }

        /// <summary>A licence read back as local time would expire at the wrong moment for
        /// anyone outside UTC.</summary>
        [Fact]
        public void TimesStayUtcAcrossTheRoundTrip()
        {
            LicenceDocument.TryFromJson(Sample().ToCanonicalJson(), out var back);
            Assert.Equal(DateTimeKind.Utc, back!.IssuedUtc.Kind);
            Assert.Equal(DateTimeKind.Utc, back.ExpiresUtc.Kind);
        }
    }
}
