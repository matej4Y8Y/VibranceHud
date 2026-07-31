// The owner's record of every key ever sold. Getting this wrong means either losing track of
// a paying customer or handing the same identity to two of them.

using System;
using System.Collections.Generic;
using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class KeyLedgerTests
    {
        private static readonly DateTime Now = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        private static KeyRecord Key(string code, string plan = PlanCatalog.Monthly) =>
            new() { Code = code, Plan = plan, IssuedUtc = Now };

        private static IReadOnlyList<KeyRecord> Empty => new List<KeyRecord>();

        [Fact]
        public void NewKeyStartsUnused()
        {
            Assert.Equal(KeyStatus.Unused, Key("AAAA-BBBB-CCCC-DDDD").StatusAt(Now));
        }

        /// <summary>The clock starts at activation, not generation - otherwise a key sitting
        /// unsold in the ledger quietly expires before anyone receives it.</summary>
        [Fact]
        public void UnusedKeyHasNoExpiry()
        {
            Assert.Null(Key("AAAA-BBBB-CCCC-DDDD").ExpiresUtc);
        }

        [Fact]
        public void ActivatedKeyExpiresOnePlanDurationLater()
        {
            var ledger = KeyLedger.MarkActivated(
                KeyLedger.Add(Empty, Key("AAAA-BBBB-CCCC-DDDD")),
                "AAAA-BBBB-CCCC-DDDD", "PC1", Now);

            var key = KeyLedger.Find(ledger, "AAAA-BBBB-CCCC-DDDD")!;
            Assert.Equal(Now.AddDays(30), key.ExpiresUtc);
            Assert.Equal(KeyStatus.Active, key.StatusAt(Now.AddDays(15)));
            Assert.Equal(KeyStatus.Expired, key.StatusAt(Now.AddDays(31)));
        }

        [Fact]
        public void RevokedBeatsEveryOtherStatus()
        {
            var ledger = KeyLedger.Revoke(
                KeyLedger.MarkActivated(KeyLedger.Add(Empty, Key("A")), "A", "PC1", Now),
                "A");

            Assert.Equal(KeyStatus.Revoked, KeyLedger.Find(ledger, "A")!.StatusAt(Now));
        }

        [Fact]
        public void RevokeCanBeUndone()
        {
            var ledger = KeyLedger.Restore(KeyLedger.Revoke(KeyLedger.Add(Empty, Key("A")), "A"), "A");
            Assert.Equal(KeyStatus.Unused, KeyLedger.Find(ledger, "A")!.StatusAt(Now));
        }

        /// <summary>The one that saves a paying customer who changes their GPU: releasing
        /// detaches the key from their old PC so they can activate again.</summary>
        [Fact]
        public void ReleasingAKeyMakesItUsableAgain()
        {
            var ledger = KeyLedger.MarkActivated(KeyLedger.Add(Empty, Key("A")), "A", "OLD-PC", Now);
            Assert.Equal(KeyStatus.Active, KeyLedger.Find(ledger, "A")!.StatusAt(Now));

            ledger = KeyLedger.Release(ledger, "A");

            var key = KeyLedger.Find(ledger, "A")!;
            Assert.Equal(KeyStatus.Unused, key.StatusAt(Now));
            Assert.False(key.IsActivated);
            Assert.Equal("", key.ActivatedBy);
        }

        /// <summary>Two records sharing a code would mean two customers sharing one identity.</summary>
        [Fact]
        public void DuplicateCodesAreNotAdded()
        {
            var ledger = KeyLedger.Add(KeyLedger.Add(Empty, Key("A")), Key("A"));
            Assert.Single(ledger);
        }

        [Fact]
        public void CodesAreMatchedCaseInsensitively()
        {
            var ledger = KeyLedger.Revoke(KeyLedger.Add(Empty, Key("2K7M-Q8XR-T9WD-N3FG")),
                                          "2k7m-q8xr-t9wd-n3fg");
            Assert.True(KeyLedger.Find(ledger, "2K7M-Q8XR-T9WD-N3FG")!.Revoked);
        }

        [Fact]
        public void OperationsOnAnUnknownCodeChangeNothing()
        {
            var ledger = KeyLedger.Add(Empty, Key("A"));
            Assert.Equal(ledger, KeyLedger.Revoke(ledger, "NOPE"));
            Assert.Equal(ledger, KeyLedger.Release(ledger, "NOPE"));
            Assert.Null(KeyLedger.Find(ledger, "NOPE"));
        }

        [Fact]
        public void StatsCountEveryCategory()
        {
            var ledger = Empty;
            ledger = KeyLedger.Add(ledger, Key("UNUSED"));
            ledger = KeyLedger.Add(ledger, Key("ACTIVE"));
            ledger = KeyLedger.Add(ledger, Key("EXPIRED"));
            ledger = KeyLedger.Add(ledger, Key("REVOKED"));
            ledger = KeyLedger.MarkActivated(ledger, "ACTIVE", "PC1", Now);
            ledger = KeyLedger.MarkActivated(ledger, "EXPIRED", "PC2", Now.AddDays(-40));
            ledger = KeyLedger.Revoke(ledger, "REVOKED");

            var stats = KeyLedger.Stats(ledger, Now);

            Assert.Equal(4, stats.Total);
            Assert.Equal(1, stats.Unused);
            Assert.Equal(1, stats.Active);
            Assert.Equal(1, stats.Expired);
            Assert.Equal(1, stats.Revoked);
            Assert.Equal(2, stats.ActivatedEver);
            Assert.Equal(0.5, stats.ActivationRate);
        }

        [Fact]
        public void ActivationRateOfAnEmptyLedgerIsZeroNotAnError()
        {
            Assert.Equal(0, KeyLedger.Stats(Empty, Now).ActivationRate);
        }

        /// <summary>Who to message about renewing, soonest first.</summary>
        [Fact]
        public void ExpiringWithinListsSoonestFirst()
        {
            var ledger = Empty;
            ledger = KeyLedger.Add(ledger, Key("LATER"));
            ledger = KeyLedger.Add(ledger, Key("SOONER"));
            ledger = KeyLedger.MarkActivated(ledger, "LATER", "PC1", Now.AddDays(-20)); // 10 left
            ledger = KeyLedger.MarkActivated(ledger, "SOONER", "PC2", Now.AddDays(-27)); // 3 left

            var due = KeyLedger.ExpiringWithin(ledger, TimeSpan.FromDays(14), Now);

            Assert.Equal(2, due.Count);
            Assert.Equal("SOONER", due[0].Code);
            Assert.Equal("LATER", due[1].Code);
        }

        [Fact]
        public void ExpiringWithinIgnoresUnusedAndRevokedKeys()
        {
            var ledger = Empty;
            ledger = KeyLedger.Add(ledger, Key("UNUSED"));
            ledger = KeyLedger.Add(ledger, Key("REVOKED"));
            ledger = KeyLedger.MarkActivated(ledger, "REVOKED", "PC1", Now);
            ledger = KeyLedger.Revoke(ledger, "REVOKED");

            Assert.Empty(KeyLedger.ExpiringWithin(ledger, TimeSpan.FromDays(365), Now));
        }
    }
}
