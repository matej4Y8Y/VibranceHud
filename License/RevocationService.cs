// Fetches and caches the revocation list. Mirrors UpdateService's shape on purpose:
// pure parsing logic lives in RevocationList (fully unit-tested), this class is the
// thin network/disk shell around it (not heavily unit-tested, same split UpdateService
// draws with GitHubReleases).
//
// No account, no server, no token: the list is a plain JSON file read from the public
// GitHub repo via raw.githubusercontent.com - the same free-hosting trick the
// auto-updater already leans on for release metadata. Revocation is deliberately
// best-effort: a machine with no internet (or GitHub down, or rate-limited) keeps
// using its last-known-good cache instead of getting locked out. A user who is offline
// right when the developer revokes their key keeps working until the next successful
// check - that's the accepted tradeoff for not running a license server.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace VibranceHud.License
{
    public static class RevocationService
    {
        private const string RevocationUrl =
            "https://raw.githubusercontent.com/matej4Y8Y/VibranceHud/main/license-revocations.json";

        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlexusX");

        private static readonly string CachePath = Path.Combine(CacheDir, "revocations-cache.json");

        /// <summary>Whatever's on disk from the last successful fetch. Never touches
        /// the network - safe to call synchronously from LicenseService.Load().</summary>
        public static System.Collections.Generic.IReadOnlySet<string> LoadCached()
        {
            try
            {
                if (!File.Exists(CachePath)) return new System.Collections.Generic.HashSet<string>();
                return RevocationList.Parse(File.ReadAllText(CachePath));
            }
            catch
            {
                return new System.Collections.Generic.HashSet<string>();
            }
        }

        /// <summary>Fetches the latest list and overwrites the cache. Any failure
        /// (offline, 404, malformed JSON, rate-limited) leaves the existing cache
        /// untouched and returns false - callers should treat that as "nothing new to
        /// report", not an error to surface to the user.</summary>
        public static async Task<bool> RefreshAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PlexusX-Updater");
                var json = await client.GetStringAsync(RevocationUrl);

                // Validate before trusting it - RevocationList.Parse never throws, so
                // check the JSON is at least well-formed before we let it overwrite a
                // possibly-good cache with a possibly-empty one.
                System.Text.Json.JsonDocument.Parse(json).Dispose();

                Directory.CreateDirectory(CacheDir);
                await File.WriteAllTextAsync(CachePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
