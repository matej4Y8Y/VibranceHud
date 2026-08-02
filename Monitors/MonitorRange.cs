using System;
using System.Collections.Generic;
using System.Linq;

namespace VibranceHud.Monitors
{
    /// <summary>
    /// One control on one monitor, in that monitor's own numbers.
    ///
    /// Monitors disagree about what a number means - brightness might be 0-100 on one panel
    /// and 0-255 on the next, and some report a floor above zero because they refuse to go
    /// fully dark. The slider on screen is always 0-100%, so this is where the two meet.
    /// </summary>
    public sealed record MonitorRange
    {
        public int Min { get; }
        public int Max { get; }
        public int Current { get; }

        public MonitorRange(int min, int current, int max)
        {
            Min = Math.Min(min, max);
            Max = Math.Max(min, max);
            // Real monitors have been seen reporting a current value outside their own
            // reported range. Trust the range, not the reading.
            Current = Math.Clamp(current, Min, Max);
        }

        /// <summary>A range with no room in it isn't a control. Some monitors answer for a
        /// feature out of politeness and report min == max; showing a slider that cannot move
        /// is worse than not showing one.</summary>
        public bool IsUsable => Max > Min;

        /// <summary>Where the current value sits on a 0-100 slider.</summary>
        public int Percent => IsUsable
            ? (int)Math.Round((Current - Min) * 100.0 / (Max - Min), MidpointRounding.AwayFromZero)
            : 0;

        /// <summary>The monitor's own number for a slider position.</summary>
        public int RawFromPercent(int percent)
        {
            if (!IsUsable) return Min;
            percent = Math.Clamp(percent, 0, 100);
            return Min + (int)Math.Round((Max - Min) * percent / 100.0, MidpointRounding.AwayFromZero);
        }

        public MonitorRange With(int current) => new(Min, current, Max);
    }

    /// <summary>
    /// One monitor as found by the scan: what it is, and every control it actually answered
    /// for. The tab is built from this and nothing else - a setting that isn't in here is a
    /// setting this screen can't do, so it never appears.
    /// </summary>
    public sealed class MonitorSnapshot
    {
        private readonly IReadOnlyDictionary<MonitorSetting, MonitorRange> _settings;

        public MonitorSnapshot(
            string deviceName,
            string model,
            IReadOnlyDictionary<MonitorSetting, MonitorRange> settings)
        {
            DeviceName = deviceName;
            Model = model;
            _settings = settings;
        }

        public string DeviceName { get; }
        public string Model { get; }

        /// <summary>What to call it on screen. The model name comes off the monitor itself, so
        /// people see "Dell S2721DGF" rather than "DISPLAY1" and can tell which is which.</summary>
        public string Label => string.IsNullOrWhiteSpace(Model) ? DeviceName : Model;

        /// <summary>
        /// False when the monitor answered nothing at all.
        ///
        /// This has to be its own state rather than "no settings", because the usual cause is
        /// DDC/CI switched off in the monitor's own menu - one setting away from working. A
        /// user told "not supported" gives up; a user told "check your monitor's menu" doesn't.
        /// </summary>
        public bool RespondedAtAll => _settings.Values.Any(r => r.IsUsable);

        public bool Supports(MonitorSetting setting) =>
            _settings.TryGetValue(setting, out var range) && range.IsUsable;

        public MonitorRange? Range(MonitorSetting setting) =>
            _settings.TryGetValue(setting, out var range) && range.IsUsable ? range : null;

        public IEnumerable<MonitorSetting> Available =>
            _settings.Where(kv => kv.Value.IsUsable).Select(kv => kv.Key);
    }
}
