using System;
using System.IO;
using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The gate between a customer pasting something and the app unlocking.
    ///
    /// Every test here is a way someone could try to get in for free, or a way a paying
    /// customer could wrongly be locked out. Those are the only two failure modes that matter.
    /// </summary>
    public sealed class LicenceStoreTests : IDisposable
    {
        private readonly string _dir;
        private readonly string _path;
        private readonly byte[] _private;
        private readonly byte[] _public;

        private static readonly DateTime Now = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        private const string ThisPc = "hw-this-pc";

        public LicenceStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "plexusx-licence-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "licence.dat");
            LicenceSigner.CreateKeyPair(out _private, out _public);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        private LicenceStore Store() => new(_path, _public);

        private string SignedFor(string hardwareId, DateTime expires, string plan = PlanCatalog.Monthly)
        {
            var doc = new LicenceDocument("PLX-0001", plan, Now.AddDays(-1), expires, hardwareId);
            return LicenceSigner.Sign(doc, _private);
        }

        // ---- the happy path ---------------------------------------------------------------

        [Fact]
        public void A_licence_signed_for_this_pc_installs_and_reads_back_valid()
        {
            var store = Store();
            Assert.True(store.TryInstall(SignedFor(ThisPc, Now.AddDays(30)), ThisPc, Now, out var error), error);

            var state = store.Read(ThisPc, Now);
            Assert.Equal(LicenceStatus.Valid, state.Status);
            Assert.Equal(PlanCatalog.Monthly, state.Document!.Plan);
        }

        [Fact]
        public void An_installed_licence_survives_a_restart()
        {
            Assert.True(Store().TryInstall(SignedFor(ThisPc, Now.AddDays(30)), ThisPc, Now, out _));

            // A brand new store object, as if the app had been closed and reopened.
            Assert.Equal(LicenceStatus.Valid, Store().Read(ThisPc, Now).Status);
        }

        // ---- ways in that must not work ----------------------------------------------------

        [Fact]
        public void A_licence_signed_by_someone_elses_key_is_rejected()
        {
            LicenceSigner.CreateKeyPair(out var otherPrivate, out _);
            var forged = LicenceSigner.Sign(
                new LicenceDocument("PLX-9999", PlanCatalog.Lifetime600, Now, Now.AddDays(600), ThisPc),
                otherPrivate);

            Assert.False(Store().TryInstall(forged, ThisPc, Now, out _));
        }

        [Fact]
        public void Editing_the_expiry_date_after_signing_breaks_the_signature()
        {
            var envelope = SignedFor(ThisPc, Now.AddDays(1));

            // Swap one character of the signed payload - the sort of thing a text editor
            // makes trivial and the signature makes pointless.
            var tampered = envelope.Replace("\"doc\":\"", "\"doc\":\"A");

            Assert.False(Store().TryInstall(tampered, ThisPc, Now, out _));
        }

        [Fact]
        public void A_licence_for_another_pc_is_refused_at_install()
        {
            Assert.False(Store().TryInstall(SignedFor("hw-someone-else", Now.AddDays(30)), ThisPc, Now, out var error));
            Assert.Contains("another", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Copying_an_installed_licence_to_a_different_pc_does_not_work()
        {
            Assert.True(Store().TryInstall(SignedFor(ThisPc, Now.AddDays(30)), ThisPc, Now, out _));

            // Same file, different machine - this is the copy-it-to-your-mate case.
            Assert.Equal(LicenceStatus.WrongMachine, Store().Read("hw-a-friends-pc", Now).Status);
        }

        [Fact]
        public void An_already_expired_licence_is_not_installed()
        {
            Assert.False(Store().TryInstall(SignedFor(ThisPc, Now.AddDays(-1)), ThisPc, Now, out var error));
            Assert.Contains("expired", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Rubbish_pasted_into_the_box_is_refused_without_throwing()
        {
            var store = Store();
            foreach (var junk in new[] { "", "   ", "hello", "{}", "{\"doc\":\"x\",\"sig\":\"y\"}" })
                Assert.False(store.TryInstall(junk, ThisPc, Now, out _));
        }

        [Fact]
        public void A_corrupt_licence_file_reads_as_invalid_rather_than_crashing()
        {
            File.WriteAllText(_path, "not a licence at all");
            Assert.Equal(LicenceStatus.Invalid, Store().Read(ThisPc, Now).Status);
        }

        // ---- the passage of time -----------------------------------------------------------

        [Fact]
        public void A_licence_that_runs_out_reads_as_expired_rather_than_staying_valid()
        {
            Assert.True(Store().TryInstall(SignedFor(ThisPc, Now.AddDays(30)), ThisPc, Now, out _));

            Assert.Equal(LicenceStatus.Valid, Store().Read(ThisPc, Now.AddDays(29)).Status);
            Assert.Equal(LicenceStatus.Expired, Store().Read(ThisPc, Now.AddDays(31)).Status);
        }

        [Fact]
        public void No_licence_file_means_no_licence()
        {
            Assert.Equal(LicenceStatus.None, Store().Read(ThisPc, Now).Status);
        }

        [Fact]
        public void Clear_removes_the_licence()
        {
            var store = Store();
            Assert.True(store.TryInstall(SignedFor(ThisPc, Now.AddDays(30)), ThisPc, Now, out _));
            store.Clear();
            Assert.Equal(LicenceStatus.None, store.Read(ThisPc, Now).Status);
        }

        // ---- the shipped key ----------------------------------------------------------------

        [Fact]
        public void The_shipped_verification_key_is_a_usable_public_key()
        {
            // Guards against a paste accident: a truncated or reordered key would still
            // compile, and every licence would silently fail to verify on customers' PCs.
            var doc = new LicenceDocument("PLX-0001", PlanCatalog.Monthly, Now, Now.AddDays(30), ThisPc);
            LicenceSigner.CreateKeyPair(out var otherPrivate, out _);

            // Signed by a different key, so this must come back false - but it must come back,
            // not throw, which is what proves the shipped key imports cleanly.
            Assert.False(LicenceVerifier.TryVerify(
                LicenceSigner.Sign(doc, otherPrivate), LicenceKeys.Verification, out _));
        }
    }
}
