using System;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class GitHubReleasesTests
    {
        private const string Json = @"{
            ""tag_name"": ""v0.3.0"",
            ""html_url"": ""https://github.com/matej4Y8Y/VibranceHud/releases/tag/v0.3.0"",
            ""body"": ""- Added gamma slider\n- Faster startup"",
            ""assets"": [
                { ""name"": ""notes.txt"", ""browser_download_url"": ""https://example.com/notes.txt"" },
                { ""name"": ""PlexusX-Setup-0.3.0.exe"", ""browser_download_url"": ""https://example.com/PlexusX-Setup-0.3.0.exe"" }
            ]
        }";

        [Fact]
        public void ParseLatest_ReadsVersionTagAndInstallerAsset()
        {
            var release = GitHubReleases.ParseLatest(Json);

            Assert.NotNull(release);
            Assert.Equal(new Version(0, 3, 0), release!.Version);
            Assert.Equal("v0.3.0", release.Tag);
            Assert.Equal("https://example.com/PlexusX-Setup-0.3.0.exe", release.InstallerUrl);
        }

        [Fact]
        public void ParseLatest_ReadsReleaseNotes()
        {
            var release = GitHubReleases.ParseLatest(Json);

            Assert.Contains("gamma slider", release!.Notes);
            Assert.Contains("Faster startup", release.Notes);
        }

        [Fact]
        public void ParseLatest_ToleratesAMissingBody()
        {
            var json = @"{ ""tag_name"": ""v1.0.0"", ""assets"": [
                { ""name"": ""PlexusX-Setup-1.0.0.exe"", ""browser_download_url"": ""https://example.com/s.exe"" } ] }";

            var release = GitHubReleases.ParseLatest(json);

            Assert.NotNull(release);
            Assert.Equal("", release!.Notes);
        }

        [Fact]
        public void ParseLatest_IgnoresNonInstallerAssets()
        {
            var release = GitHubReleases.ParseLatest(Json);
            Assert.EndsWith(".exe", release!.InstallerUrl);
        }

        [Fact]
        public void ParseLatest_ReturnsNull_WhenThereIsNoInstaller()
        {
            var json = @"{ ""tag_name"": ""v1.0.0"", ""assets"": [
                { ""name"": ""readme.md"", ""browser_download_url"": ""https://example.com/readme.md"" } ] }";

            Assert.Null(GitHubReleases.ParseLatest(json));
        }

        [Fact]
        public void ParseLatest_ReturnsNull_ForGarbage()
        {
            Assert.Null(GitHubReleases.ParseLatest("not json at all"));
            Assert.Null(GitHubReleases.ParseLatest("{}"));
        }

        [Theory]
        [InlineData("v0.3.0", 0, 3, 0)]
        [InlineData("0.3.0", 0, 3, 0)]
        [InlineData("V1.2.3", 1, 2, 3)]
        [InlineData("v0.3", 0, 3, 0)]     // two-part tags normalise to x.y.0
        public void ParseVersion_HandlesCommonTagShapes(string tag, int major, int minor, int build)
        {
            Assert.Equal(new Version(major, minor, build), GitHubReleases.ParseVersion(tag));
        }

        [Fact]
        public void ParseVersion_ReturnsNull_ForNonsense()
        {
            Assert.Null(GitHubReleases.ParseVersion("banana"));
            Assert.Null(GitHubReleases.ParseVersion(""));
        }

        [Fact]
        public void IsNewer_OnlyWhenStrictlyGreater()
        {
            Assert.True(GitHubReleases.IsNewer(new Version(0, 3, 0), new Version(0, 2, 0)));
            Assert.True(GitHubReleases.IsNewer(new Version(1, 0, 0), new Version(0, 9, 9)));

            Assert.False(GitHubReleases.IsNewer(new Version(0, 2, 0), new Version(0, 2, 0)));
            Assert.False(GitHubReleases.IsNewer(new Version(0, 1, 0), new Version(0, 2, 0)));
        }

        [Fact]
        public void IsNewer_IgnoresTheFourthAssemblyComponent()
        {
            // Assembly versions come through as 0.2.0.0 - that must not read as newer.
            Assert.False(GitHubReleases.IsNewer(new Version(0, 2, 0), new Version("0.2.0.0")));
        }
    }
}
