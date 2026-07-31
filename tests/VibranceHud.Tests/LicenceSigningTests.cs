// The tests that decide whether anyone can forge a licence.
//
// The beta failed exactly here: one symmetric secret both created and checked keys, so it had
// to ship inside the app, and extracting it was enough to mint working paid keys. These pin
// down the property that replaces it - the shipped half can check, and cannot create.

using System;
using System.Text;
using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class LicenceSigningTests
    {
        private static LicenceDocument Sample() => new(
            "2K7M-Q8XR-T9WD-N3FG", PlanCatalog.Monthly,
            new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc),
            "MXXBGGXAOCQP36SC");

        [Fact]
        public void GenuineLicenceVerifies()
        {
            LicenceSigner.CreateKeyPair(out var priv, out var pub);
            var envelope = LicenceSigner.Sign(Sample(), priv);

            Assert.True(LicenceVerifier.TryVerify(envelope, pub, out var doc));
            Assert.Equal(Sample(), doc);
        }

        /// <summary>THE test. Someone with the app - and therefore the public key - still
        /// cannot produce a licence it accepts, because they don't hold the private half.</summary>
        [Fact]
        public void LicenceSignedByAnotherKeyIsRejected()
        {
            LicenceSigner.CreateKeyPair(out var attackerPriv, out _);
            LicenceSigner.CreateKeyPair(out _, out var ourPub);

            var forged = LicenceSigner.Sign(Sample(), attackerPriv);

            Assert.False(LicenceVerifier.TryVerify(forged, ourPub, out var doc));
            Assert.Null(doc);
        }

        /// <summary>Hand-editing the licence file to extend it must break the signature.</summary>
        [Fact]
        public void ExtendingTheExpiryByHandIsRejected()
        {
            LicenceSigner.CreateKeyPair(out var priv, out var pub);
            var envelope = LicenceSigner.Sign(Sample(), priv);

            var extended = Sample() with { ExpiresUtc = Sample().ExpiresUtc.AddYears(10) };
            var tampered = envelope.Replace(
                Convert.ToBase64String(Encoding.UTF8.GetBytes(Sample().ToCanonicalJson())),
                Convert.ToBase64String(Encoding.UTF8.GetBytes(extended.ToCanonicalJson())));

            Assert.NotEqual(envelope, tampered); // the swap really happened
            Assert.False(LicenceVerifier.TryVerify(tampered, pub, out _));
        }

        /// <summary>Copying someone else's licence onto another PC must not work - the
        /// hardware id is inside the signed bytes.</summary>
        [Fact]
        public void RepointingTheLicenceAtAnotherPcIsRejected()
        {
            LicenceSigner.CreateKeyPair(out var priv, out var pub);
            var envelope = LicenceSigner.Sign(Sample(), priv);

            var moved = Sample() with { HardwareId = "SOMEONEELSESPC00" };
            var tampered = envelope.Replace(
                Convert.ToBase64String(Encoding.UTF8.GetBytes(Sample().ToCanonicalJson())),
                Convert.ToBase64String(Encoding.UTF8.GetBytes(moved.ToCanonicalJson())));

            Assert.False(LicenceVerifier.TryVerify(tampered, pub, out _));
        }

        [Fact]
        public void EveryKeyPairIsDifferent()
        {
            LicenceSigner.CreateKeyPair(out var priv1, out _);
            LicenceSigner.CreateKeyPair(out var priv2, out _);
            Assert.NotEqual(priv1, priv2);
        }

        [Fact]
        public void SigningDoesNotAlterTheDocument()
        {
            LicenceSigner.CreateKeyPair(out var priv, out _);
            var doc = Sample();
            LicenceSigner.Sign(doc, priv);
            Assert.Equal(Sample(), doc);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{}")]
        [InlineData("""{"doc":"","sig":""}""")]
        [InlineData("""{"doc":"!!!notbase64!!!","sig":"!!!"}""")]
        public void MalformedEnvelopeIsRejected(string? envelope)
        {
            LicenceSigner.CreateKeyPair(out _, out var pub);
            Assert.False(LicenceVerifier.TryVerify(envelope, pub, out var doc));
            Assert.Null(doc);
        }

        [Fact]
        public void MissingPublicKeyIsRejected()
        {
            LicenceSigner.CreateKeyPair(out var priv, out _);
            Assert.False(LicenceVerifier.TryVerify(LicenceSigner.Sign(Sample(), priv),
                Array.Empty<byte>(), out _));
        }

        /// <summary>A verified licence still has to be checked for expiry - a signature only
        /// proves it is genuine, not that it is current.</summary>
        [Fact]
        public void AGenuineButExpiredLicenceIsStillExpired()
        {
            LicenceSigner.CreateKeyPair(out var priv, out var pub);
            var envelope = LicenceSigner.Sign(Sample(), priv);

            Assert.True(LicenceVerifier.TryVerify(envelope, pub, out var doc));
            Assert.True(doc!.IsExpiredAt(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        }
    }
}
