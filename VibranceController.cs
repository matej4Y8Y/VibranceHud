using System;
using NvAPIWrapper;
using NvAPIWrapper.Native;
using NvAPIWrapper.Native.Display;
using NvAPIWrapper.Native.Display.Structures;

namespace VibranceHud
{
    /// <summary>
    /// Thin wrapper around NVIDIA's Digital Vibrance Control (DVC) - the exact same
    /// driver-level feature behind the "Digital Vibrance" slider in NVIDIA Control Panel.
    ///
    /// This is deliberately capped at 0-100: that's not this class being cautious, it's
    /// the actual ceiling the NVIDIA driver enforces on this API, slider or no slider.
    /// Going past it means a different mechanism entirely (a custom shader injected into
    /// a specific game) - that's tier 2, not this class.
    /// </summary>
    public sealed class VibranceController : IVibranceController
    {
        private readonly DisplayHandle _display;

        public VibranceController()
        {
            NVIDIA.Initialize();

            var displays = DisplayApi.EnumNvidiaDisplayHandle();
            if (displays.Length == 0)
            {
                throw new InvalidOperationException(
                    "No NVIDIA-driven display was found.");
            }

            // Primary display for now - if you run multiple monitors on the same GPU
            // and want per-monitor control, this is the spot to add a picker.
            _display = displays[0];
        }

        public int CurrentLevel => DisplayApi.GetDVCInfoEx(_display).CurrentLevel;

        public int DefaultLevel => DisplayApi.GetDVCInfoEx(_display).DefaultLevel;

        public void SetLevel(int level)
        {
            level = Math.Clamp(level, 0, 100);
            DisplayApi.SetDVCLevelEx(_display, level);
        }
    }
}
