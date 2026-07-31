using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VibranceHud.Licensing;

namespace PlexusXKeys
{
    /// <summary>
    /// Everything this tool keeps on disk: the signing key, and the ledger of issued keys.
    ///
    /// Both live under %LocalAppData%\PlexusXKeys and never go near the repository. The private
    /// key in particular must never be committed - anyone holding it can mint licences, which
    /// is the exact power this whole design exists to keep in one pair of hands.
    ///
    /// The key file is created once, on first run, and then only read. There is deliberately no
    /// "regenerate" button: a new key pair silently invalidates every licence ever issued, and
    /// that is not something anyone should be one misclick away from.
    /// </summary>
    public static class KeyVault
    {
        private static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlexusXKeys");

        private static readonly string PrivateKeyPath = Path.Combine(Dir, "signing-key.private");
        private static readonly string PublicKeyPath = Path.Combine(Dir, "signing-key.public");
        private static readonly string LedgerPath = Path.Combine(Dir, "keys.json");

        public static string Location => Dir;
        public static string PublicKeyFile => PublicKeyPath;

        /// <summary>True the first time this tool is run, before a key pair exists.</summary>
        public static bool NeedsSetup => !File.Exists(PrivateKeyPath);

        /// <summary>
        /// Create the signing key pair. Refuses if one already exists, because overwriting it
        /// would invalidate every licence previously issued with no way back.
        /// </summary>
        public static void CreateSigningKey()
        {
            if (File.Exists(PrivateKeyPath))
                throw new InvalidOperationException(
                    "A signing key already exists. Overwriting it would invalidate every " +
                    "licence ever issued.");

            Directory.CreateDirectory(Dir);
            LicenceSigner.CreateKeyPair(out var priv, out var pub);
            File.WriteAllBytes(PrivateKeyPath, priv);
            File.WriteAllBytes(PublicKeyPath, pub);
        }

        public static byte[] PrivateKey() => File.ReadAllBytes(PrivateKeyPath);

        public static byte[] PublicKey() => File.ReadAllBytes(PublicKeyPath);

        /// <summary>The public key as a C# literal, ready to paste into the app. This is the
        /// half that ships - it can verify licences and cannot create them.</summary>
        public static string PublicKeyAsCSharp()
        {
            var bytes = PublicKey();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// PlexusX licence verification key. Safe to ship: it verifies, it");
            sb.AppendLine("// cannot sign. The private half stays in PlexusX Keys.");
            sb.AppendLine("private static readonly byte[] LicencePublicKey =");
            sb.AppendLine("{");
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i % 12 == 0) sb.Append("    ");
                sb.Append("0x").Append(bytes[i].ToString("X2")).Append(", ");
                if (i % 12 == 11) sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("};");
            return sb.ToString();
        }

        public static IReadOnlyList<KeyRecord> LoadLedger()
        {
            try
            {
                if (!File.Exists(LedgerPath)) return new List<KeyRecord>();
                return JsonSerializer.Deserialize<List<KeyRecord>>(File.ReadAllText(LedgerPath))
                       ?? new List<KeyRecord>();
            }
            catch
            {
                // A corrupt ledger must not look like an empty one - that would invite
                // overwriting it with nothing on the next save.
                throw new InvalidOperationException(
                    $"The key ledger at {LedgerPath} could not be read. It has been left " +
                    "untouched - move it aside manually if you want to start fresh.");
            }
        }

        /// <summary>
        /// Write the ledger through a temp file, then swap. A half-written keys.json is the
        /// record of who paid you, so a crash mid-save must not be able to shred it.
        /// </summary>
        public static void SaveLedger(IReadOnlyList<KeyRecord> ledger)
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(ledger, new JsonSerializerOptions { WriteIndented = true });

            var temp = LedgerPath + ".tmp";
            File.WriteAllText(temp, json);

            if (File.Exists(LedgerPath))
            {
                var backup = LedgerPath + ".bak";
                File.Copy(LedgerPath, backup, overwrite: true);
            }
            File.Move(temp, LedgerPath, overwrite: true);
        }
    }
}
