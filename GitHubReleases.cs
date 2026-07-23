using System;
using System.Text.Json;

namespace VibranceHud
{
    /// <summary>A published release on GitHub, as far as the updater cares.</summary>
    /// <param name="Notes">The release body ("what's new"), shown after updating.</param>
    public sealed record ReleaseInfo(Version Version, string Tag, string InstallerUrl, string PageUrl, string Notes);

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

                // The installer is the .exe asset (prefer one that looks like a Setup).
                string? url = null;
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    var link = asset.TryGetProperty("browser_download_url", out var d) ? d.GetString() : null;
                    if (link == null) continue;

                    url = link;
                    if (name.Contains("setup", StringComparison.OrdinalIgnoreCase)) break;
                }
                if (url == null) return null;

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

        public static bool IsNewer(Version latest, Version current) =>
            Normalize(latest) > Normalize(current);

        /// <summary>Collapse to major.minor.build so 0.2 and 0.2.0.0 compare equal.</summary>
        private static Version Normalize(Version v) =>
            new(v.Major, v.Minor, Math.Max(v.Build, 0));
    }
}
