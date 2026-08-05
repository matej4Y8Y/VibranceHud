using System;
using System.Runtime.InteropServices;

namespace VibranceHud.Capabilities
{
    /// <summary>
    /// Whether any display is currently running in HDR.
    ///
    /// Worth its own file because it is the single most likely reason a user's tone controls
    /// do nothing, and nothing in the app checked for it before. Windows applies its own
    /// colour pipeline in HDR and ignores or clamps SetDeviceGammaRamp, so every advanced
    /// colour slider can move while the screen stays exactly as it was.
    ///
    /// Uses the CCD (Connecting and Configuring Displays) API rather than DXGI: it reports
    /// what Windows is actually doing right now, whereas the DXGI colour space reflects what
    /// the swap chain asked for.
    /// </summary>
    internal static class HdrDetection
    {
        public static bool AnyDisplayInHdr()
        {
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount) != 0)
                return false;

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths,
                    ref modeCount, modes, IntPtr.Zero) != 0)
                return false;

            for (int i = 0; i < pathCount; i++)
            {
                var info = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                        size = Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                        adapterId = paths[i].targetInfo.adapterId,
                        id = paths[i].targetInfo.id,
                    }
                };

                if (DisplayConfigGetDeviceInfo(ref info) != 0) continue;

                // Bit 1 is advancedColorEnabled - HDR actually switched on, as opposed to
                // bit 0 which only says the display is capable of it.
                if ((info.value & 0x2) != 0) return true;
            }

            return false;
        }

        private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
        private const int DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9;

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(uint flags,
            ref uint pathCount, [Out] DISPLAYCONFIG_PATH_INFO[] paths,
            ref uint modeCount, [Out] DISPLAYCONFIG_MODE_INFO[] modes, IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO info);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId; public uint id; public uint modeInfoIdx; public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId; public uint id; public uint modeInfoIdx;
            public uint outputTechnology; public uint rotation; public uint scaling;
            public ulong refreshRate; public uint scanLineOrdering;
            [MarshalAs(UnmanagedType.Bool)] public bool targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        /// <summary>Opaque here - the mode array only has to round-trip through
        /// QueryDisplayConfig, and its union layout is irrelevant to this question.</summary>
        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct DISPLAYCONFIG_MODE_INFO { }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public int type; public int size; public LUID adapterId; public uint id;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value;              // bitfield: advancedColorSupported | advancedColorEnabled | ...
            public uint colorEncoding;
            public int bitsPerColorChannel;
        }
    }

    /// <summary>Which GPU is driving the primary display.</summary>
    internal static class GpuDetection
    {
        public static GpuVendor PrimaryVendor()
        {
            using var factory = new SharpDX.DXGI.Factory1();
            using var adapter = factory.GetAdapter1(0);

            return adapter.Description1.VendorId switch
            {
                0x10DE => GpuVendor.Nvidia,
                0x1002 or 0x1022 => GpuVendor.Amd,
                0x8086 => GpuVendor.Intel,
                _ => GpuVendor.Other,
            };
        }
    }
}
