using System.Threading.Tasks;

namespace VibranceHud
{
    /// <summary>
    /// Snapshot-then-apply logic for auto-managed game profiles. Holds the
    /// current profile and the pre-apply desktop snapshot so that
    /// <see cref="RestoreAsync"/> returns the user to their desktop settings
    /// after the game closes.
    ///
    /// All visual writes go through the existing <see cref="IVibranceEngine"/>
    /// setters, so the DX11 overlay / NVAPI / gamma-ramp paths in
    /// <see cref="VibranceEngine"/> keep owning how a value reaches the GPU.
    ///
    /// Colour only. This used to also push settings into the game's own config file through
    /// an IGameHubApplier; that went with the Games Hub, because writing to a game's files is
    /// under the hood and PlexusX only changes what the monitor shows.
    /// </summary>
    public sealed class ProfileApplyEngine
    {
        private readonly IVibranceEngine _engine;
        private GameProfile? _current;
        private GameProfile? _before;

        /// <summary>The profile currently applied (or null if none).</summary>
        public GameProfile? Current => _current;

        public ProfileApplyEngine(IVibranceEngine engine) => _engine = engine;

        /// <summary>The coordinator hands the engine the profile it found in the
        /// store before calling <see cref="ApplyAsync"/>.</summary>
        public void SetCurrent(GameProfile profile) => _current = profile;

        /// <summary>Snapshots the current desktop state, then applies the profile's colour.
        /// No-op if no profile is set or the id doesn't match (defensive: the coordinator
        /// already filtered).</summary>
        public Task ApplyAsync(string gameId)
        {
            if (_current == null || _current.GameId != gameId) return Task.CompletedTask;

            // 1. Snapshot desktop state so RestoreAsync can put it back.
            _before = new GameProfile
            {
                GameId = "snapshot",
                Vibrance = _engine.Vibrance,
                Saturation = _engine.Saturation,
                Brightness = _engine.Brightness,
                Gamma = _engine.Gamma,
            };

            // 2. Apply the profile's visual sliders through the existing setters.
            _engine.Vibrance = _current.Vibrance;
            _engine.Saturation = _current.Saturation;
            _engine.Brightness = _current.Brightness;
            _engine.Gamma = _current.Gamma;

            return Task.CompletedTask;
        }

        /// <summary>Puts the desktop state back. Safe to call when nothing is
        /// applied (no-op).</summary>
        public Task RestoreAsync()
        {
            if (_before == null) return Task.CompletedTask;
            _engine.Vibrance = _before.Vibrance;
            _engine.Saturation = _before.Saturation;
            _engine.Brightness = _before.Brightness;
            _engine.Gamma = _before.Gamma;
            _before = null;
            _current = null;
            return Task.CompletedTask;
        }
    }
}