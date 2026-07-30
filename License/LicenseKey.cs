// A parsed activation key. The user types it into the activation dialog; we parse
// it into typed fields so the rest of the system can reason about the key without
// re-parsing every time.
//
// Format: YYYY-R-T-BBBBBBBB-CCCCCCCC   (example: AACO-R-F-7NXVFVEO-UMJPAPPY)
//  YYYY     = year+month encoded as 4 base32 chars
//  R        = single release marker char ('R' = release, 'B' = beta)
//  T        = single tier marker char ('F' = free, 'T' = trial, 'P' = paid)
//  BBBBBBBB = 8-char base32 body (random)
//  CCCCCCCC = 8-char base32 HMAC checksum (40 bits, see SignPayload)
//
// Total: 5 groups separated by '-'; 22 base32 chars, 26 including the dashes.
// Parse() below is the authority on those lengths - keep this comment in step
// with it, since the dialog quotes the format to the user.

using System;

namespace VibranceHud.License
{
    public sealed class LicenseKey
    {
        public string YearMonthToken { get; }
        public char TierMarker { get; }
        public char ReleaseMarker { get; }
        public string Body { get; }
        public string Checksum { get; }

        public string Serial =>
            $"{YearMonthToken}-{ReleaseMarker}-{TierMarker}-{Body}-{Checksum}";

        /// <summary>The 4-group payload that was HMAC'd when the key was issued
        /// (everything except the trailing checksum). Both the issuer and the
        /// verifier must agree on this exact byte sequence.</summary>
        public string SignedPayload =>
            $"{YearMonthToken}-{ReleaseMarker}-{TierMarker}-{Body}";

        public LicenseKey(string yearMonth, char releaseMarker, char tierMarker,
            string body, string checksum)
        {
            YearMonthToken = yearMonth;
            ReleaseMarker = releaseMarker;
            TierMarker = tierMarker;
            Body = body;
            Checksum = checksum;
        }

        public enum Kind
        {
            /// <summary>Zero value on purpose, so `default(Kind)` is "no tier" rather than a
            /// real one. Free used to occupy zero, which meant any uninitialised or
            /// failed-to-resolve Kind silently read as the 365-day tier - the most generous
            /// one. Nothing should ever be granted on the strength of this value.</summary>
            Unknown = 0,

            Free,
            Trial,
            Paid,

            /// <summary>Short-lived demo key - hours, not months. Marker 'H'. Exists so a key
            /// can be handed out for a session without relying on the revocation list being
            /// fetched (and without having to remember to revoke it).</summary>
            Temp,

            /// <summary>One week. Marker 'W'. The useful middle ground between a single
            /// session and a month - long enough for someone to actually test through a
            /// weekend, short enough to expire on its own.</summary>
            Week,
        }

        /// <summary>
        /// Resolve the tier marker, or fail if this build doesn't recognise it.
        ///
        /// GetKind used to fall through to Free for anything unknown - and Free is the 365-day
        /// tier, so an unrecognised marker granted a full year. That's the opposite of failing
        /// safe, and it was real rather than hypothetical: 'W' (week) was added after 0.9.7
        /// shipped, so a week key given to anyone still on 0.9.7 would have been read as a year.
        ///
        /// A build genuinely cannot know how long a tier it's never heard of is meant to last,
        /// so refusing is the only honest answer. The signature check is unaffected - this is
        /// about a validly signed key whose tier is from the future or simply wrong.
        /// </summary>
        public bool TryGetKind(out Kind kind)
        {
            switch (TierMarker)
            {
                case 'F': kind = Kind.Free; return true;
                case 'T': kind = Kind.Trial; return true;
                case 'P': kind = Kind.Paid; return true;
                case 'H': kind = Kind.Temp; return true;
                case 'W': kind = Kind.Week; return true;
                default: kind = default; return false;
            }
        }

        /// <summary>Tier for a marker already known to be valid. Callers that haven't checked
        /// should use <see cref="TryGetKind"/> - this throws rather than inventing a tier.</summary>
        public Kind GetKind() =>
            TryGetKind(out var kind)
                ? kind
                : throw new InvalidOperationException(
                    $"Unrecognised licence tier marker '{TierMarker}'. This key was likely issued " +
                    "by a newer version of PlexusX.");

        public static LicenseKey? Parse(string s)
        {
            if (s == null) return null;
            s = s.Trim().ToUpperInvariant();
            var parts = s.Split('-');
            if (parts.Length != 5) return null;
            if (parts[0].Length != 4 || parts[1].Length != 1 || parts[2].Length != 1 ||
                parts[3].Length != 8 || parts[4].Length != 8) return null;

            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            // Only check parts for alphabet - the dashes between groups are not base32.
            for (int i = 0; i < parts.Length; i++)
            {
                foreach (var c in parts[i])
                {
                    if (alphabet.IndexOf(c) < 0) return null;
                }
            }

            try
            {
                return new LicenseKey(parts[0], parts[1][0], parts[2][0], parts[3], parts[4]);
            }
            catch
            {
                return null;
            }
        }
    }
}
