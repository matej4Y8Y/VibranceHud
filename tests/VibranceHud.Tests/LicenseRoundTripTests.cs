// Round-trip verification of the activation key system.
// Generates a fresh key, then feeds it back through LicenseService.TryActivate
// and asserts the result is Valid. This is the test that was missing before
// the bug shipped: KeyGenerator and LicenseService were signing/verifying
// different payload shapes, so every key got rejected.
//
// The bug was: KeyGenerator signed the 4-group payload (no checksum), but
// LicenseService verified the 5-group form (with checksum). The fix added
// LicenseKey.SignedPayload so both sides agree on the bytes.

using System;
using VibranceHud.License;
using Xunit;

namespace VibranceHud.Tests
{
    [Collection(LicenseTestCollection.Name)]
    public sealed class LicenseRoundTripTests
    {
        [Fact]
        public void GeneratedKey_ValidatesThroughLicenseService()
        {
            var masterKey = LicenseKeyDerivation.DeriveMasterKey();
            var yearMonth = LicenseKeyDerivation.EncodeYearMonth(2026, 7);
            var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var body = RandomBase32(rng, 8);
            var payload = $"{yearMonth}-R-F-{body}";
            var checksum = LicenseKeyDerivation.SignPayload(payload, masterKey);
            var key = $"{payload}-{checksum}";

            var parsed = LicenseKey.Parse(key);
            Assert.NotNull(parsed);
            Assert.Equal(payload, parsed!.SignedPayload);
            Assert.Equal(checksum, parsed.Checksum);

            // Now feed it through the real service and verify it accepts.
            var service = new LicenseService();
            var state = service.TryActivate(key);
            Assert.Equal(LicenseState.Valid, state);

            service.Deactivate();
        }

        /// <summary>
        /// Regression guard for the bug that made every activation fail: an earlier
        /// version of DeriveMasterKey() seeded itself with RandomNumberGenerator,
        /// so it returned a DIFFERENT key on every call. KeyGenerator.exe signs a
        /// key with one call to this method in one process; the shipped app
        /// verifies it with a completely separate call, often in a separate
        /// process (or the same process on a later launch). If two calls to
        /// DeriveMasterKey() don't return byte-identical results, no key issued
        /// by KeyGenerator can ever validate - the previous round-trip test above
        /// did not catch this, because it only ever called DeriveMasterKey() once
        /// and reused the local variable for both signing and verifying.
        /// </summary>
        [Fact]
        public void DeriveMasterKey_IsDeterministic_AcrossIndependentCalls()
        {
            var first = LicenseKeyDerivation.DeriveMasterKey();
            var second = LicenseKeyDerivation.DeriveMasterKey();
            Assert.Equal(first, second);
        }

        /// <summary>
        /// The same scenario as <see cref="GeneratedKey_ValidatesThroughLicenseService"/>,
        /// but with the master key derived TWICE independently - once to sign (as
        /// KeyGenerator.exe would, in its own process) and once to verify (as the
        /// app does on every launch, in its own process). This is the shape of bug
        /// that actually shipped: each side computed its own "master key" and only
        /// happened to match if the derivation was deterministic.
        /// </summary>
        [Fact]
        public void KeyGenerator_And_App_IndependentlyDerivedMasterKeys_StillAgree()
        {
            var signingKey = LicenseKeyDerivation.DeriveMasterKey();   // KeyGenerator's call
            var yearMonth = LicenseKeyDerivation.EncodeYearMonth(2026, 7);
            var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var body = RandomBase32(rng, 8);
            var payload = $"{yearMonth}-R-F-{body}";
            var checksum = LicenseKeyDerivation.SignPayload(payload, signingKey);
            var key = $"{payload}-{checksum}";

            var verifyingKey = LicenseKeyDerivation.DeriveMasterKey();  // the app's own call
            var parsed = LicenseKey.Parse(key);
            Assert.NotNull(parsed);
            Assert.True(LicenseKeyDerivation.VerifySignature(parsed!.SignedPayload, parsed.Checksum, verifyingKey));
        }

        private static string RandomBase32(System.Security.Cryptography.RandomNumberGenerator rng, int length)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var bytes = new byte[length];
            rng.GetBytes(bytes);
            var sb = new System.Text.StringBuilder(length);
            foreach (var b in bytes)
            {
                sb.Append(alphabet[b % alphabet.Length]);
            }
            return sb.ToString();
        }
    }
}
