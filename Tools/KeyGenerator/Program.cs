// Activation key generator. Run from a terminal:
//
//   dotnet run --project Tools/KeyGenerator -- --tier free --count 4
//
// Outputs one key per line, ready to copy-paste. Keys are deterministic in
// structure (year-month + release + tier + body + checksum) but the body is
// random so the developer can hand them out without a central registry.

using System;
using System.Security.Cryptography;
using VibranceHud.License;

internal static class Program
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static int Main(string[] args)
    {
        string tier = "free";
        int count = 1;
        int? year = null;
        int? month = null;
        bool overrideYearMonth = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
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
        Console.WriteLine("Options:");
        Console.WriteLine("  --tier <free|trial|paid>   Tier marker (default: free)");
        Console.WriteLine("  --count <N>                 Number of keys to generate (default: 1)");
        Console.WriteLine("  --year <YYYY>               Override the issue year (default: current UTC)");
        Console.WriteLine("  --month <M>                 Override the issue month (default: current UTC)");
        Console.WriteLine("  --help, -h                  Show this message");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  KeyGenerator --tier free --count 4");
    }
}
