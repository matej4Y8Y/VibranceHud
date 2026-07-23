using System.Collections.Generic;

namespace VibranceHud.Games
{
    /// <summary>A game the hub knows how to optimize, identified by its Steam app id.</summary>
    public sealed record SupportedGame(string Id, string DisplayName, int SteamAppId, string InstallFolder);

    /// <summary>A supported game found installed on this PC, with its resolved folder.</summary>
    public sealed record DetectedGame(SupportedGame Game, string InstallDir);

    /// <summary>The catalog of games the hub supports. v1: Rust only.</summary>
    public static class SupportedGames
    {
        public static readonly SupportedGame Rust = new("rust", "Rust", 252490, "Rust");

        public static readonly IReadOnlyList<SupportedGame> All = new[] { Rust };
    }
}
