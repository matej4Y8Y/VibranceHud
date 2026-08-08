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
    /// <summary>
    /// A range the panel reported, in the panel's own units.
    ///
    /// MCCS does not fix these. Brightness is often 0-100 but need not be, and gain is a
    /// one-byte value that is routinely 0-255. Assuming 0-100 and writing an absolute number
    /// is how you turn somebody's screen orange: a "gain 100" meant as neutral is 39% on a
    /// 0-255 panel, which is a heavy blue cut.
    /// </summary>
    public readonly record struct PanelRange(int Min, int Current, int Max)
    {
        public bool IsUsable => Max > Min && Current >= Min && Current <= Max;

        /// <summary>Map a 0-100 slider onto the panel's own scale.</summary>
        public int FromPercent(int percent) =>
            Min + (int)Math.Round((Max - Min) * Math.Clamp(percent, 0, 100) / 100.0);

        /// <summary>And back, so a slider can be seeded from what the panel reported.</summary>
        public int ToPercent(int raw) => Max > Min
            ? Math.Clamp((int)Math.Round((raw - Min) * 100.0 / (Max - Min)), 0, 100)
            : 0;
    }

    public sealed record MonitorCapability(
        string Description,
        bool SupportsBrightness,
        bool SupportsContrast,
        bool SupportsRgbGain,
        int BrightnessMin,
        int BrightnessCurrent,
        int BrightnessMax,
        string Refusal)
    {
        /// <summary>The panel's real brightness range, as reported.</summary>
        public PanelRange Brightness { get; init; } =
            new(BrightnessMin, BrightnessCurrent, BrightnessMax);

        /// <summary>The panel's real contrast range. Read by the probe rather than assumed -
        /// it used to be read and thrown away, which is why the UI invented a value.</summary>
        public PanelRange Contrast { get; init; }

        /// <summary>The panel's real blue-gain range, and its current gain. The current value
        /// is what "off" means for low blue light - not the literal number 100.</summary>
        public PanelRange BlueGain { get; init; }

        /// <summary>
        /// Which physical monitor this is, so a write can be aimed at it.
        ///
        /// The enumeration index rather than a device path: DDC/CI has no stable identifier
        /// Windows will hand back through this API, and the index is consistent for as long as
        /// the display arrangement does not change - which is exactly as long as a card built
        /// from this capability is on screen.
        /// </summary>
        public int Index { get; init; }
    }

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
                    foreach (var cap in Describe(hMonitor))
                        // Indexed in enumeration order, so a write can be aimed at the panel
                        // whose card the user is actually looking at.
                        found.Add(cap with { Index = found.Count });
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

            // Every range is carried, not just brightness. The contrast and gain ranges used
            // to be read here and discarded, which left the UI inventing a contrast value and
            // the writer assuming 0-100 for a gain that is commonly 0-255.
            return new MonitorCapability(name, brightness, contrast, gain,
                (int)bMin, (int)bCur, (int)bMax, refusal)
            {
                Contrast = contrast ? new PanelRange((int)cMin, (int)cCur, (int)cMax) : default,
                BlueGain = gain ? new PanelRange((int)gMin, (int)gCur, (int)gMax) : default,
            };
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
