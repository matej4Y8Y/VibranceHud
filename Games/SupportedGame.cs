using System;
using System.Collections.Generic;
using System.Linq;

namespace VibranceHud.Games
{
    /// <summary>
    /// A game the hub knows how to optimize, identified by its Steam app id, or - for
    /// Epic-only titles - its Epic Games launcher <see cref="EpicAppName"/> instead.
    /// </summary>
    /// <param name="ProcessName">The running executable, without ".exe". Lives here rather
    /// than being repeated in each service because six different files had their own copy of
    /// these strings - the watcher, the resolution restore, and one "is it running?" check per
    /// game - and a game that renames its exe would have had to be fixed in all of them.</param>
    public sealed record SupportedGame(
        string Id, string DisplayName, int SteamAppId, string InstallFolder,
        string ProcessName, string? EpicAppName = null);

    /// <summary>A supported game found installed on this PC, with its resolved folder.</summary>
    public sealed record DetectedGame(SupportedGame Game, string InstallDir);

    /// <summary>The catalog of games the hub supports.</summary>
    public static class SupportedGames
    {
        public static readonly SupportedGame Rust =
            new("rust", "Rust", 252490, "Rust", "RustClient");
        public static readonly SupportedGame Cs2 =
            new("cs2", "Counter-Strike 2", 730, "Counter-Strike Global Offensive", "cs2");
        public static readonly SupportedGame Apex =
            new("apex", "Apex Legends", 1172470, "Apex Legends", "r5apex");
        public static readonly SupportedGame Fortnite =
            new("fortnite", "Fortnite", 0, "Fortnite", "FortniteClient-Win64-Shipping",
                EpicAppName: "Fortnite");

        public static readonly IReadOnlyList<SupportedGame> All = new[] { Rust, Cs2, Apex, Fortnite };

        /// <summary>gameId → process name, for the launch watcher.</summary>
        public static IReadOnlyDictionary<string, string> ProcessNames =>
            All.ToDictionary(g => g.Id, g => g.ProcessName, StringComparer.OrdinalIgnoreCase);

        public static SupportedGame? ById(string? id) =>
            id == null ? null : All.FirstOrDefault(g => g.Id == id);
    }
}
