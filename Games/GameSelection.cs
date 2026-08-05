using System;
using System.Collections.Generic;
using System.Linq;

namespace VibranceHud.Games
{
    /// <summary>
    /// Which game the app is currently pointed at.
    ///
    /// One selection, app-wide, remembered between runs. The Game tab, the Profile Editor and
    /// (later) the resolution and keybind pages all read it, so the question "which game am I
    /// configuring?" is answered once in the shell instead of by a dropdown inside whichever
    /// page happened to need it.
    ///
    /// Deliberately a UI concept only. It does NOT decide what auto-apply does - that still
    /// keys off whichever game actually launches, through GameProcessWatcher. Someone can be
    /// editing Rust in the app while CS2 is running and CS2's profile still applies. Wiring
    /// auto-apply to this would mean picking a game from a menu changed the colours on the
    /// user's screen, which is not what a menu should do.
    /// </summary>
    public sealed class GameSelection
    {
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly Func<IReadOnlyList<DetectedGame>> _detect;

        private List<DetectedGame> _installed = new();
        private string? _currentId;

        /// <summary>Raised when the selection changes. Not raised for a re-select of the same
        /// game - pages rebuild themselves on this, and rebuilding to show what is already
        /// there is a visible flicker for no reason.</summary>
        public event EventHandler? Changed;

        public GameSelection(AppSettings settings, SettingsStore store,
            Func<IReadOnlyList<DetectedGame>>? detect = null)
        {
            _settings = settings;
            _store = store;
            _detect = detect ?? GameLibrary.DetectInstalled;
            Refresh();
            _currentId = Resolve(_settings.CurrentGameId);
        }

        /// <summary>Games found on this PC, in catalogue order.</summary>
        public IReadOnlyList<DetectedGame> Installed => _installed;

        /// <summary>Null means Desktop - no game selected.</summary>
        public string? CurrentId => _currentId;

        /// <summary>The selected game, or null at Desktop.</summary>
        public SupportedGame? Current =>
            _currentId == null ? null : SupportedGames.All.FirstOrDefault(g => g.Id == _currentId);

        /// <summary>The selected game's install, or null at Desktop / not installed.</summary>
        public DetectedGame? Detected =>
            _currentId == null ? null : _installed.FirstOrDefault(d => d.Game.Id == _currentId);

        /// <summary>True when there is nothing to point at - no supported game is installed.</summary>
        public bool NothingInstalled => _installed.Count == 0;

        /// <summary>Re-run detection. Called when the Game tab is opened, so a game installed
        /// or removed while the app was running corrects itself the next time the user looks
        /// rather than needing a restart.</summary>
        public void Refresh()
        {
            try { _installed = _detect().ToList(); }
            catch { _installed = new List<DetectedGame>(); }

            // A selection that is no longer installed silently becomes Desktop rather than
            // leaving pages pointed at a game that isn't there.
            if (_currentId != null && Resolve(_currentId) == null)
            {
                _currentId = null;
                Persist();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Point the app at a game, or at Desktop with null.</summary>
        public void Select(string? gameId)
        {
            var resolved = Resolve(gameId);
            if (resolved == _currentId) return;

            _currentId = resolved;
            Persist();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Turn a stored id into one we can actually honour, or null for Desktop.
        ///
        /// Covers three ways a saved id goes bad: empty (never chosen), not in the catalogue
        /// (a downgrade or a hand-edited settings file), and in the catalogue but not
        /// installed any more. All three land on Desktop, which is the one state that is
        /// always valid.
        /// </summary>
        private string? Resolve(string? gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return null;
            if (!SupportedGames.All.Any(g => g.Id == gameId)) return null;
            if (!_installed.Any(d => d.Game.Id == gameId)) return null;
            return gameId;
        }

        private void Persist()
        {
            _settings.CurrentGameId = _currentId ?? "";
            _store.Save(_settings);
        }
    }
}
