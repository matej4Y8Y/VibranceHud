using System;

namespace VibranceHud
{
    /// <summary>Small facts about the running build, shown in the UI so it feels like a
    /// real, versioned product.</summary>
    public static class AppInfo
    {
        public const string ProductName = "PlexusX";
        public const string Tagline = "Sharper colors. Smoother games.";
        public const string DiscordUrl = "https://discord.gg/Gha6kYq4e";

        /// <summary>The running version as "v0.2.3" (major.minor.build).</summary>
        public static string VersionText => FormatVersion(UpdateService.CurrentVersion);

        /// <summary>Format a version as "v{major}.{minor}.{build}".</summary>
        public static string FormatVersion(Version v) => $"v{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";
    }
}
