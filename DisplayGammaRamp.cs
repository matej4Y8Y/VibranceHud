using System;
using System.Runtime.InteropServices;

namespace VibranceHud
{
    /// <summary>
    /// Applies a gamma ramp to the screen via GDI's SetDeviceGammaRamp - the same
    /// mechanism f.lux and similar tools use. Unlike the Magnification color effect,
    /// Windows does NOT restore a gamma ramp when the process exits, so we always reset
    /// it on shutdown.
    /// </summary>
    public sealed class DisplayGammaRamp : IGammaRamp, IDisposable
    {
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDeviceGammaRamp(IntPtr hDC, ushort[] ramp);

        public void Apply(ushort[] ramp)
        {
            var dc = GetDC(IntPtr.Zero); // the whole screen
            if (dc == IntPtr.Zero) return;
            try { SetDeviceGammaRamp(dc, ramp); }
            catch { /* driver refused - leave the screen as-is */ }
            finally { ReleaseDC(IntPtr.Zero, dc); }
        }

        public void Reset() => Apply(GammaCurve.Identity());

        public void Dispose() => Reset();
    }
}
