using System.Collections.Generic;

namespace VibranceHud.Games
{
    /// <summary>
    /// A game the hub knows how to optimize, identified by its Steam app id, or - for
    /// Epic-only titles - its Epic Games launcher <see cref="EpicAppName"/> instead.
    /// </summary>
    public sealed record SupportedGame(string Id, string DisplayName, int SteamAppId, string InstallFolder, string? EpicAppName = null);

    /// <summary>A supported game found installed on this PC, with its resolved folder.</summary>
    public sealed record DetectedGame(SupportedGame Game, string InstallDir);

    /// <summary>The catalog of games the hub supports.</summary>
    public static class SupportedGames
    {
        public static readonly SupportedGame Rust = new("rust", "Rust", 252490, "Rust");
        public static readonly SupportedGame Cs2 =
            new("cs2", "Counter-Strike 2", 730, "Counter-Strike Global Offensive");
        public static readonly SupportedGame Apex =
            new("apex", "Apex Legends", 1172470, "Apex Legends");
        public static readonly SupportedGame Fortnite =
            new("fortnite", "Fortnite", 0, "Fortnite", EpicAppName: "Fortnite");

        public static readonly IReadOnlyList<SupportedGame> All = new[] { Rust, Cs2, Apex, Fortnite };
    }
}
