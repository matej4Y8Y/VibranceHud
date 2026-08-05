using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VibranceHud.Capabilities
{
    /// <summary>Reads and writes the screen gamma ramp. Seam so the probe's logic is testable
    /// without a display driver.</summary>
    public interface IGammaProbeTarget
    {
        bool TrySet(ushort[] ramp);
        ushort[]? TryGet();
    }

    /// <summary>
    /// Measures what this PC can actually do, once, at startup.
    ///
    /// Exists because the app used to assert things about the user's machine rather than
    /// check them, and was repeatedly wrong in the direction that made a working feature look
    /// broken. Everything here is observed.
    ///
    /// Never throws. A probe that crashes the app it is meant to make robust is worse than no
    /// probe at all, so every individual test degrades to "unknown" on failure.
    /// </summary>
    public static class CapabilityProbe
    {
        /// <summary>
        /// How close a read-back has to be to count as the same curve, in 16-bit ramp units
        /// out of 65535. Roughly 0.8% - loose enough for driver rounding, tight enough that a
        /// clamped curve never passes as an applied one.
        /// </summary>
        internal const int SameCurveTolerance = 512;

        /// <summary>
        /// Decide what happened to a ramp we wrote, from what came back.
        ///
        /// Pure, because this is the part that actually matters and the part most likely to
        /// be wrong. The three-way answer is the point: SetDeviceGammaRamp returning true only
        /// means Windows accepted the call, not that it applied the curve - it limits how far
        /// a ramp may deviate from linear, so a rejected-in-practice write looks identical to
        /// a successful one from the return value alone.
        /// </summary>
        internal static GammaSupport Classify(ushort[] written, ushort[]? readback, ushort[] identity)
        {
            if (readback == null || written.Length == 0 || readback.Length != written.Length)
                return GammaSupport.Untested;

            double toWritten = MeanAbsoluteDifference(readback, written);
            double toIdentity = MeanAbsoluteDifference(readback, identity);

            // Came back as we wrote it.
            if (toWritten <= SameCurveTolerance) return GammaSupport.Working;

            // Came back linear: the write was accepted and discarded.
            if (toIdentity <= SameCurveTolerance) return GammaSupport.Refused;

            // Somewhere in between - Windows flattened it toward linear but kept some of it.
            return GammaSupport.Clamped;
        }

        private static double MeanAbsoluteDifference(ushort[] a, ushort[] b)
        {
            long total = 0;
            for (int i = 0; i < a.Length; i++) total += Math.Abs(a[i] - b[i]);
            return total / (double)a.Length;
        }

        /// <summary>
        /// A curve distinctive enough that a clamp is obvious.
        ///
        /// Deliberately strong. A gentle test curve sits inside what Windows permits even on a
        /// restricted machine, so it would come back intact and report Working on a PC where
        /// the user's actual settings will be flattened.
        /// </summary>
        internal static ushort[] TestRamp() => GammaCurve.Build(1.6f);

        /// <summary>Measure the gamma path, then put back exactly what was there before.</summary>
        internal static GammaSupport ProbeGamma(IGammaProbeTarget target)
        {
            if (target == null) return GammaSupport.Untested;

            ushort[]? original = null;
            try
            {
                original = target.TryGet();

                var written = TestRamp();
                if (!target.TrySet(written)) return GammaSupport.Refused;

                var readback = target.TryGet();
                return Classify(written, readback, GammaCurve.Identity());
            }
            catch
            {
                return GammaSupport.Untested;
            }
            finally
            {
                // Always restore. The probe runs before the user's own settings are applied,
                // but leaving a test curve on screen if that step ever fails would be a very
                // visible bug.
                try
                {
                    if (original != null) target.TrySet(original);
                    else target.TrySet(GammaCurve.Identity());
                }
                catch { /* nothing further we can do */ }
            }
        }

        /// <summary>Run every test. Never throws.</summary>
        public static MachineCapabilities Run(bool driverVibrance, OverlayMode overlayPath,
            IGammaProbeTarget? gammaTarget = null)
        {
            var gamma = GammaSupport.Untested;
            try { gamma = ProbeGamma(gammaTarget ?? new ScreenGammaTarget()); }
            catch { }

            return new MachineCapabilities(
                GammaRamp: gamma,
                HdrActive: SafeHdr(),
                Gpu: SafeVendor(),
                DriverVibrance: driverVibrance,
                MonitorCount: SafeMonitorCount(),
                MixedDpi: SafeMixedDpi(),
                Elevated: SafeElevated(),
                OverlayPath: overlayPath);
        }

        // ---- individual tests, each independently safe ---------------------------------

        private static bool SafeHdr()
        {
            try { return HdrDetection.AnyDisplayInHdr(); }
            catch { return false; }
        }

        private static GpuVendor SafeVendor()
        {
            try { return GpuDetection.PrimaryVendor(); }
            catch { return GpuVendor.Unknown; }
        }

        private static int SafeMonitorCount()
        {
            try { return Math.Max(1, Screen.AllScreens.Length); }
            catch { return 1; }
        }

        /// <summary>
        /// Whether the monitors run at different scale factors.
        ///
        /// Worth knowing because it is the arrangement that breaks per-monitor DPI handling,
        /// and because a bug report from a mixed-DPI machine deserves to say so up front.
        /// </summary>
        private static bool SafeMixedDpi()
        {
            try
            {
                var screens = Screen.AllScreens;
                if (screens.Length < 2) return false;

                uint first = DpiOf(screens[0]);
                for (int i = 1; i < screens.Length; i++)
                    if (DpiOf(screens[i]) != first) return true;

                return false;
            }
            catch { return false; }
        }

        private static uint DpiOf(Screen screen)
        {
            var pt = new POINT { X = screen.Bounds.Left + 1, Y = screen.Bounds.Top + 1 };
            IntPtr monitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
            return GetDpiForMonitor(monitor, 0, out uint x, out _) == 0 ? x : 96;
        }

        private static bool SafeElevated()
        {
            try { return SystemTweaks.SystemTweakService.IsElevated(); }
            catch { return false; }
        }

        // ---- Win32 --------------------------------------------------------------------

        private const int MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, int flags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);

        /// <summary>The real screen, behind the seam.</summary>
        private sealed class ScreenGammaTarget : IGammaProbeTarget
        {
            [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
            [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

            [DllImport("gdi32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool SetDeviceGammaRamp(IntPtr hDC, ushort[] ramp);

            [DllImport("gdi32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool GetDeviceGammaRamp(IntPtr hDC, ushort[] ramp);

            public bool TrySet(ushort[] ramp)
            {
                var dc = GetDC(IntPtr.Zero);
                if (dc == IntPtr.Zero) return false;
                try { return SetDeviceGammaRamp(dc, ramp); }
                finally { ReleaseDC(IntPtr.Zero, dc); }
            }

            public ushort[]? TryGet()
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
        }
    }
}
