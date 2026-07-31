// Which installer the updater downloads when a release carries more than one.
//
// Found on the live repo: a release tagged 0.9.5 had BOTH PlexusX-Setup-0.9.6.exe and
// PlexusX-Setup-0.9.8.exe attached. The picker took the first asset whose name contained
// "setup" and stopped, so every user would have been handed 0.9.6 while 0.9.8 sat there
// unused - an older build than the one that was actually published, with no error anywhere.
//
// GitHub does not guarantee asset order, so "first" is never the right answer. Pick the
// highest version.

using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class ReleaseAssetPickingTests
    {
        private static string Release(string tag, params string[] assetNames)
        {
            var assets = string.Join(",", System.Array.ConvertAll(assetNames, n =>
                $$"""{"name":"{{n}}","browser_download_url":"https://example.test/{{n}}"}"""));
            return $$"""
            {"tag_name":"{{tag}}","html_url":"https://example.test/r","body":"notes","assets":[{{assets}}]}
            """;
        }

        /// <summary>The exact live situation.</summary>
        [Fact]
        public void TwoInstallers_PicksTheNewer_NotTheFirstListed()
        {
            var release = GitHubReleases.ParseLatest(
                Release("0.9.5", "PlexusX-Setup-0.9.6.exe", "PlexusX-Setup-0.9.8.exe"));

            Assert.NotNull(release);
            Assert.Contains("0.9.8", release!.InstallerUrl);
            Assert.Equal(new System.Version(0, 9, 8), release.Version);
        }

        /// <summary>Order must not matter - GitHub doesn't promise one.</summary>
        [Fact]
        public void NewerListedFirst_StillPicksTheNewer()
        {
            var release = GitHubReleases.ParseLatest(
                Release("0.9.5", "PlexusX-Setup-0.9.8.exe", "PlexusX-Setup-0.9.6.exe"));

            Assert.Contains("0.9.8", release!.InstallerUrl);
        }

        [Fact]
        public void ThreeInstallers_PicksTheHighest()
        {
            var release = GitHubReleases.ParseLatest(
                Release("0.9.0",
                    "PlexusX-Setup-0.9.4.exe",
                    "PlexusX-Setup-0.9.8.exe",
                    "PlexusX-Setup-0.9.6.exe"));

            Assert.Contains("0.9.8", release!.InstallerUrl);
            Assert.Equal(new System.Version(0, 9, 8), release.Version);
        }

        /// <summary>A single installer keeps working exactly as before.</summary>
        [Fact]
        public void SingleInstaller_IsStillChosen()
        {
            var release = GitHubReleases.ParseLatest(
                Release("v0.9.8", "PlexusX-Setup-0.9.8.exe"));

            Assert.Contains("0.9.8", release!.InstallerUrl);
        }

        /// <summary>Non-installer attachments must never be downloaded as the update.</summary>
        [Fact]
        public void NonInstallerAssets_AreIgnored()
        {
            var release = GitHubReleases.ParseLatest(
                Release("v0.9.8", "release-notes.md", "PlexusX-Setup-0.9.8.exe", "checksums.txt"));

            Assert.Contains("PlexusX-Setup-0.9.8.exe", release!.InstallerUrl);
        }

        /// <summary>A mistagged release must resolve to what's actually in the file - the tag
        /// is a label a human types, the filename is the build.</summary>
        [Fact]
        public void MistaggedRelease_ReportsTheInstallersVersion()
        {
            var release = GitHubReleases.ParseLatest(
                Release("0.9.5", "PlexusX-Setup-0.9.8.exe"));

            Assert.Equal(new System.Version(0, 9, 8), release!.Version);
        }

        /// <summary>An installer whose name carries no version can't be compared, so it must
        /// not win over one that can.</summary>
        [Fact]
        public void UnversionedInstaller_DoesNotBeatAVersionedOne()
        {
            var release = GitHubReleases.ParseLatest(
                Release("v0.9.8", "PlexusX-Setup.exe", "PlexusX-Setup-0.9.8.exe"));

            Assert.Contains("PlexusX-Setup-0.9.8.exe", release!.InstallerUrl);
        }
    }
}
