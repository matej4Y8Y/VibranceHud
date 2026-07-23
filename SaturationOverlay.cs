using System;
using System.Runtime.InteropServices;

namespace VibranceHud
{
    /// <summary>
    /// System-wide saturation via the Windows Magnification API's fullscreen color effect
    /// (Magnification.dll). This is how "past 100%" is achieved: the NVIDIA driver caps
    /// digital vibrance at 100, so beyond that we apply a saturation color matrix to every
    /// pixel on screen ourselves.
    ///
    /// Needs no elevation or signing. Windows tears the effect down automatically if this
    /// process dies; <see cref="Dispose"/> also clears it on a graceful exit so the screen
    /// is never left oversaturated.
    ///
    /// Does not affect exclusive-fullscreen games or DRM-protected video, and shares the
    /// pipeline with Windows Night Light / Color Filters.
    /// </summary>
    public sealed class SaturationOverlay : ISaturationOverlay, IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MAGCOLOREFFECT
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
            public float[] transform;
        }

        [DllImport("Magnification.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MagInitialize();

        [DllImport("Magnification.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MagUninitialize();

        [DllImport("Magnification.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MagSetFullscreenColorEffect(ref MAGCOLOREFFECT pEffect);

        private static readonly float[] Identity =
        {
            1f, 0f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f, 0f,
            0f, 0f, 1f, 0f, 0f,
            0f, 0f, 0f, 1f, 0f,
            0f, 0f, 0f, 0f, 1f,
        };

        private bool _initialized;

        /// <summary>True if the Magnification runtime is available; false means "tier-1 only".</summary>
        public bool IsAvailable => _initialized;

        public SaturationOverlay()
        {
            _initialized = MagInitialize();
        }

        public void Apply(float[] matrix)
        {
            if (!_initialized) return;
            Send(matrix);
        }

        public void Clear()
        {
            if (!_initialized) return;
            Send(Identity);
        }

        private static void Send(float[] transform)
        {
            var effect = new MAGCOLOREFFECT { transform = transform };
            MagSetFullscreenColorEffect(ref effect);
        }

        public void Dispose()
        {
            if (!_initialized) return;
            Send(Identity); // never leave the desktop tinted or oversaturated
            MagUninitialize();
            _initialized = false;
        }
    }
}
