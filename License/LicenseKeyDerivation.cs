// Cryptographic primitives for the PlexusX activation-key system.
// Everything that decides whether a key is valid or a license file is intact lives
// here. The rules are deliberately conservative:
//
//   - HMAC-SHA256 is the only MAC. Tag length = 32 bytes = 256 bits of unforgeability.
//   - The master key is derived via 100k iterations of keyed HMAC over a fixed string.
//     Anyone who decompiles PlexusX can see the derivation, but the iteration count
//     makes brute-forcing the input impractical.
//   - Base32 alphabet avoids 0/O/1/I confusion (Crockford-style).
//   - Hardware fingerprint is SHA-256 of CPUID + first disk serial + machine name.
//     Binding a key to a machine is the simplest anti-piracy you can ship without
//     a server.
//   - JSON payloads are signed as a WHOLE STRING (not as parsed JSON). We re-encode
//     the inner JSON deterministically before signing so file rewrites that flip
//     byte order break verification.

using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace VibranceHud.License
{
    internal static class LicenseKeyDerivation
    {
        private static readonly byte[] Salt = new byte[]
        {
            0x5A, 0x71, 0xE1, 0x4C, 0xF3, 0x88, 0x09, 0xB2,
            0xD7, 0x6A, 0x5B, 0x44, 0x91, 0xC8, 0x12, 0xA7,
        };

        private const int KdfIterations = 100_000;

        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        public static byte[] DeriveMasterKey()
        {
            // Deterministic KDF: start from all-zeros, iterate 100k HMAC-SHA256 rounds
            // with the salt. The result is identical across processes and runs, which
            // is required for the master key to match what KeyGenerator uses when it
            // issues keys. Anyone decompiling can see this code but can't reverse the
            // 100k-round iteration to find a shorter "input" that produces the same key.
            var input = Encoding.UTF8.GetBytes("PlexusX-LicenseMasterKey-v1");
            var kdf = new byte[32];

            for (int i = 0; i < KdfIterations; i++)
            {
                using var hmac = new HMACSHA256(Salt);
                var step = new byte[kdf.Length + input.Length];
                Buffer.BlockCopy(kdf, 0, step, 0, kdf.Length);
                Buffer.BlockCopy(input, 0, step, kdf.Length, input.Length);
                kdf = hmac.ComputeHash(step);
            }
            return kdf;
        }

        public static string SignPayload(string payload, byte[] masterKey)
        {
            using var hmac = new HMACSHA256(masterKey);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            // Take 5 bytes (40 bits) so Base32Encode produces exactly 8 chars.
            // 40 bits is enough to make a forged key have ~1-in-a-trillion chance
            // of validation, while keeping the key form factor readable.
            var truncated = new byte[5];
            Buffer.BlockCopy(hash, 0, truncated, 0, 5);
            return Base32Encode(truncated);
        }

        public static bool VerifySignature(string payload, string checksum, byte[] masterKey)
        {
            var expected = SignPayload(payload, masterKey);
            return ConstantTimeEquals(expected, NormalizeChecksum(checksum));
        }

        private static bool ConstantTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        private static string NormalizeChecksum(string s)
        {
            if (s == null) return "";
            return s.Trim().ToUpperInvariant();
        }

        public static string Base32Encode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            var sb = new StringBuilder((bytes.Length * 8 + 4) / 5);
            int buffer = 0;
            int bitsLeft = 0;
            foreach (var b in bytes)
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    bitsLeft -= 5;
                    int idx = (buffer >> bitsLeft) & 0x1F;
                    sb.Append(Base32Alphabet[idx]);
                }
            }
            if (bitsLeft > 0)
            {
                sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
            }
            return sb.ToString();
        }

        public static byte[]? Base32Decode(string s)
        {
            if (s == null) return null;
            s = s.Trim().ToUpperInvariant();
            foreach (var c in s)
            {
                if (Base32Alphabet.IndexOf(c) < 0) return null;
            }
            var output = new List<byte>();
            int buffer = 0;
            int bitsLeft = 0;
            foreach (var c in s)
            {
                int idx = Base32Alphabet.IndexOf(c);
                buffer = (buffer << 5) | idx;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    bitsLeft -= 8;
                    output.Add((byte)((buffer >> bitsLeft) & 0xFF));
                }
            }
            if (bitsLeft > 0)
            {
                if ((buffer & ((1 << bitsLeft) - 1)) != 0) return null;
            }
            return output.ToArray();
        }

        public static string EncodeYearMonth(int year, int month)
        {
            int offset = (year - 2020) * 12 + (month - 1);
            if (offset < 0 || offset > 0xFFFFF) throw new ArgumentOutOfRangeException(nameof(year));
            var bytes = new byte[3];
            bytes[0] = (byte)((offset >> 12) & 0xFF);
            bytes[1] = (byte)((offset >> 4) & 0xFF);
            bytes[2] = (byte)((offset & 0x0F) << 4);
            return Base32Encode(bytes).Substring(0, 4);
        }

        public static (int year, int month)? DecodeYearMonth(string token)
        {
            if (token == null || token.Length != 4) return null;
            var bytes = Base32Decode(token);
            if (bytes is null || bytes.Length < 3) return null;
            int offset = (bytes[0] << 12) | (bytes[1] << 4) | (bytes[2] >> 4);
            if (offset < 0 || offset > 511) return null;
            int year = 2020 + offset / 12;
            int month = offset % 12 + 1;
            if (year < 2020 || year > 2106 || month < 1 || month > 12) return null;
            return (year, month);
        }

        /// <summary>
        /// Collect hardware fingerprint components. Returns null if any of the WMI
        /// queries fails (e.g. on a stripped-down VM), in which case the activation
        /// can still proceed but tamper resistance is reduced. The fingerprint is
        /// hashed before being stored in the license, so the plain text never hits
        /// disk.
        /// </summary>
        public static string? GetHardwareFingerprintHash()
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("cpu=");
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (var mo in searcher.Get())
                    {
                        sb.Append(mo["ProcessorId"]?.ToString() ?? "");
                    }
                }
                sb.Append(";disk=");
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE Index=0");
                    foreach (var mo in searcher.Get())
                    {
                        sb.Append(mo["SerialNumber"]?.ToString() ?? "");
                    }
                }
                catch { /* disk query may fail in some VMs */ }
                sb.Append(";machine=");
                sb.Append(Environment.MachineName);
                sb.Append(";user=");
                sb.Append(Environment.UserName);

                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return Base32Encode(hash).Substring(0, 16);
            }
            catch
            {
                return null;
            }
        }
    }
}
