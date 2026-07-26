namespace VibranceHud
{
    /// <summary>
    /// The narrow slice of the Games-Hub config-write path that
    /// <see cref="ProfileApplyEngine"/> needs. The real implementation wraps the
    /// existing per-game <c>SettingsService</c>s (Rust, CS2, Apex, Fortnite); tests
    /// substitute a fake.
    /// </summary>
    public interface IGameHubApplier
    {
        /// <summary>Writes the supplied hub options into the game's own config.
        /// Called on game launch. Implementations are responsible for the same
        /// exception handling the Games-Hub UI already does (e.g. "config busy"
        /// toast when the cfg file is locked).</summary>
        void Apply(string gameId, GameHubOptions options);
    }
}