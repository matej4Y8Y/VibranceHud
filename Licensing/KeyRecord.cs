using System;

namespace VibranceHud.Licensing
{
    /// <summary>Where a key is in its life. Derived from the record, never stored - a stored
    /// status would drift the moment a licence expired while nothing was looking.</summary>
    public enum KeyStatus
    {
        /// <summary>Generated but never redeemed. Safe to hand out.</summary>
        Unused,

        /// <summary>Redeemed and still within its window.</summary>
        Active,

        /// <summary>Redeemed, and its window has passed.</summary>
        Expired,

        /// <summary>Manually killed. Never valid again, redeemed or not.</summary>
        Revoked,
    }

    /// <summary>
    /// One key as the owner sees it: what it is, who has it, and what it's doing.
    ///
    /// This is the seller's record, not the customer's licence - it holds the note about who
    /// bought it and when it was activated, none of which belongs in a signed licence.
    /// </summary>
    public sealed record KeyRecord
    {
        public string Code { get; init; } = "";
        public string Plan { get; init; } = "";
        public DateTime IssuedUtc { get; init; }

        /// <summary>Free text - who bought it, which Discord name, what they paid. Purely for
        /// the owner; never leaves this machine.</summary>
        public string Note { get; init; } = "";

        /// <summary>Hardware id of the PC that redeemed it, or empty if never redeemed.</summary>
        public string ActivatedBy { get; init; } = "";

        public DateTime? ActivatedUtc { get; init; }

        public bool Revoked { get; init; }

        public bool IsActivated => !string.IsNullOrEmpty(ActivatedBy) && ActivatedUtc != null;

        /// <summary>When this key stops working, or null if it has never been redeemed - the
        /// clock starts at activation, not at generation, so an unsold key doesn't quietly
        /// expire in a drawer.</summary>
        public DateTime? ExpiresUtc
        {
            get
            {
                if (ActivatedUtc == null) return null;
                var duration = PlanCatalog.DurationFor(Plan);
                return duration == null ? null : ActivatedUtc.Value + duration.Value;
            }
        }

        public KeyStatus StatusAt(DateTime nowUtc)
        {
            if (Revoked) return KeyStatus.Revoked;
            if (!IsActivated) return KeyStatus.Unused;

            var expires = ExpiresUtc;
            if (expires == null) return KeyStatus.Active; // unknown plan - don't guess it's dead
            return nowUtc >= expires.Value ? KeyStatus.Expired : KeyStatus.Active;
        }

        /// <summary>How long is left, or null when that isn't a meaningful question.</summary>
        public TimeSpan? RemainingAt(DateTime nowUtc)
        {
            var expires = ExpiresUtc;
            if (expires == null) return null;
            var left = expires.Value - nowUtc;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }
}
