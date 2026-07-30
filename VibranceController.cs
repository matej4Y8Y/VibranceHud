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
        // EVERY NVIDIA-driven display, not just the first.
        //
        // This used to keep displays[0] alone and write DVC only there. On a multi-monitor
        // rig the effect did apply - just possibly to a monitor the user wasn't looking at,
        // which is indistinguishable from "the slider does nothing". EnumNvidiaDisplayHandle
        // ordering isn't guaranteed to match the Windows primary display, so which monitor
        // won was effectively arbitrary. "System-wide vibrance" should mean all of them.
        private readonly DisplayHandle[] _displays;

        public VibranceController()
        {
            NVIDIA.Initialize();

            _displays = DisplayApi.EnumNvidiaDisplayHandle();
            if (_displays.Length == 0)
            {
                throw new InvalidOperationException(
                    "No NVIDIA-driven display was found.");
            }
        }

        // Read from the first display only: these exist to seed the UI with a single
        // number, and a rig whose monitors disagree isn't worth modelling here.
        public int CurrentLevel => DisplayApi.GetDVCInfoEx(_displays[0]).CurrentLevel;

        public int DefaultLevel => DisplayApi.GetDVCInfoEx(_displays[0]).DefaultLevel;

        public bool IsAvailable => true;

        public void SetLevel(int level)
        {
            level = Math.Clamp(level, 0, 100);

            // One display failing must not cost the others - a handle can go stale when a
            // monitor sleeps, is unplugged, or switches input. Reuses the same per-item
            // tolerance as the DX overlay's per-monitor init.
            TolerantOutputBuilder.Build(_displays.Length,
                i => DisplayApi.SetDVCLevelEx(_displays[i], level));
        }
    }
}
