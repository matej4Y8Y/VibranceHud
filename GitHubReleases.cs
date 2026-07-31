using System;
using System.Text.Json;

namespace VibranceHud
{
    /// <summary>A published release on GitHub, as far as the updater cares.</summary>
    /// <param name="Notes">The release body ("what's new"), shown after updating.</param>
    public sealed record ReleaseInfo(
        Version Version,
        string Tag,
        string InstallerUrl,
        string PageUrl,
        string Notes,
        string? Sha256 = null,
        string? MirrorUrl = null,
        string? RawMirrorUrl = null);

    /// <summary>
    /// Parses GitHub's "latest release" JSON and decides whether it's newer than what's
    /// running. Pure - no network - so the version logic is unit-tested.
    /// </summary>
    public static class GitHubReleases
    {
        /// <summary>Reads tag_name + the installer asset. Returns null if unusable.</summary>
        public static ReleaseInfo? ParseLatest(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("tag_name", out var tagEl)) return null;
                var tag = tagEl.GetString() ?? "";
                var version = ParseVersion(tag);
                if (version == null) return null;

                var page = root.TryGetProperty("html_url", out var p) ? p.GetString() ?? "" : "";
                var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

                if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                    return null;

                // The installer is the .exe asset - and when a release carries more than one,
                // the NEWEST wins.
                //
                // This used to take the first asset whose name contained "setup" and stop. A
                // release on the live repo had both PlexusX-Setup-0.9.6.exe and
                // PlexusX-Setup-0.9.8.exe attached, with 0.9.6 listed first - so every user
                // would have been handed 0.9.6 while the 0.9.8 they were meant to get sat
                // there unused, and nothing would have reported a problem. GitHub makes no
                // promise about asset order, so "first" was never a safe choice.
                string? url = null;
                string installerName = "";
                Version? bestVersion = null;

                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    var link = asset.TryGetProperty("browser_download_url", out var d) ? d.GetString() : null;
                    if (link == null) continue;

                    var assetVersion = ParseVersionFromFilename(name);

                    // First candidate wins by default; after that only a higher version
                    // displaces it. An asset with no readable version never displaces one that
                    // has a version, since there's no way to know it's newer.
                    bool better = url == null
                        || (assetVersion != null && (bestVersion == null || assetVersion > bestVersion));

                    if (!better) continue;

                    url = link;
                    installerName = name;
                    bestVersion = assetVersion;
                }
                if (url == null) return null;

                // Guard against a mistagged release: if the installer file is a newer version
                // than the tag (e.g. Setup-0.2.2.exe uploaded into the v0.2.1 release), trust
                // the file so users on the old version still get the update instead of stalling.
                var fileVersion = ParseVersionFromFilename(installerName);
                if (fileVersion != null && fileVersion > version) version = fileVersion;

                return new ReleaseInfo(version, tag, url, page, notes);
            }
            catch
            {
                return null; // malformed payload - treat as "no update available"
            }
        }

        /// <summary>Turns "v0.3" / "0.3.0" into a comparable 3-part version.</summary>
        public static Version? ParseVersion(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var text = tag.Trim().TrimStart('v', 'V');
            if (!Version.TryParse(text, out var parsed)) return null;
            return Normalize(parsed);
        }

        /// <summary>Pull an "x.y[.z]" version out of an installer filename like
        /// "PlexusX-Setup-0.2.2.exe". Null when there's no version-looking part.</summary>
        public static Version? ParseVersionFromFilename(string filename)
        {
            // Take the LAST version-looking token so a product number in the name
            // (e.g. "PlexusX2-Setup-0.2.2.exe") doesn't get mistaken for the version.
            var matches = System.Text.RegularExpressions.Regex.Matches(filename, @"\d+\.\d+(?:\.\d+)?");
            return matches.Count > 0 ? ParseVersion(matches[^1].Value) : null;
        }

        public static bool IsNewer(Version latest, Version current) =>
            Normalize(latest) > Normalize(current);

        /// <summary>Collapse to major.minor.build so 0.2 and 0.2.0.0 compare equal.</summary>
        private static Version Normalize(Version v) =>
            new(v.Major, v.Minor, Math.Max(v.Build, 0));

        /// <summary>Public version of Normalize for callers that need a comparable Version
        /// (UpdateService.ReadInstallerVersion, etc.). -1 build means "no build component".</summary>
        public static Version NormalizeForCompare(Version v) => Normalize(v);
    }
}
