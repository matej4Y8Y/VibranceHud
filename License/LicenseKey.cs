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
            Free,
            Trial,
            Paid,
        }

        public Kind GetKind()
        {
            switch (TierMarker)
            {
                case 'F': return Kind.Free;
                case 'T': return Kind.Trial;
                case 'P': return Kind.Paid;
                default: return Kind.Free;
            }
        }

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
