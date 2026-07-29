// Pure logic for the key-revocation list: hashing a serial and checking it against
// a known-revoked set. No I/O here on purpose - RevocationService owns fetching and
// caching the list; this class only has to agree on the hash and the JSON shape, so
// it's fully unit-testable without touching the network or the filesystem.
//
// Why hash instead of listing plaintext keys: the list is meant to live in the public
// GitHub repo (same free-hosting trick the auto-updater already uses for release
// metadata). Publishing raw serials there would hand out a list of every key the
// developer has ever revoked - some of which may still be legitimately in someone's
// hands as a paid key going through a dispute, not a leak. A SHA-256 hash lets the
// app check membership without the repo itself being a directory of real keys.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibranceHud.License
{
    public sealed class RevocationListData
    {
        [JsonPropertyName("revokedSerialHashes")]
        public List<string> RevokedSerialHashes { get; set; } = new();
    }

    public static class RevocationList
    {
        /// <summary>SHA-256 of the serial, normalised the same way LicenseKey.Parse
        /// normalises user input (trim + uppercase) so the hash is stable no matter
        /// how the key was typed, pasted, or read back off a LicensePayload.</summary>
        public static string HashSerial(string serial)
        {
            var normalised = (serial ?? "").Trim().ToUpperInvariant();
            var bytes = Encoding.UTF8.GetBytes(normalised);
            var hash = SHA256.HashData(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>Parses the revocation list JSON. Returns an empty set (not null)
        /// on any malformed input - a bad fetch should never crash the license check,
        /// it should just mean "nothing known to be revoked yet".</summary>
        public static IReadOnlySet<string> Parse(string json)
        {
            try
            {
                var data = JsonSerializer.Deserialize<RevocationListData>(json);
                if (data?.RevokedSerialHashes == null) return new HashSet<string>();
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in data.RevokedSerialHashes)
                    if (!string.IsNullOrWhiteSpace(h)) set.Add(h.Trim());
                return set;
            }
            catch
            {
                return new HashSet<string>();
            }
        }

        public static string Serialize(IEnumerable<string> revokedHashes)
        {
            var data = new RevocationListData { RevokedSerialHashes = new List<string>(revokedHashes) };
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }

        public static bool IsRevoked(string serial, IReadOnlySet<string> revokedHashes) =>
            revokedHashes.Contains(HashSerial(serial));
    }
}
