using System;
using System.Collections.Generic;
using System.Linq;

namespace VibranceHud.Licensing
{
    /// <summary>A snapshot of the business, computed from the ledger.</summary>
    public sealed record KeyStats(
        int Total,
        int Unused,
        int Active,
        int Expired,
        int Revoked,
        int ActivatedEver)
    {
        /// <summary>Of the keys handed out, how many were actually redeemed. Low means keys
        /// are being generated and never delivered, or people aren't installing.</summary>
        public double ActivationRate => Total == 0 ? 0 : (double)ActivatedEver / Total;
    }

    /// <summary>
    /// Every key ever issued, and what can be asked of that list.
    ///
    /// Pure operations over an immutable list - the file and the UI live elsewhere, so all of
    /// this is testable without either. Each method returns a new list rather than mutating,
    /// which keeps "what changed" obvious at the call site and makes an accidental
    /// double-revoke impossible to miss.
    /// </summary>
    public static class KeyLedger
    {
        public static IReadOnlyList<KeyRecord> Add(IReadOnlyList<KeyRecord> ledger, KeyRecord record)
        {
            // A duplicate code would mean two customers sharing one identity in the ledger.
            if (ledger.Any(k => Same(k.Code, record.Code))) return ledger;
            return ledger.Append(record).ToList();
        }

        public static IReadOnlyList<KeyRecord> Revoke(IReadOnlyList<KeyRecord> ledger, string code) =>
            ledger.Select(k => Same(k.Code, code) ? k with { Revoked = true } : k).ToList();

        public static IReadOnlyList<KeyRecord> Restore(IReadOnlyList<KeyRecord> ledger, string code) =>
            ledger.Select(k => Same(k.Code, code) ? k with { Revoked = false } : k).ToList();

        /// <summary>
        /// Detach a key from the PC that redeemed it, so it can be used again.
        ///
        /// This is what saves a paying customer who changes GPU or reinstalls Windows: their
        /// hardware id changes, their key stops matching, and without this their only option is
        /// asking for a refund.
        /// </summary>
        public static IReadOnlyList<KeyRecord> Release(IReadOnlyList<KeyRecord> ledger, string code) =>
            ledger.Select(k => Same(k.Code, code)
                ? k with { ActivatedBy = "", ActivatedUtc = null }
                : k).ToList();

        public static IReadOnlyList<KeyRecord> MarkActivated(
            IReadOnlyList<KeyRecord> ledger, string code, string hardwareId, DateTime whenUtc) =>
            ledger.Select(k => Same(k.Code, code)
                ? k with { ActivatedBy = hardwareId, ActivatedUtc = whenUtc }
                : k).ToList();

        public static KeyRecord? Find(IReadOnlyList<KeyRecord> ledger, string code) =>
            ledger.FirstOrDefault(k => Same(k.Code, code));

        public static KeyStats Stats(IReadOnlyList<KeyRecord> ledger, DateTime nowUtc)
        {
            int unused = 0, active = 0, expired = 0, revoked = 0, activatedEver = 0;

            foreach (var k in ledger)
            {
                switch (k.StatusAt(nowUtc))
                {
                    case KeyStatus.Unused: unused++; break;
                    case KeyStatus.Active: active++; break;
                    case KeyStatus.Expired: expired++; break;
                    case KeyStatus.Revoked: revoked++; break;
                }
                if (k.IsActivated) activatedEver++;
            }

            return new KeyStats(ledger.Count, unused, active, expired, revoked, activatedEver);
        }

        /// <summary>Keys expiring within the given window - who to contact about renewing.</summary>
        public static IReadOnlyList<KeyRecord> ExpiringWithin(
            IReadOnlyList<KeyRecord> ledger, TimeSpan window, DateTime nowUtc) =>
            ledger.Where(k => k.StatusAt(nowUtc) == KeyStatus.Active)
                  .Where(k => k.RemainingAt(nowUtc) is { } left && left <= window)
                  .OrderBy(k => k.ExpiresUtc)
                  .ToList();

        /// <summary>Codes are compared case-insensitively because they're read off screens and
        /// retyped; the canonical form is upper case.</summary>
        private static bool Same(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
