namespace VibranceHud.Games
{
    /// <summary>Which per-game optimization page a game id should route to.</summary>
    public enum GamePageKind
    {
        Rust,
        Cs2,
        Apex,
        Fortnite,

        /// <summary>No page written for this id yet - the fail-closed default. Never falls
        /// back to another game's page (Rust's edits <c>client.cfg</c> directly, so routing
        /// an unrecognised id there would write to the wrong game's config).</summary>
        Unsupported,
    }

    /// <summary>
    /// Pure mapping from a <see cref="SupportedGame.Id"/> to the page that knows how to
    /// optimize it. Pulled out of <c>MainWindow.OnConfigureGame</c> so the routing decision
    /// - including the fail-closed default for an id nothing claims - is unit-testable
    /// without constructing the window or any of the real per-game settings pages.
    /// </summary>
    public static class GamePageRouter
    {
        public static GamePageKind Resolve(string gameId) => gameId switch
        {
            "rust" => GamePageKind.Rust,
            "cs2" => GamePageKind.Cs2,
            "apex" => GamePageKind.Apex,
            "fortnite" => GamePageKind.Fortnite,
            _ => GamePageKind.Unsupported,
        };
    }
}
