using System.Collections.Generic;

namespace VibranceHud.Monitors
{
    /// <summary>
    /// Talking to the monitors themselves.
    ///
    /// Behind an interface for the same reason the vibrance driver is: the real one needs
    /// hardware that answers, and none of the logic around it should need a monitor to be
    /// tested. The fake in the tests can pretend to be a panel that supports everything, one
    /// that supports nothing, or one that lies about its own range.
    /// </summary>
    public interface IMonitorControl
    {
        /// <summary>
        /// Ask every connected monitor what it can do. Slow - some panels take a moment to
        /// answer and a few never do - so this is never called on the UI thread.
        /// </summary>
        IReadOnlyList<MonitorSnapshot> Scan();

        /// <summary>Write a value in the monitor's own units. Returns false if the monitor
        /// refused, which happens even for settings it answered for.</summary>
        bool Set(string deviceName, MonitorSetting setting, int rawValue);
    }
}
