// Activation key generator. Run from a terminal:
//
//   dotnet run --project Tools/KeyGenerator -- --tier free --count 4
//
// Outputs one key per line, ready to copy-paste. Keys are deterministic in
// structure (year-month + release + tier + body + checksum) but the body is
// random so the developer can hand them out without a central registry.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using VibranceHud.License;

internal static class Program
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>The published revocation list, at the repo root so
    /// raw.githubusercontent.com serves it at the URL RevocationService fetches.</summary>
    private const string RevocationFile = "license-revocations.json";

    private static int Main(string[] args)
    {
        string tier = "free";
        int count = 1;
        int? year = null;
        int? month = null;
        bool overrideYearMonth = false;
        var toRevoke = new List<string>();
        var toRestore = new List<string>();
        bool listRevoked = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--revoke" when i + 1 < args.Length:
                    toRevoke.Add(args[++i]);
                    break;
                case "--restore" when i + 1 < args.Length:
                    toRestore.Add(args[++i]);
                    break;
                case "--list-revoked":
                    listRevoked = true;
                    break;
                case "--tier" when i + 1 < args.Length:
                    tier = args[++i].ToLowerInvariant();
                    break;
                case "--count" when i + 1 < args.Length:
                    count = int.Parse(args[++i]);
                    break;
                case "--year" when i + 1 < args.Length:
                    year = int.Parse(args[++i]);
                    overrideYearMonth = true;
                    break;
                case "--month" when i + 1 < args.Length:
                    month = int.Parse(args[++i]);
                    overrideYearMonth = true;
                    break;
                case "--help" or "-h":
                    PrintUsage();
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown arg: {args[i]}");
                    PrintUsage();
                    return 1;
            }
        }

        // Revocation modes run instead of key generation, not alongside it.
        if (listRevoked) return ListRevoked();
        if (toRevoke.Count > 0 || toRestore.Count > 0) return UpdateRevocations(toRevoke, toRestore);

        char marker = tier switch
        {
            "free" => 'F',
            "trial" => 'T',
            "paid" => 'P',
            _ => throw new ArgumentException($"Unknown tier: {tier}. Use free, trial, or paid."),
        };

        if (!overrideYearMonth)
        {
            var now = DateTime.UtcNow;
            year = now.Year;
            month = now.Month;
        }

        var yearMonthToken = LicenseKeyDerivation.EncodeYearMonth(year!.Value, month!.Value);
        var masterKey = LicenseKeyDerivation.DeriveMasterKey();

        var rng = RandomNumberGenerator.Create();
        for (int i = 0; i < count; i++)
        {
            var body = RandomBase32(rng, 8);
            var payload = $"{yearMonthToken}-R-{marker}-{body}";
            var checksum = LicenseKeyDerivation.SignPayload(payload, masterKey);
            var key = $"{payload}-{checksum}";
            Console.WriteLine(key);
        }
        return 0;
    }

    /// <summary>Walks up from the working directory to find the repo root (the folder
    /// holding VibranceHud.csproj), so the tool edits the real published list no matter
    /// which subdirectory it was invoked from.</summary>
    private static string ResolveRevocationPath()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "VibranceHud.csproj")))
                return Path.Combine(dir.FullName, RevocationFile);
            dir = dir.Parent;
        }
        return Path.GetFullPath(RevocationFile);
    }

    private static HashSet<string> ReadRevocations(string path)
    {
        if (!File.Exists(path)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return new HashSet<string>(RevocationList.Parse(File.ReadAllText(path)),
            StringComparer.OrdinalIgnoreCase);
    }

    private static int ListRevoked()
    {
        var path = ResolveRevocationPath();
        var hashes = ReadRevocations(path);
        Console.WriteLine($"Revocation list: {path}");
        if (hashes.Count == 0)
        {
            Console.WriteLine("  (empty - no keys revoked)");
            return 0;
        }
        Console.WriteLine($"  {hashes.Count} key(s) revoked:");
        foreach (var h in hashes.OrderBy(x => x, StringComparer.Ordinal))
            Console.WriteLine($"    {h}");
        Console.WriteLine();
        Console.WriteLine("Hashes are one-way - the original keys can't be recovered from this list.");
        Console.WriteLine("To check whether a specific key is revoked, run --revoke on it and see if it reports 'already revoked'.");
        return 0;
    }

    private static int UpdateRevocations(List<string> revoke, List<string> restore)
    {
        var path = ResolveRevocationPath();
        var hashes = ReadRevocations(path);

        foreach (var raw in revoke)
        {
            var key = LicenseKey.Parse(raw);
            if (key == null)
            {
                Console.Error.WriteLine($"Not a valid key, skipping: {raw}");
                Console.Error.WriteLine("  (expected format AAAA-R-P-XXXXXXXX-XXXXXXXX)");
                return 1;
            }
            var hash = RevocationList.HashSerial(key.Serial);
            if (hashes.Add(hash)) Console.WriteLine($"Revoked  {key.Serial}");
            else Console.WriteLine($"Already revoked  {key.Serial}");
        }

        foreach (var raw in restore)
        {
            var key = LicenseKey.Parse(raw);
            if (key == null)
            {
                Console.Error.WriteLine($"Not a valid key, skipping: {raw}");
                return 1;
            }
            var hash = RevocationList.HashSerial(key.Serial);
            if (hashes.Remove(hash)) Console.WriteLine($"Restored  {key.Serial}");
            else Console.WriteLine($"Was not revoked  {key.Serial}");
        }

        File.WriteAllText(path, RevocationList.Serialize(
            hashes.OrderBy(x => x, StringComparer.Ordinal)));

        Console.WriteLine();
        Console.WriteLine($"Wrote {path} ({hashes.Count} revoked).");
        Console.WriteLine("Commit and push it for the change to reach users:");
        Console.WriteLine($"  git add {RevocationFile} && git commit -m \"revoke key\" && git push");
        return 0;
    }

    private static string RandomBase32(RandomNumberGenerator rng, int length)
    {
        var bytes = new byte[length];
        rng.GetBytes(bytes);
        var sb = new System.Text.StringBuilder(length);
        foreach (var b in bytes)
        {
            sb.Append(Base32Alphabet[b % Base32Alphabet.Length]);
        }
        return sb.ToString();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --project Tools/KeyGenerator -- [options]");
        Console.WriteLine();
        Console.WriteLine("Generating keys:");
        Console.WriteLine("  --tier <free|trial|paid>   Tier marker (default: free)");
        Console.WriteLine("  --count <N>                 Number of keys to generate (default: 1)");
        Console.WriteLine("  --year <YYYY>               Override the issue year (default: current UTC)");
        Console.WriteLine("  --month <M>                 Override the issue month (default: current UTC)");
        Console.WriteLine();
        Console.WriteLine("Revoking keys (cuts off access on the user's next launch):");
        Console.WriteLine("  --revoke <KEY>              Add a key to the revocation list (repeatable)");
        Console.WriteLine("  --restore <KEY>             Remove a key from the list again (repeatable)");
        Console.WriteLine("  --list-revoked              Show how many keys are currently revoked");
        Console.WriteLine();
        Console.WriteLine("  --help, -h                  Show this message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  KeyGenerator --tier paid --count 4");
        Console.WriteLine("  KeyGenerator --revoke AACO-R-P-IYNIVVT6-DMRFQIQU");
        Console.WriteLine();
        Console.WriteLine("Revocation only takes effect once license-revocations.json is");
        Console.WriteLine("committed and pushed - the app reads it from the public repo.");
    }
}
