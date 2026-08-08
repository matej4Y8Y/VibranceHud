using System;
using System.Runtime.InteropServices;

namespace VibranceHud.Monitors
{
    /// <summary>
    /// Writes to the physical panel over DDC/CI.
    ///
    /// Separate from <see cref="MonitorProbe"/> on purpose: the probe only reads, and keeping
    /// the writing somewhere else means nothing that runs at startup can change the user's
    /// monitor. Every call opens a handle, does one thing and closes it, because holding a
    /// physical monitor handle open across a session is a good way to be holding a stale one
    /// after the user unplugs a cable.
    ///
    /// Nothing here throws. A panel refusing a write is the expected case.
    /// </summary>
    public static class MonitorControl
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr h, ref uint count);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr h, uint count,
            [Out] PHYSICAL_MONITOR[] monitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool DestroyPhysicalMonitor(IntPtr h);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool SetMonitorBrightness(IntPtr h, uint value);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetMonitorBrightness(IntPtr h, ref uint min, ref uint cur, ref uint max);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool SetMonitorContrast(IntPtr h, uint value);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool SetMonitorRedGreenOrBlueGain(IntPtr h, uint gainType, uint value);

        private const uint Blue = 2;

        /// <summary>
        /// Set brightness on one panel, as a percentage of the range that panel reported.
        ///
        /// The percentage matters. Brightness ranges are panel-defined, and writing an
        /// absolute 0-100 into a panel that reports 20-80 makes the top and bottom quarters of
        /// the slider dead - the user drags and nothing happens.
        /// </summary>
        public static bool SetBrightnessPercent(int monitorIndex, PanelRange range, int percent) =>
            OnMonitor(monitorIndex, h => SetMonitorBrightness(h, (uint)range.FromPercent(percent)));

        public static bool SetContrastPercent(int monitorIndex, PanelRange range, int percent) =>
            OnMonitor(monitorIndex, h => SetMonitorContrast(h, (uint)range.FromPercent(percent)));

        /// <summary>
        /// Reduce the blue channel's gain, relative to where the panel already had it.
        /// </summary>
        /// <param name="strength">0 = leave the panel's own gain alone, 100 = as far down as
        /// this implementation is willing to go.</param>
        /// <remarks>
        /// Blue gain rather than a vendor "reader mode" VCP code, because gain is standard
        /// MCCS and works across manufacturers while reader-mode codes differ per vendor and
        /// would half-work.
        ///
        /// Relative to the panel's CURRENT gain, not to an absolute number. The first version
        /// wrote "100" for off, which is neutral only if the panel's range happens to be
        /// 0-100; on a 0-255 panel - which is common, since MCCS gain is a byte - that is 39%,
        /// a heavy blue cut applied by a slider the user had just set to zero.
        ///
        /// The floor is half the panel's own range below its current gain: taking blue all the
        /// way out leaves a monitor nobody can read, and a control whose far end is unusable is
        /// a control with a broken range.
        /// </remarks>
        public static bool SetLowBlueLight(int monitorIndex, PanelRange gain, int strength)
        {
            int span = (gain.Current - gain.Min) / 2;
            int target = gain.Current - span * Math.Clamp(strength, 0, 100) / 100;

            return OnMonitor(monitorIndex,
                h => SetMonitorRedGreenOrBlueGain(h, Blue, (uint)Math.Clamp(target, gain.Min, gain.Max)));
        }

        /// <summary>Put one panel back exactly where it was, in its own units.</summary>
        public static bool RestoreBrightness(int monitorIndex, int raw) =>
            OnMonitor(monitorIndex, h => SetMonitorBrightness(h, (uint)raw));

        public static bool RestoreContrast(int monitorIndex, int raw) =>
            OnMonitor(monitorIndex, h => SetMonitorContrast(h, (uint)raw));

        public static bool RestoreBlueGain(int monitorIndex, int raw) =>
            OnMonitor(monitorIndex, h => SetMonitorRedGreenOrBlueGain(h, Blue, (uint)raw));

        /// <summary>
        /// Read brightness twice and only believe a bottom-of-range answer if it repeats.
        ///
        /// This machine's panel reports current = 0 on first contact while plainly lit, so a
        /// "remember the original" that trusted one read would store 0 and later restore the
        /// screen to black. A second read is cheap next to that.
        /// </summary>
        public static int? ReadTrustedBrightness(int monitorIndex)
        {
            int? first = ReadBrightness(monitorIndex);
            if (first is not 0) return first;

            int? second = ReadBrightness(monitorIndex);
            return second is 0 ? null : second;
        }

        private static int? ReadBrightness(int monitorIndex)
        {
            int? result = null;
            OnMonitor(monitorIndex, h =>
            {
                uint min = 0, cur = 0, max = 0;
                if (!GetMonitorBrightness(h, ref min, ref cur, ref max)) return false;
                if (max <= min) return false;
                result = (int)cur;
                return true;
            });
            return result;
        }

        /// <summary>
        /// Run one operation against the panel at <paramref name="monitorIndex"/>, counted in
        /// the same enumeration order the probe used.
        ///
        /// Targeted rather than "the first one that opens". The first version applied every
        /// card's slider to the first monitor, so on a two-screen desk dragging the second
        /// card's brightness dimmed the first one - the exact thing this method's own comment
        /// said it was avoiding.
        /// </summary>
        private static bool OnMonitor(int monitorIndex, Func<IntPtr, bool> action)
        {
            bool done = false;
            int seen = 0;

            try
            {
                var handles = new System.Collections.Generic.List<IntPtr>();

                bool Collect(IntPtr h, IntPtr hdc, ref RECT r, IntPtr d)
                {
                    try { handles.Add(h); } catch { }
                    return true;
                }

                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Collect, IntPtr.Zero);

                foreach (var hMonitor in handles)
                {
                    uint count = 0;
                    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref count) || count == 0)
                        continue;

                    var monitors = new PHYSICAL_MONITOR[count];
                    if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors)) continue;

                    try
                    {
                        foreach (var m in monitors)
                        {
                            if (seen++ != monitorIndex) continue;

                            try { done = action(m.hPhysicalMonitor); }
                            catch { }
                            break;
                        }
                    }
                    finally
                    {
                        // Always released, even when the write failed - these are OS handles.
                        foreach (var m in monitors)
                            try { DestroyPhysicalMonitor(m.hPhysicalMonitor); } catch { }
                    }

                    if (seen > monitorIndex) break;
                }
            }
            catch { /* the panel refusing is the expected case, not an error */ }

            return done;
        }
    }
}
