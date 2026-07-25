using System;
using System.Threading;
using System.Threading.Tasks;

namespace VibranceHud.Audio
{
    /// <summary>
    /// Audio Edge: runs the peak limiter against the real speakers. While it's on, anything
    /// louder than the ceiling gets pulled down, so a gun shot can't punch above the level
    /// footsteps sit at once you turn the game up.
    ///
    /// It remembers the volume you had before it started and always puts it back when it
    /// stops - we never leave someone's PC quietly turned down.
    /// </summary>
    public sealed class AudioEdgeService : IDisposable
    {
        /// <summary>How often we sample the output. ~30ms is responsive without burning CPU.</summary>
        public const int TickMs = 30;

        private readonly IAudioOutput _output;
        private CancellationTokenSource? _cts;
        private float _restoreVolume = 1f;

        public AudioEdgeService(IAudioOutput output) => _output = output;

        /// <summary>The ceiling, 0-1 (the slider). Live-adjustable while running.</summary>
        public float Threshold { get; set; } = 0.30f;

        public bool IsRunning => _cts != null;

        /// <summary>The volume we'll hand back when switched off (also the limiter's ceiling).</summary>
        public float RestoreVolume => _restoreVolume;

        public void Start()
        {
            if (IsRunning) return;

            _restoreVolume = _output.Volume; // never push above what the user had
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        Tick();
                        await Task.Delay(TickMs, token);
                    }
                }
                catch (OperationCanceledException) { /* normal stop */ }
                catch { /* audio device went away - fall through and restore */ }
                finally
                {
                    _output.Volume = _restoreVolume;
                }
            }, token);
        }

        /// <summary>One step of the limiter. Public so the loop's behaviour is testable.</summary>
        public void Tick()
        {
            float next = AudioLimiter.NextVolume(_output.Peak, Threshold, _output.Volume, _restoreVolume);
            _output.Volume = next;
        }

        public void Stop()
        {
            var cts = _cts;
            _cts = null;
            if (cts == null) return;

            cts.Cancel();
            cts.Dispose();
            _output.Volume = _restoreVolume; // immediate, don't wait for the loop to notice
        }

        public void Dispose()
        {
            Stop();
            (_output as IDisposable)?.Dispose();
        }
    }
}
