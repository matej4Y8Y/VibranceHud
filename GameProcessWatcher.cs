using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VibranceHud
{
    /// <summary>
    /// Polls the OS for the supported games' processes every
    /// <see cref="DefaultPollInterval"/> milliseconds and raises
    /// <see cref="OnGameLaunched"/> / <see cref="OnGameClosed"/> when an EXE appears
    /// or disappears. Goes through the public <see cref="Process.GetProcessesByName(string)"/>
    /// API — a single kernel call per EXE per tick, no P/Invoke, no Steam, no anti-cheat
    /// surface.
    ///
    /// The <c>Process.GetProcessesByName</c> lookup matches on the <em>short</em> name
    /// (the file name without the ".exe"), so the registry-driven process names of the
    /// four supported games are the keys (<c>RustClient</c>, <c>cs2</c>, <c>r5apex</c>,
    /// <c>FortniteClient-Win64-Shipping</c>).
    ///
    /// Lifecycle: <see cref="Start"/> spawns a <c>Task.Run</c> polling loop; <see cref="Stop"/>
    /// signals the cooperative <see cref="CancellationTokenSource"/>. Both are cheap; safe
    /// to call repeatedly.
    /// </summary>
    public sealed class GameProcessWatcher
    {
        public delegate void GameEventHandler(string gameId);

        /// <summary>gameId (matches <see cref="SupportedGame.Id"/>) for the game whose EXE
        /// we just saw in the process list.</summary>
        public event GameEventHandler? OnGameLaunched;

        /// <summary>gameId for the game whose EXE was running last tick and is gone now.</summary>
        public event GameEventHandler? OnGameClosed;

        /// <summary>Default poll cadence (2.5 s). Cheap, but fast enough that the
        /// user-perceived gap between "launched game" and "applied profile" feels
        /// near-instant.</summary>
        public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(2500);

        private readonly IReadOnlyDictionary<string, string> _gameIdToExe;
        private readonly TimeSpan _pollInterval;
        private readonly HashSet<string> _knownRunning = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _cts;

        /// <summary>True between <see cref="Start"/> and <see cref="Stop"/>.</summary>
        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        /// <summary>Process→gameId map. EXE names are matched case-insensitively
        /// by <see cref="Process.GetProcessesByName(string)"/>.</summary>
        /// <param name="gameIdToExe">Map of gameId → process name (no ".exe").</param>
        /// <param name="pollInterval">Polling cadence. <see cref="DefaultPollInterval"/>
        /// when null.</param>
        public GameProcessWatcher(
            IReadOnlyDictionary<string, string> gameIdToExe,
            TimeSpan? pollInterval = null)
        {
            _gameIdToExe = gameIdToExe ?? throw new ArgumentNullException(nameof(gameIdToExe));
            _pollInterval = pollInterval ?? DefaultPollInterval;
        }

        /// <summary>Begin the polling loop. Idempotent — a second call while running is a
        /// no-op rather than starting a second loop.</summary>
        public void Start()
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            // Fire-and-forget. The loop owns _cts.Token and will exit when Stop() cancels.
            _ = Task.Run(() => PollLoop(_cts.Token));
        }

        /// <summary>Signal the loop to exit at the next delay. Idempotent.</summary>
        public void Stop()
        {
            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // already gone
            }
            _cts?.Dispose();
            _cts = null;
        }

        private async Task PollLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var currentRunning = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (gameId, exe) in _gameIdToExe)
                    {
                        try
                        {
                            if (Process.GetProcessesByName(exe).Length > 0)
                                currentRunning.Add(gameId);
                        }
                        catch (InvalidOperationException)
                        {
                            // "Process has exited" between the length check and any access;
                            // treat as not running. Don't kill the loop on a single race.
                        }
                        catch (Exception)
                        {
                            // Best-effort polling. If a single EXE lookup fails
                            // (rare, mostly during shutdown), we still want the
                            // rest of the tick to succeed.
                        }
                    }

                    // Newly launched = seen now but not last tick.
                    foreach (var gameId in currentRunning.Except(_knownRunning))
                        OnGameLaunched?.Invoke(gameId);
                    // Newly closed = seen last tick but not now.
                    foreach (var gameId in _knownRunning.Except(currentRunning))
                        OnGameClosed?.Invoke(gameId);

                    _knownRunning.Clear();
                    foreach (var g in currentRunning) _knownRunning.Add(g);
                }
                catch (Exception)
                {
                    // Top-level catch so a transient blip never kills the watcher.
                    // The last-known set is preserved so we don't re-fire spurious
                    // launch events when the tick recovers.
                }

                try
                {
                    await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
    }
}
