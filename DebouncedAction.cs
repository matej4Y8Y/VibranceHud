using System;
using System.Threading;

namespace VibranceHud
{
    /// <summary>
    /// Coalesces rapid repeated triggers into a single delayed call - e.g. a slider firing
    /// ValueChanged on every mouse-move during a drag, when only one settings save is wanted
    /// once the user stops moving it. Each <see cref="Trigger"/> resets the delay window, so
    /// the action only runs once the caller has been quiet for the full delay.
    /// </summary>
    public sealed class DebouncedAction : IDisposable
    {
        private readonly Action _action;
        private readonly System.Threading.Timer _timer;

        public DebouncedAction(Action action, int delayMs)
        {
            _action = action;
            // Due-time Infinite: idle until the first Trigger(); Period Infinite so it fires
            // exactly once per window instead of repeating.
            _timer = new System.Threading.Timer(_ => _action(), null, Timeout.Infinite, Timeout.Infinite);
            DelayMs = delayMs;
        }

        public int DelayMs { get; }

        /// <summary>Restart the delay window. The action runs once, DelayMs after the last
        /// call to Trigger - not once per call.</summary>
        public void Trigger() => _timer.Change(DelayMs, Timeout.Infinite);

        public void Dispose() => _timer.Dispose();
    }
}
