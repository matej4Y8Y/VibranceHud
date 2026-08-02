using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VibranceHud.Monitors
{
    /// <summary>
    /// Everything the app does with monitor settings.
    ///
    /// Three jobs, all of them shaped by DDC/CI being slow:
    ///
    ///   - Scan in the background. A monitor with DDC/CI switched off can take a second to
    ///     not answer, and doing that on startup on the UI thread would look like a hang.
    ///   - Coalesce writes. Dragging a slider produces a value every frame; sending each one
    ///     down the cable would queue up behind a link that manages maybe ten a second, and
    ///     the picture would keep changing after the user let go. Only the newest value for
    ///     each control is ever sent.
    ///   - Remember what was there. Whatever the monitor was set to when we found it gets
    ///     written back on exit, so closing PlexusX doesn't leave someone's screen dimmed.
    /// </summary>
    public sealed class MonitorService : IDisposable
    {
        private readonly IMonitorControl _control;
        private readonly object _gate = new();

        private readonly Dictionary<(string Device, MonitorSetting Setting), int> _pending = new();
        private readonly Dictionary<(string Device, MonitorSetting Setting), int> _original = new();

        private IReadOnlyList<MonitorSnapshot> _monitors = Array.Empty<MonitorSnapshot>();
        private System.Threading.Timer? _flushTimer;
        private bool _disposed;

        /// <summary>Long enough that a slider drag collapses into a handful of writes, short
        /// enough that letting go feels immediate.</summary>
        public static readonly TimeSpan FlushDelay = TimeSpan.FromMilliseconds(120);

        public MonitorService(IMonitorControl control) => _control = control;

        /// <summary>The last scan. Empty until one has finished.</summary>
        public IReadOnlyList<MonitorSnapshot> Monitors
        {
            get { lock (_gate) return _monitors; }
        }

        public bool HasScanned { get; private set; }

        /// <summary>
        /// False when no monitor answered anything.
        ///
        /// The tab uses this to choose its message, and the distinction matters: the usual
        /// cause is DDC/CI switched off in the monitor's own menu, which is one setting away
        /// from working. "Not supported" makes people give up on something that would work.
        /// </summary>
        public bool AnyMonitorResponded => Monitors.Any(m => m.RespondedAtAll);

        /// <summary>Raised on a background thread when a scan finishes.</summary>
        public event Action? Updated;

        public Task ScanAsync() => Task.Run(Scan);

        public void Scan()
        {
            IReadOnlyList<MonitorSnapshot> found;
            try
            {
                found = _control.Scan();
            }
            catch
            {
                // A machine with no DDC/CI support at all. Not an error worth showing - the
                // tab simply has nothing to offer.
                found = Array.Empty<MonitorSnapshot>();
            }

            lock (_gate)
            {
                _monitors = found;

                // Only record an original the first time we see a control. A rescan while the
                // user has been adjusting things must not overwrite what they started with.
                foreach (var monitor in found)
                {
                    foreach (var setting in monitor.Available)
                    {
                        var key = (monitor.DeviceName, setting);
                        if (!_original.ContainsKey(key))
                            _original[key] = monitor.Range(setting)!.Current;
                    }
                }
            }

            HasScanned = true;
            Updated?.Invoke();
        }

        /// <summary>
        /// Queue a slider position. Converts to the monitor's own units and sends the newest
        /// value shortly after the user stops moving.
        /// </summary>
        public bool SetPercent(string deviceName, MonitorSetting setting, int percent)
        {
            var range = Monitors
                .FirstOrDefault(m => m.DeviceName == deviceName)
                ?.Range(setting);
            if (range == null) return false;

            lock (_gate)
            {
                _pending[(deviceName, setting)] = range.RawFromPercent(percent);
                _flushTimer ??= new System.Threading.Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
                _flushTimer.Change(FlushDelay, Timeout.InfiniteTimeSpan);
            }
            return true;
        }

        /// <summary>
        /// Write a value straight through, no percentage conversion and no queueing.
        ///
        /// For the controls that are choices rather than scales - a picture mode, a factory
        /// reset. There is nothing to coalesce: a button is pressed once.
        /// </summary>
        public bool SetRaw(string deviceName, MonitorSetting setting, int rawValue)
        {
            try { return _control.Set(deviceName, setting, rawValue); }
            catch { return false; }
        }

        /// <summary>Send whatever is queued. Called by the timer; called directly by tests.</summary>
        public void Flush()
        {
            KeyValuePair<(string Device, MonitorSetting Setting), int>[] batch;
            lock (_gate)
            {
                if (_pending.Count == 0) return;
                batch = _pending.ToArray();
                _pending.Clear();
            }

            foreach (var item in batch)
            {
                try { _control.Set(item.Key.Device, item.Key.Setting, item.Value); }
                catch { /* a monitor refusing one write must not stop the rest */ }
            }
        }

        /// <summary>
        /// Put every monitor back the way it was found.
        ///
        /// Runs on exit. Anything still queued is dropped first - finishing a slider drag on
        /// the way out and then restoring would write the wrong value last.
        /// </summary>
        public void RestoreAll()
        {
            KeyValuePair<(string Device, MonitorSetting Setting), int>[] originals;
            lock (_gate)
            {
                _pending.Clear();
                originals = _original.ToArray();
            }

            foreach (var item in originals)
            {
                try { _control.Set(item.Key.Device, item.Key.Setting, item.Value); }
                catch { /* best effort - a monitor that has been unplugged can't be restored */ }
            }
        }

        /// <summary>What this control was set to when PlexusX found it.</summary>
        public int? OriginalRaw(string deviceName, MonitorSetting setting)
        {
            lock (_gate)
                return _original.TryGetValue((deviceName, setting), out var value) ? value : null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Stop before disposing: a queued callback still fires otherwise, and it would
            // run after the restore below and undo it.
            _flushTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _flushTimer?.Dispose();
            _flushTimer = null;

            RestoreAll();
        }
    }
}

