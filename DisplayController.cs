using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace VibranceHud
{
    /// <summary>
    /// Reads and changes the Windows desktop resolution. Rust launchers switch the whole
    /// desktop (not just the game), so the game picks the mode up on startup.
    ///
    /// Safety: we only ever apply a mode the driver itself reported, and we test it before
    /// committing - applying an unsupported mode is how you black-screen someone.
    /// </summary>
    public static class DisplayController
    {
        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int CDS_TEST = 0x02;
        private const int DISP_CHANGE_SUCCESSFUL = 0;

        private const int DM_PELSWIDTH = 0x80000;
        private const int DM_PELSHEIGHT = 0x100000;
        private const int DM_DISPLAYFREQUENCY = 0x400000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight;
            public int dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2;
            public int dmPanningWidth, dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE dm);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettings(ref DEVMODE dm, int flags);

        private static DEVMODE NewDevMode()
        {
            var dm = new DEVMODE
            {
                dmDeviceName = new string('\0', 32),
                dmFormName = new string('\0', 32)
            };
            dm.dmSize = (short)Marshal.SizeOf<DEVMODE>();
            return dm;
        }

        /// <summary>Every 32-bit mode the driver reports for the primary display.</summary>
        public static IReadOnlyList<DisplayMode> SupportedModes()
        {
            var modes = new List<DisplayMode>();
            var dm = NewDevMode();
            for (int i = 0; EnumDisplaySettings(null, i, ref dm); i++)
            {
                if (dm.dmBitsPerPel == 32)
                    modes.Add(new DisplayMode(dm.dmPelsWidth, dm.dmPelsHeight, dm.dmDisplayFrequency));
                dm = NewDevMode();
            }
            return modes;
        }

        /// <summary>The mode the desktop is in right now.</summary>
        public static DisplayMode? Current()
        {
            var dm = NewDevMode();
            if (!EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm)) return null;
            return new DisplayMode(dm.dmPelsWidth, dm.dmPelsHeight, dm.dmDisplayFrequency);
        }

        /// <summary>
        /// Switch the desktop to this resolution at the highest refresh rate the monitor
        /// supports for it. Refuses anything the driver didn't report.
        /// </summary>
        public static bool Apply(int width, int height)
        {
            var supported = SupportedModes();
            int hz = DisplayModes.MaxRefreshFor(supported, width, height);
            if (hz <= 0) return false; // not a mode this monitor offers

            var dm = NewDevMode();
            if (!EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm)) return false;

            dm.dmPelsWidth = width;
            dm.dmPelsHeight = height;
            dm.dmDisplayFrequency = hz;
            dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;

            // Ask the driver first; only commit if it says the mode is fine.
            if (ChangeDisplaySettings(ref dm, CDS_TEST) != DISP_CHANGE_SUCCESSFUL) return false;
            return ChangeDisplaySettings(ref dm, 0) == DISP_CHANGE_SUCCESSFUL;
        }

        /// <summary>
        /// Switch to this resolution at a specific refresh rate.
        ///
        /// Same driver test-then-commit as <see cref="Apply(int,int)"/>, and it refuses a
        /// combination the monitor never reported - applying an unreported mode is how you
        /// black-screen somebody with no way back.
        /// </summary>
        public static bool Apply(int width, int height, int hz)
        {
            if (hz <= 0) return Apply(width, height);
            if (!DisplayModes.IsSupported(SupportedModes(), width, height, hz)) return false;

            var dm = NewDevMode();
            if (!EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm)) return false;

            dm.dmPelsWidth = width;
            dm.dmPelsHeight = height;
            dm.dmDisplayFrequency = hz;
            dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;

            if (ChangeDisplaySettings(ref dm, CDS_TEST) != DISP_CHANGE_SUCCESSFUL) return false;
            return ChangeDisplaySettings(ref dm, 0) == DISP_CHANGE_SUCCESSFUL;
        }

        /// <summary>
        /// Return the desktop to the mode captured earlier.
        ///
        /// Restores the refresh rate too. It previously went through the resolution-only
        /// overload, which reapplies at the monitor's maximum - so a user who ran at 120Hz on
        /// a 240Hz panel got put back at 240 after every game, silently, and had to fix it in
        /// Windows each time.
        /// </summary>
        public static bool Restore(DisplayMode mode) =>
            Apply(mode.Width, mode.Height, mode.RefreshHz);

        /// <summary>
        /// Wait for a game to start and then exit, and put the desktop back.
        ///
        /// Fire-and-forget, and it restores on every exit path - including the game never
        /// starting at all. Leaving somebody on a stretched desktop because a launch failed
        /// is the worst outcome here, so the timeout restores rather than giving up.
        /// </summary>
        /// <param name="processName">Executable without ".exe", from the game catalogue.</param>
        public static void RestoreWhenGameExits(string processName, DisplayMode original,
            TimeSpan waitForStart)
        {
            if (string.IsNullOrWhiteSpace(processName)) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    var deadline = DateTime.UtcNow + waitForStart;

                    // Wait for it to appear (or give up and restore).
                    while (Process.GetProcessesByName(processName).Length == 0)
                    {
                        if (DateTime.UtcNow > deadline) { Restore(original); return; }
                        await Task.Delay(2000);
                    }

                    // Then wait for it to close.
                    while (Process.GetProcessesByName(processName).Length > 0)
                        await Task.Delay(3000);

                    Restore(original);
                }
                catch
                {
                    // Never let a background poll take the process down, and never leave the
                    // desktop switched because of one.
                    try { Restore(original); } catch { /* nothing left to try */ }
                }
            });
        }

        /// <summary>Kept for the Rust page's existing call.</summary>
        public static void RestoreWhenRustExits(DisplayMode original, TimeSpan waitForStart) =>
            RestoreWhenGameExits("RustClient", original, waitForStart);
    }
}
