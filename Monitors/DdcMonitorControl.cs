using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VibranceHud.Monitors
{
    /// <summary>
    /// The real thing: DDC/CI over the display cable, which is how the monitor's own menu
    /// settings are reached. Windows exposes it through dxva2.dll.
    ///
    /// Two things make this awkward and both are handled here rather than upstream:
    ///
    ///   - It is slow. A single read can take tens of milliseconds and a monitor that has
    ///     DDC/CI switched off can sit there until it times out. Never call this on the UI
    ///     thread.
    ///   - Monitors lie. They claim features they don't have, report values outside their own
    ///     ranges, and occasionally answer a question nobody asked. Every call is treated as
    ///     "might fail, might be nonsense" and anything that doesn't come back cleanly is
    ///     simply left out of the result.
    ///
    /// Handles are opened for the scan and closed straight after. Holding them open across a
    /// session sounds faster and means a monitor unplugged mid-game leaves a dead handle
    /// behind.
    /// </summary>
    public sealed class DdcMonitorControl : IMonitorControl
    {
        // ---- what to ask each monitor for ------------------------------------------------
        // The high-level calls (brightness, contrast, gain) report a real minimum, which some
        // panels set above zero. Everything else goes through raw VCP, where the floor is 0.
        private const byte VcpSharpness = 0x87;
        private const byte VcpVolume = 0x62;

        // Picture mode (0xDC) and input source (0x60) are deliberately not read.
        //
        // They are enumerated features - the value is a code from a list, not a position on a
        // scale - and the reply's "maximum" doesn't describe that list. A real monitor here
        // answered current=17, max=3 for input source, which is not a range at all: 17 is the
        // code for HDMI-1.
        //
        // Treating that as a slider would let someone drag their monitor onto a port with
        // nothing plugged into it. The screen goes black, and the app they'd use to undo it is
        // on the screen that just went black. Not shipping that.

        public IReadOnlyList<MonitorSnapshot> Scan()
        {
            var result = new List<MonitorSnapshot>();

            foreach (var (deviceName, hMonitor) in EnumerateMonitors())
            {
                foreach (var (handle, description) in PhysicalMonitors(hMonitor))
                {
                    try
                    {
                        result.Add(new MonitorSnapshot(deviceName, description, Read(handle)));
                    }
                    finally
                    {
                        DestroyPhysicalMonitor(handle);
                    }
                }
            }

            return result;
        }

        private static Dictionary<MonitorSetting, MonitorRange> Read(IntPtr handle)
        {
            var found = new Dictionary<MonitorSetting, MonitorRange>();

            if (TryGetMonitorBrightness(handle, out var min, out var cur, out var max))
                found[MonitorSetting.Brightness] = new MonitorRange(min, cur, max);

            if (TryGetMonitorContrast(handle, out min, out cur, out max))
                found[MonitorSetting.Contrast] = new MonitorRange(min, cur, max);

            foreach (var (setting, index) in new[]
                     {
                         (MonitorSetting.Red, 0u),
                         (MonitorSetting.Green, 1u),
                         (MonitorSetting.Blue, 2u),
                     })
            {
                if (TryGetGain(handle, index, out min, out cur, out max))
                    found[setting] = new MonitorRange(min, cur, max);
            }

            foreach (var (setting, code) in new[]
                     {
                         (MonitorSetting.Sharpness, VcpSharpness),
                         (MonitorSetting.Volume, VcpVolume),
                     })
            {
                if (TryGetVcp(handle, code, out cur, out max))
                    found[setting] = new MonitorRange(0, cur, max);
            }

            return found;
        }

        public bool Set(string deviceName, MonitorSetting setting, int rawValue)
        {
            foreach (var (name, hMonitor) in EnumerateMonitors())
            {
                if (!string.Equals(name, deviceName, StringComparison.OrdinalIgnoreCase)) continue;

                foreach (var (handle, _) in PhysicalMonitors(hMonitor))
                {
                    try
                    {
                        return Write(handle, setting, (uint)Math.Max(0, rawValue));
                    }
                    finally
                    {
                        DestroyPhysicalMonitor(handle);
                    }
                }
            }
            return false;
        }

        private static bool Write(IntPtr handle, MonitorSetting setting, uint value) => setting switch
        {
            MonitorSetting.Brightness => Safe(() => SetMonitorBrightness(handle, value)),
            MonitorSetting.Contrast => Safe(() => SetMonitorContrast(handle, value)),
            MonitorSetting.Red => Safe(() => SetMonitorRedGreenOrBlueGain(handle, 0, value)),
            MonitorSetting.Green => Safe(() => SetMonitorRedGreenOrBlueGain(handle, 1, value)),
            MonitorSetting.Blue => Safe(() => SetMonitorRedGreenOrBlueGain(handle, 2, value)),
            MonitorSetting.Sharpness => Safe(() => SetVCPFeature(handle, VcpSharpness, value)),
            MonitorSetting.Volume => Safe(() => SetVCPFeature(handle, VcpVolume, value)),
            _ => false,
        };

        // ---- enumeration -----------------------------------------------------------------

        private static IEnumerable<(string DeviceName, IntPtr Handle)> EnumerateMonitors()
        {
            var found = new List<(string, IntPtr)>();

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
            {
                var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
                if (GetMonitorInfo(hMonitor, ref info))
                    found.Add((info.szDevice, hMonitor));
                return true;
            }, IntPtr.Zero);

            return found;
        }

        private static IEnumerable<(IntPtr Handle, string Description)> PhysicalMonitors(IntPtr hMonitor)
        {
            var list = new List<(IntPtr, string)>();
            try
            {
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
                    return list;

                var monitors = new PHYSICAL_MONITOR[count];
                if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors))
                    return list;

                foreach (var m in monitors)
                    list.Add((m.hPhysicalMonitor, m.szPhysicalMonitorDescription ?? ""));
            }
            catch
            {
                // A machine without dxva2 support, or a driver that refuses. Nothing to offer.
            }
            return list;
        }

        private static bool Safe(Func<bool> call)
        {
            try { return call(); } catch { return false; }
        }

        private static bool TryGetMonitorBrightness(IntPtr h, out int min, out int cur, out int max)
        {
            min = cur = max = 0;
            try
            {
                if (!GetMonitorBrightness(h, out uint a, out uint b, out uint c)) return false;
                min = (int)a; cur = (int)b; max = (int)c;
                return true;
            }
            catch { return false; }
        }

        private static bool TryGetMonitorContrast(IntPtr h, out int min, out int cur, out int max)
        {
            min = cur = max = 0;
            try
            {
                if (!GetMonitorContrast(h, out uint a, out uint b, out uint c)) return false;
                min = (int)a; cur = (int)b; max = (int)c;
                return true;
            }
            catch { return false; }
        }

        private static bool TryGetGain(IntPtr h, uint index, out int min, out int cur, out int max)
        {
            min = cur = max = 0;
            try
            {
                if (!GetMonitorRedGreenOrBlueGain(h, index, out uint a, out uint b, out uint c))
                    return false;
                min = (int)a; cur = (int)b; max = (int)c;
                return true;
            }
            catch { return false; }
        }

        private static bool TryGetVcp(IntPtr h, byte code, out int current, out int max)
        {
            current = max = 0;
            try
            {
                if (!GetVCPFeatureAndVCPFeatureReply(h, code, IntPtr.Zero, out uint cur, out uint mx))
                    return false;
                current = (int)cur; max = (int)mx;
                return true;
            }
            catch { return false; }
        }

        // ---- interop ---------------------------------------------------------------------

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr rect, IntPtr data);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint count);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint count,
            [Out] PHYSICAL_MONITOR[] monitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetMonitorBrightness(IntPtr h, out uint min, out uint cur, out uint max);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool SetMonitorBrightness(IntPtr h, uint value);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetMonitorContrast(IntPtr h, out uint min, out uint cur, out uint max);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool SetMonitorContrast(IntPtr h, uint value);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetMonitorRedGreenOrBlueGain(IntPtr h, uint index,
            out uint min, out uint cur, out uint max);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool SetMonitorRedGreenOrBlueGain(IntPtr h, uint index, uint value);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr h, byte code,
            IntPtr type, out uint current, out uint max);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool SetVCPFeature(IntPtr h, byte code, uint value);
    }
}
