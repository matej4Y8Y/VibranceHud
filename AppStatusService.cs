using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace VibranceHud
{
    /// <summary>
    /// Fetches and caches the published status file that <see cref="VersionGate"/> decides on.
    ///
    /// Same split as UpdateService/GitHubReleases and RevocationService/RevocationList: every
    /// rule lives in the pure class and is unit-tested; this is only the network and disk
    /// around it. No server to run - it's a static file served over HTTPS.
    ///
    /// The cache is what makes the lockout stick. Once a machine has seen "minimum 1.0.0", that
    /// requirement is written to disk and applied on every later launch, so going offline
    /// afterwards doesn't hand the beta back. And because VersionGate.Resolve keeps the highest
    /// requirement ever seen, serving an older file can't undo it either.
    /// </summary>
    public static class AppStatusService
    {
        private const string StatusUrl =
            "https://raw.githubusercontent.com/matej4Y8Y/VibranceHud/main/app-status.json";

        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlexusX");

        private static readonly string CachePath = Path.Combine(CacheDir, "app-status-cache.json");

        /// <summary>The requirement already known on this machine. Never touches the network,
        /// so it's safe to call before the UI exists.</summary>
        public static Version? CachedMinimum()
        {
            try
            {
                if (!File.Exists(CachePath)) return null;
                return VersionGate.Parse(File.ReadAllText(CachePath)).MinimumVersion;
            }
            catch
            {
                return null;
            }
        }

        public static string CachedMessage()
        {
            try
            {
                if (!File.Exists(CachePath)) return "";
                return VersionGate.Parse(File.ReadAllText(CachePath)).Message;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Fetch the published status and fold it into the cache. Returns the requirement now in
        /// force, which may be the cached one if the fetch failed.
        ///
        /// Deliberately quiet about failure: offline, rate-limited, captive portal and DNS
        /// breakage are all ordinary, and none of them should lock a user out. Only a
        /// successfully read, higher requirement ever changes anything.
        /// </summary>
        public static async Task<Version?> RefreshAsync()
        {
            var cached = CachedMinimum();

            Version? fetched = null;
            string message = "";
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PlexusX-Updater");
                var status = VersionGate.Parse(await client.GetStringAsync(StatusUrl));
                fetched = status.MinimumVersion;
                message = status.Message;
            }
            catch
            {
                // Nothing usable came back - the cache stands.
            }

            var effective = VersionGate.Resolve(fetched, cached);

            // Only write when the requirement actually rose. Rewriting on every launch would
            // let a downgraded or stale file quietly lower the bar.
            if (effective != null && (cached == null || effective > cached))
            {
                try
                {
                    Directory.CreateDirectory(CacheDir);
                    File.WriteAllText(CachePath, VersionGate.Serialize(effective, message));
                }
                catch { /* cache is an optimisation, not a requirement */ }
            }

            return effective;
        }
    }
}
