using System;
using System.IO;
using VibranceHud;
using VibranceHud.Capabilities;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Runs the probe against the real machine.
    ///
    /// Deliberately asserts almost nothing about the answers - they are properties of
    /// whatever box this happens to run on, and pinning them would make the suite fail on a
    /// different PC. What it does pin is that the probe completes, never throws, and leaves
    /// the screen as it found it. Those hold everywhere.
    ///
    /// It also writes what it measured to a file, so a developer can see what this machine
    /// actually reports without attaching a debugger.
    /// </summary>
    public sealed class CapabilityProbeLiveTests
    {
        [Fact]
        public void ProbeRunsOnThisMachineAndReportsSomething()
        {
            var caps = CapabilityProbe.Run(driverVibrance: false, overlayPath: OverlayMode.Mag);

            Assert.NotNull(caps);
            Assert.True(caps.MonitorCount >= 1);

            var report =
                $"gamma        : {caps.GammaRamp}\n" +
                $"hdr active   : {caps.HdrActive}\n" +
                $"gpu          : {caps.Gpu}\n" +
                $"monitors     : {caps.MonitorCount}\n" +
                $"mixed dpi    : {caps.MixedDpi}\n" +
                $"elevated     : {caps.Elevated}\n" +
                $"tone works   : {caps.ToneControlsWork}\n" +
                $"limitation   : {(caps.ToneLimitation == "" ? "(none)" : caps.ToneLimitation)}\n";

            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "plexusx-capabilities.txt"), report);
        }

        /// <summary>
        /// The probe writes a test curve to the screen. If it ever failed to put things back,
        /// the user would be left staring at a deliberately wrong gamma - so this is the one
        /// behaviour worth checking against the real display.
        /// </summary>
        [Fact]
        public void ProbeLeavesTheScreenAsItFoundIt()
        {
            var before = ReadScreenRamp();
            CapabilityProbe.Run(driverVibrance: false, overlayPath: OverlayMode.Mag);
            var after = ReadScreenRamp();

            if (before == null || after == null) return;   // can't read here; nothing to check

            long drift = 0;
            for (int i = 0; i < before.Length; i++) drift += Math.Abs(before[i] - after[i]);
            double mean = drift / (double)before.Length;

            Assert.True(mean <= CapabilityProbe.SameCurveTolerance,
                $"probe left the screen {mean:F0} away from where it started");
        }

        private static ushort[]? ReadScreenRamp()
        {
            var dc = GetDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) return null;
            try
            {
                var ramp = new ushort[GammaCurve.Entries * 3];
                return GetDeviceGammaRamp(dc, ramp) ? ramp : null;
            }
            finally { ReleaseDC(IntPtr.Zero, dc); }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetDeviceGammaRamp(IntPtr hDC, ushort[] ramp);
    }
}
