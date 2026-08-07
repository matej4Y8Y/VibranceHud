using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VibranceHud.Monitors
{
    /// <summary>
    /// What one physical panel will let us change over DDC/CI.
    ///
    /// <paramref name="Refusal"/> is empty when the panel answered. Otherwise it holds a
    /// plain-English reason, because a monitor tab full of controls that quietly do nothing is
    /// worse than a tab that says the panel will not talk to us.
    /// </summary>
    public sealed record MonitorCapability(
        string Description,
        bool SupportsBrightness,
        bool SupportsContrast,
        bool SupportsRgbGain,
        int BrightnessMin,
        int BrightnessCurrent,
        int BrightnessMax,
        string Refusal);

    /// <summary>
    /// Asks every attached monitor what it supports, over DDC/CI.
    ///
    /// DDC/CI is a serial protocol carried on the display cable, so support varies enormously:
    /// many panels do not implement it, several implement it badly, and laptop internal
    /// displays almost never do. Every call here is therefore treated as likely to fail, and a
    /// failure is recorded as a refusal rather than thrown - this runs during startup, and an
    /// exception here would take the whole app down before a window ever appeared.
    ///
    /// Reads only. Nothing in this file changes a monitor's settings.
    /// </summary>
    public static class MonitorProbe
    {
        // ---- Win32 ------------------------------------------------------------------------

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
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint count);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint count,
            [Out] PHYSICAL_MONITOR[] monitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetMonitorBrightness(IntPtr h, ref uint min, ref uint cur, ref uint max);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetMonitorContrast(IntPtr h, ref uint min, ref uint cur, ref uint max);

        /// <summary>gainType: 0 red, 1 green, 2 blue.</summary>
        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetMonitorRedGreenOrBlueGain(IntPtr h, uint gainType,
            ref uint min, ref uint cur, ref uint max);

        // ---- probe ------------------------------------------------------------------------

        /// <summary>
        /// Every attached monitor and what it will allow. Never throws; a panel that will not
        /// answer comes back with a refusal instead.
        /// </summary>
        public static IReadOnlyList<MonitorCapability> Probe()
        {
            var found = new List<MonitorCapability>();

            try
            {
                var handles = new List<IntPtr>();

                // The callback cannot be allowed to throw back through unmanaged code - that
                // tears down the process rather than raising a catchable exception.
                bool Collect(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data)
                {
                    try { handles.Add(hMonitor); } catch { }
                    return true;
                }

                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Collect, IntPtr.Zero);

                foreach (var hMonitor in handles)
                    found.AddRange(Describe(hMonitor));
            }
            catch (Exception ex)
            {
                // dxva2.dll missing, or the enumeration itself refused. One entry so the UI
                // still has something honest to show.
                found.Add(new MonitorCapability("Display", false, false, false, 0, 0, 0,
                    "Windows would not list the monitors: " + ex.Message));
            }

            if (found.Count == 0)
                found.Add(new MonitorCapability("Display", false, false, false, 0, 0, 0,
                    "No monitor answered. This is normal on a laptop's built-in screen."));

            return found;
        }

        /// <summary>
        /// Built into a list rather than yielded: C# forbids yield inside try/catch, and every
        /// call in here has to be guarded because the whole point is that panels refuse.
        /// </summary>
        private static List<MonitorCapability> Describe(IntPtr hMonitor)
        {
            var results = new List<MonitorCapability>();

            uint count = 0;
            PHYSICAL_MONITOR[] monitors;

            try
            {
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref count) || count == 0)
                {
                    results.Add(Refused("Display",
                        "This display does not expose a physical monitor to Windows."));
                    return results;
                }

                monitors = new PHYSICAL_MONITOR[count];
                if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors))
                {
                    results.Add(Refused("Display",
                        "Windows could not open a connection to this monitor."));
                    return results;
                }
            }
            catch (Exception ex)
            {
                results.Add(Refused("Display", "This monitor could not be opened: " + ex.Message));
                return results;
            }

            foreach (var m in monitors)
            {
                try { results.Add(Interrogate(m)); }
                catch (Exception ex)
                {
                    results.Add(Refused(Name(m.szPhysicalMonitorDescription),
                        "This monitor stopped responding while being read: " + ex.Message));
                }
                finally
                {
                    // Always released, even when the read failed - these are real OS handles.
                    try { DestroyPhysicalMonitor(m.hPhysicalMonitor); } catch { }
                }
            }

            return results;
        }

        /// <summary>
        /// Ask one open handle what it supports. Each capability is probed independently,
        /// because panels routinely answer one of these and refuse the others.
        /// </summary>
        private static MonitorCapability Interrogate(PHYSICAL_MONITOR m)
        {
            string name = Name(m.szPhysicalMonitorDescription);

            uint bMin = 0, bCur = 0, bMax = 0;
            bool brightness = TryRead(() => GetMonitorBrightness(m.hPhysicalMonitor, ref bMin, ref bCur, ref bMax));

            // A driver that answers with a degenerate range has not really answered. Building a
            // slider on min==max would move the user's brightness somewhere arbitrary.
            if (brightness && (bMax <= bMin || bCur < bMin || bCur > bMax))
            {
                brightness = false;
                bMin = bCur = bMax = 0;
            }

            uint cMin = 0, cCur = 0, cMax = 0;
            bool contrast = TryRead(() => GetMonitorContrast(m.hPhysicalMonitor, ref cMin, ref cCur, ref cMax))
                            && cMax > cMin;

            uint gMin = 0, gCur = 0, gMax = 0;
            bool gain = TryRead(() => GetMonitorRedGreenOrBlueGain(m.hPhysicalMonitor, 2, ref gMin, ref gCur, ref gMax))
                        && gMax > gMin;

            string refusal = brightness || contrast || gain
                ? ""
                : "This monitor is connected but will not accept DDC/CI control. "
                  + "That is usually a monitor setting - look for \"DDC/CI\" in its on-screen menu.";

            return new MonitorCapability(name, brightness, contrast, gain,
                (int)bMin, (int)bCur, (int)bMax, refusal);
        }

        /// <summary>A refused call is the normal case, not an error worth propagating.</summary>
        private static bool TryRead(Func<bool> call)
        {
            try { return call(); }
            catch { return false; }
        }

        private static string Name(string? description) =>
            string.IsNullOrWhiteSpace(description) ? "Display" : description.Trim();

        private static MonitorCapability Refused(string name, string why) =>
            new(name, false, false, false, 0, 0, 0, why);
    }
}
