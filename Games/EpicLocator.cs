using System;
using System.IO;
using System.Text.Json;

namespace VibranceHud.Games
{
    /// <summary>
    /// Finds Epic Games Launcher installs by reading its manifest (.item) files - JSON blobs
    /// dropped in %ProgramData%\Epic\EpicGamesLauncher\Data\Manifests, one per installed app.
    /// The parse is pure and unit-tested directly; the filesystem walk around it degrades to
    /// null like SteamLocator does when Steam is absent.
    /// </summary>
    public static class EpicLocator
    {
        /// <summary>
        /// Parses a manifest .item JSON blob and returns its InstallLocation if AppName matches
        /// (case-insensitive), or null on a non-match, malformed JSON, or missing field. Never throws.
        /// </summary>
        public static string? ParseInstallLocation(string manifestJson, string appName)
        {
            try
            {
                using var doc = JsonDocument.Parse(manifestJson);
                if (!doc.RootElement.TryGetProperty("AppName", out var appNameProp)) return null;
                if (!string.Equals(appNameProp.GetString(), appName, StringComparison.OrdinalIgnoreCase)) return null;

                return doc.RootElement.TryGetProperty("InstallLocation", out var locProp)
                    ? locProp.GetString()
                    : null;
            }
            catch
            {
                // Never throws: valid-but-wrong-shape JSON (root array, non-string fields)
                // throws InvalidOperationException from TryGetProperty/GetString, not JsonException.
                return null;
            }
        }

        /// <summary>Scans the Epic manifests folder for an app, returning its install dir or null.</summary>
        public static string? FindGameInstall(string appName)
        {
            try
            {
                var manifestsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Epic", "EpicGamesLauncher", "Data", "Manifests");
                if (!Directory.Exists(manifestsDir)) return null;

                foreach (var file in Directory.EnumerateFiles(manifestsDir, "*.item"))
                {
                    var loc = ParseInstallLocation(File.ReadAllText(file), appName);
                    if (loc != null && Directory.Exists(loc)) return loc;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
