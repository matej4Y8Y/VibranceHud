// End-to-end proof that revocation actually cuts off access, through the real
// LicenseService and the real on-disk cache - not just the pure set logic.
//
// The bug these guard against: revocation that only checks on Load() lets a revoked
// key activate successfully once on a fresh machine (writing a valid license file),
// and only fails on the *next* launch. Someone handed a revoked key would still get
// a working session out of it.

using System;
using System.IO;
using VibranceHud.License;
using Xunit;

namespace VibranceHud.Tests
{
    [Collection(LicenseTestCollection.Name)]
    public sealed class LicenseRevocationTests : IDisposable
    {
        private readonly TempLicenseDir _dir = new();

        private static readonly string CachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlexusX", "revocations-cache.json");

        private readonly string? _savedCache;

        public LicenseRevocationTests()
        {
            // These tests write the real cache file (RevocationService hardcodes the
            // path, same as LicenseService does for license.json). Save and restore it
            // so a developer's own revocation cache survives a test run.
            _savedCache = File.Exists(CachePath) ? File.ReadAllText(CachePath) : null;
        }

        public void Dispose()
        {
            try
            {
                if (_savedCache != null) File.WriteAllText(CachePath, _savedCache);
                else if (File.Exists(CachePath)) File.Delete(CachePath);
            }
            catch { /* best-effort restore */ }
            new LicenseService(_dir.Path).Deactivate();
            _dir.Dispose();
        }

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

        private static void WriteCache(params string[] revokedSerials)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            var hashes = new string[revokedSerials.Length];
            for (int i = 0; i < revokedSerials.Length; i++)
                hashes[i] = RevocationList.HashSerial(revokedSerials[i]);
            File.WriteAllText(CachePath, RevocationList.Serialize(hashes));
        }

        private static void ClearCache()
        {
            if (File.Exists(CachePath)) File.Delete(CachePath);
        }

        /// <summary>The headline requirement: revoke a key, and the machine already
        /// running it loses access on the next launch.</summary>
        [Fact]
        public void RevokedSerial_LosesAccess_OnTheNextLaunch()
        {
            var key = IssueKey("P");
            ClearCache();

            var service = new LicenseService(_dir.Path);
            Assert.Equal(LicenseState.Valid, service.TryActivate(key));

            // Developer publishes a revocation; the app picks it up on next start.
            WriteCache(key);

            var reloaded = new LicenseService(_dir.Path);
            Assert.Equal(LicenseState.Revoked, reloaded.State);
            Assert.False(reloaded.HasValidLicense);
        }

        /// <summary>A revoked key must not buy even one working session on a fresh
        /// machine - activation itself has to refuse it.</summary>
        [Fact]
        public void RevokedSerial_CannotActivateAtAll()
        {
            var key = IssueKey("P");
            WriteCache(key);

            var service = new LicenseService(_dir.Path);
            Assert.Equal(LicenseState.Revoked, service.TryActivate(key));
            Assert.False(service.HasValidLicense);
        }

        /// <summary>Revoking one key must not affect any other key.</summary>
        [Fact]
        public void RevokingOneKey_LeavesOtherKeysWorking()
        {
            var revoked = IssueKey("P");
            var untouched = IssueKey("P");
            WriteCache(revoked);

            var service = new LicenseService(_dir.Path);
            Assert.Equal(LicenseState.Valid, service.TryActivate(untouched));

            Assert.Equal(LicenseState.Valid, new LicenseService(_dir.Path).State);
        }

        /// <summary>No cache on disk (first ever launch, or offline the whole time)
        /// must not lock anyone out.</summary>
        [Fact]
        public void MissingCache_DoesNotRevokeAnyone()
        {
            var key = IssueKey("P");
            ClearCache();

            var service = new LicenseService(_dir.Path);
            Assert.Equal(LicenseState.Valid, service.TryActivate(key));
            Assert.Equal(LicenseState.Valid, new LicenseService(_dir.Path).State);
        }

        /// <summary>A corrupt/truncated cache must fail OPEN. Failing closed here would
        /// lock out every paying user the moment the published file got malformed.</summary>
        [Fact]
        public void CorruptCache_FailsOpen_AndKeepsUsersWorking()
        {
            var key = IssueKey("P");
            ClearCache();
            var service = new LicenseService(_dir.Path);
            Assert.Equal(LicenseState.Valid, service.TryActivate(key));

            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            File.WriteAllText(CachePath, "{ this is not valid json");

            Assert.Equal(LicenseState.Valid, new LicenseService(_dir.Path).State);
        }

        /// <summary>An empty revocation list is the normal steady state - it must not
        /// revoke anything.</summary>
        [Fact]
        public void EmptyCache_DoesNotRevokeAnyone()
        {
            var key = IssueKey("P");
            ClearCache();
            var service = new LicenseService(_dir.Path);
            Assert.Equal(LicenseState.Valid, service.TryActivate(key));

            WriteCache(); // empty list

            Assert.Equal(LicenseState.Valid, new LicenseService(_dir.Path).State);
        }

        /// <summary>Un-revoking (removing the hash again) restores access, so a mistaken
        /// revocation is recoverable without issuing a new key.</summary>
        [Fact]
        public void RemovingARevocation_RestoresAccess()
        {
            var key = IssueKey("P");
            ClearCache();
            Assert.Equal(LicenseState.Valid, new LicenseService(_dir.Path).TryActivate(key));

            WriteCache(key);
            Assert.Equal(LicenseState.Revoked, new LicenseService(_dir.Path).State);

            WriteCache(); // developer removes it again
            Assert.Equal(LicenseState.Valid, new LicenseService(_dir.Path).State);
        }
    }
}
