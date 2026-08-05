using System;

namespace VibranceHud
{
    /// <summary>
    /// Wires <see cref="GameProcessWatcher"/> events to <see cref="ProfileApplyEngine"/>
    /// calls. On <see cref="GameProcessWatcher.OnGameLaunched"/>, looks the profile
    /// up in <see cref="GameProfileStore"/> (no-op when the user hasn't saved one) and
    /// applies it. On <see cref="GameProcessWatcher.OnGameClosed"/>, restores the
    /// desktop state captured at apply time.
    ///
    /// Not a singleton: constructed once by <c>TrayApplicationContext</c> and owned
    /// for the lifetime of the tray process. The <see cref="GameProfileApplyGate"/> is
    /// the per-game opt-out seam for future toggles — today it just approves
    /// everything, but the shape is right for adding a "don't auto-apply Rust" toggle
    /// without touching the coordinator.
    /// </summary>
    public sealed class ProfileEngineCoordinator
    {
        private readonly GameProcessWatcher _watcher;
        private readonly ProfileApplyEngine _engine;
        private readonly GameProfileApplyGate _gate;
        private readonly AppSettings? _settings;

        /// <summary>Convenience passthrough to <see cref="GameProcessWatcher.IsRunning"/>;
        /// used by the editor card's status dot and the tray icon's state-aware text.</summary>
        public bool IsRunning => _watcher.IsRunning;

        /// <param name="settings">Needed for the per-game resolution rules. Optional so the
        /// existing tests, which are about profiles, keep constructing this without one.</param>
        public ProfileEngineCoordinator(
            GameProcessWatcher watcher,
            ProfileApplyEngine engine,
            GameProfileApplyGate gate,
            AppSettings? settings = null)
        {
            _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _settings = settings;

            _watcher.OnGameLaunched += OnLaunched;
            _watcher.OnGameClosed += OnClosed;
        }

        /// <summary>Begin polling.</summary>
        public void Start() => _watcher.Start();

        /// <summary>Stop polling.</summary>
        public void Stop() => _watcher.Stop();

        private void OnLaunched(string gameId)
        {
            // Resolution first, and NOT behind the profile gate.
            //
            // These are two independent features that happen to share a trigger. Somebody who
            // has never opened the Profile Editor - which is most people - still expects their
            // launch resolution to work, and it used to be skipped entirely because the
            // profile lookup above returned null and bailed out before reaching it.
            ApplyMonitorRule(gameId);

            // Gate first: per-game opt-out (always approved today, but the seam exists).
            if (!_gate.ShouldAutoApply(gameId)) return;

            var profile = GameProfileStore.Get(gameId);
            if (profile == null) return; // user hasn't saved a profile for this game — silent no-op

            _engine.SetCurrent(profile);
            _engine.ApplyAsync(gameId).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Switch the desktop for a game that has a resolution rule, and arrange to switch it
        /// back when the game exits.
        ///
        /// Best-effort throughout: a monitor that refuses the mode, or a game whose process
        /// never appears, must leave the user exactly where they were rather than stranded on
        /// a resolution they did not choose.
        /// </summary>
        private void ApplyMonitorRule(string gameId)
        {
            if (_settings == null) return;

            var rule = MonitorRules.For(_settings.MonitorRules, gameId);
            if (rule == null) return;

            if (DisplayController.Current() is not { } original) return;
            if (original.Width == rule.Width && original.Height == rule.Height) return;

            if (!DisplayController.Apply(rule.Width, rule.Height)) return;

            var game = Games.SupportedGames.ById(gameId);
            if (game != null)
                DisplayController.RestoreWhenGameExits(game.ProcessName, original,
                    TimeSpan.FromMinutes(5));
        }

        private void OnClosed(string gameId)
        {
            // Restore is the same regardless of which game just left; the engine holds
            // the snapshot. If multiple games were nested last-write-wins-style, this
            // restores to whatever the previous in-flight game had set (documented in
            // the spec as the "one auto-managed game at a time" model).
            _engine.RestoreAsync().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Decision hook: should auto-apply kick in for <paramref name="gameId"/>?
    /// Default implementation is "yes, always" — the coordinator never invents a
    /// no-answer today. Future per-game opt-out UI (a toggle in the editor card) plugs
    /// in here by replacing the implementation; the coordinator never changes.
    ///
    /// Post alt-tab fix: takes an <see cref="AppSettings"/> so it can honour
    /// <see cref="AppSettings.ManualOverrideActive"/> - if the user tweaked values
    /// from the popup after a profile was applied, the next launch of the same game
    /// skips the auto-apply until they opt back in (the picker can clear the flag,
    /// or it auto-clears on PlexusX shutdown so it never persists across reboots).
    /// </summary>
    public sealed class GameProfileApplyGate
    {
        private readonly AppSettings _settings;

        public GameProfileApplyGate(AppSettings settings)
        {
            _settings = settings;
        }

        public bool ShouldAutoApply(string gameId)
        {
            if (_settings.ManualOverrideActive) return false;   // user's last popup tweak wins
            return true;
        }
    }
}
