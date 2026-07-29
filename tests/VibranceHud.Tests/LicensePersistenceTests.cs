// End-to-end proof that a key issued by KeyGenerator survives a restart.
//
// The scenario these cover is the one that actually broke in testing: activation
// appeared to succeed, but the NEXT launch read license.json back and reported
// Tampered, so the user was asked to activate again every single time. Verifying
// only "TryActivate returns Valid" cannot catch that - it never re-reads the file.

using System;
using System.IO;
using VibranceHud.License;
using Xunit;

namespace VibranceHud.Tests
{
    [Collection(LicenseTestCollection.Name)]
    public sealed class LicensePersistenceTests
    {
        private static string IssueKey(string tierMarker)
        {
            var masterKey = LicenseKeyDerivation.DeriveMasterKey();
            var yearMonth = LicenseKeyDerivation.EncodeYearMonth(
                DateTime.UtcNow.Year, DateTime.UtcNow.Month);
            var body = RandomBase32(8);
            var payload = $"{yearMonth}-R-{tierMarker}-{body}";
            var checksum = LicenseKeyDerivation.SignPayload(payload, masterKey);
            return $"{payload}-{checksum}";
        }

        private static string RandomBase32(int length)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var bytes = new byte[length];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            var sb = new System.Text.StringBuilder(length);
            foreach (var b in bytes) sb.Append(alphabet[b % alphabet.Length]);
            return sb.ToString();
        }

        /// <summary>
        /// Activate, then construct a brand-new LicenseService - which re-reads
        /// license.json from disk and re-derives the master key, exactly as the next
        /// launch of the app does. This is the assertion that would have caught the
        /// signature-length and random-master-key bugs.
        /// </summary>
        [Fact]
        public void ActivatedLicense_IsStillValid_OnTheNextLaunch()
        {
            var service = new LicenseService();
            var original = service.State;
            try
            {
                Assert.Equal(LicenseState.Valid, service.TryActivate(IssueKey("P")));

                // Simulates the next process start.
                var reloaded = new LicenseService();
                Assert.Equal(LicenseState.Valid, reloaded.State);
                Assert.True(reloaded.HasValidLicense);
                Assert.NotNull(reloaded.Current);
                Assert.Equal("paid", reloaded.Current!.Tier);
            }
            finally
            {
                new LicenseService().Deactivate();
                _ = original;
            }
        }

        /// <summary>The key the user typed is echoed back so the Account page can show
        /// it. It has to survive the disk round-trip too, or the page renders blank.</summary>
        [Fact]
        public void ActivatedLicense_RemembersTheKeyText_AcrossReload()
        {
            var key = IssueKey("F");
            try
            {
                var service = new LicenseService();
                Assert.Equal(LicenseState.Valid, service.TryActivate(key));

                var reloaded = new LicenseService();
                Assert.Equal(key, reloaded.KeyText);
            }
            finally
            {
                new LicenseService().Deactivate();
            }
        }

        /// <summary>A paid licence must not read as already expired the moment it is
        /// issued - an off-by-one in the tier duration table would lock out every
        /// paying customer on day one.</summary>
        [Fact]
        public void FreshlyIssuedLicense_HasAnExpiryInTheFuture()
        {
            try
            {
                var service = new LicenseService();
                Assert.Equal(LicenseState.Valid, service.TryActivate(IssueKey("P")));

                Assert.NotNull(service.ExpiresAt);
                Assert.True(service.ExpiresAt!.Value > DateTime.UtcNow,
                    $"paid licence expired at issue time ({service.ExpiresAt})");
            }
            finally
            {
                new LicenseService().Deactivate();
            }
        }

        /// <summary>Hand-editing license.json must be caught rather than silently
        /// accepted - that file is the only thing standing between a trial and a
        /// permanent licence.</summary>
        [Fact]
        public void EditedLicenseFile_IsRejectedAsTampered()
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PlexusX", "license.json");
            try
            {
                var service = new LicenseService();
                Assert.Equal(LicenseState.Valid, service.TryActivate(IssueKey("F")));

                // Flip the tier to "paid" without re-signing, the obvious attack.
                var json = File.ReadAllText(path);
                File.WriteAllText(path, json.Replace("free", "paid"));

                Assert.Equal(LicenseState.Tampered, new LicenseService().State);
            }
            finally
            {
                new LicenseService().Deactivate();
            }
        }
    }
}
