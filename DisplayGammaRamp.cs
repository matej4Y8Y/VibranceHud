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

        private readonly Func<IntPtr, ushort[], bool> _setRamp;

        public DisplayGammaRamp() : this(SetDeviceGammaRamp) { }

        /// <summary>Test seam: lets a fake stand in for the native call so a driver refusal
        /// (SetDeviceGammaRamp returning false) can be exercised without a real display
        /// driver that actually refuses ramps.</summary>
        internal DisplayGammaRamp(Func<IntPtr, ushort[], bool> setRamp)
        {
            _setRamp = setRamp;
        }

        /// <summary>True when the most recent <see cref="Apply"/> was refused by the driver
        /// (or the device context couldn't be obtained at all). SetDeviceGammaRamp signals a
        /// refusal by returning false, not by throwing - that return value used to be
        /// discarded outright, so a refused ramp silently looked like it had taken effect.</summary>
        public bool LastApplyFailed { get; private set; }

        public void Apply(ushort[] ramp)
        {
            var dc = GetDC(IntPtr.Zero); // the whole screen
            if (dc == IntPtr.Zero)
            {
                LastApplyFailed = true;
                System.Diagnostics.Debug.WriteLine("DisplayGammaRamp: GetDC failed; gamma ramp not applied.");
                return;
            }
            try
            {
                LastApplyFailed = !_setRamp(dc, ramp);
                if (LastApplyFailed)
                    System.Diagnostics.Debug.WriteLine("DisplayGammaRamp: driver refused SetDeviceGammaRamp; screen gamma unchanged.");
            }
            finally { ReleaseDC(IntPtr.Zero, dc); }
        }

        public void Reset() => Apply(GammaCurve.Identity());

        public void Dispose() => Reset();
    }
}
