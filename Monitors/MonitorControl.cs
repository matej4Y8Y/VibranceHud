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

        private const uint Red = 0, Green = 1, Blue = 2;

        public static bool SetBrightness(int value) =>
            OnFirstMonitor(h => SetMonitorBrightness(h, (uint)Math.Clamp(value, 0, 100)));

        public static bool SetContrast(int value) =>
            OnFirstMonitor(h => SetMonitorContrast(h, (uint)Math.Clamp(value, 0, 100)));

        /// <summary>
        /// Reduce the blue channel's gain.
        /// </summary>
        /// <param name="strength">0 = off, 100 = as far down as the panel allows.</param>
        /// <remarks>
        /// Implemented as a blue-gain reduction rather than a vendor "reader mode" VCP code,
        /// because gain is standard MCCS and works across manufacturers while the reader-mode
        /// codes differ per vendor and would half-work. The floor is 50 rather than 0: taking
        /// blue all the way out leaves a monitor somebody cannot read, and a control whose far
        /// end is unusable is a control with a broken range.
        /// </remarks>
        public static bool SetLowBlueLight(int strength)
        {
            int gain = 100 - Math.Clamp(strength, 0, 100) / 2;
            return OnFirstMonitor(h => SetMonitorRedGreenOrBlueGain(h, Blue, (uint)gain));
        }

        /// <summary>
        /// Read brightness twice and only believe a bottom-of-range answer if it repeats.
        ///
        /// The dev machine's panel reports current = 0 on first contact while plainly lit, so
        /// a "remember the original" that trusted one read would store 0 and later restore the
        /// screen to black. A second read is cheap next to that.
        /// </summary>
        public static int? ReadTrustedBrightness()
        {
            int? first = ReadBrightness();
            if (first is not 0) return first;

            int? second = ReadBrightness();
            return second is 0 ? null : second;
        }

        private static int? ReadBrightness()
        {
            int? result = null;
            OnFirstMonitor(h =>
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
        /// Run one operation against the first monitor that opens.
        ///
        /// Single-monitor for now, deliberately: applying to every panel would change a second
        /// screen the user never asked about, and choosing between them needs UI that does not
        /// exist yet.
        /// </summary>
        private static bool OnFirstMonitor(Func<IntPtr, bool> action)
        {
            bool done = false;

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
                            try { if (action(m.hPhysicalMonitor)) { done = true; } }
                            catch { }
                            if (done) break;
                        }
                    }
                    finally
                    {
                        // Always released, even when the write failed - these are OS handles.
                        foreach (var m in monitors)
                            try { DestroyPhysicalMonitor(m.hPhysicalMonitor); } catch { }
                    }

                    if (done) break;
                }
            }
            catch { /* the panel refusing is the expected case, not an error */ }

            return done;
        }
    }
}
