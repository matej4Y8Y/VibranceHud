using System;
using System.Runtime.InteropServices;

namespace VibranceHud.Audio
{
    /// <summary>
    /// The real speakers, via the Windows Core Audio API. Reads the output peak meter and
    /// drives the master volume on the default playback device - the same public API the
    /// Windows volume mixer itself uses. Nothing here touches any game.
    /// </summary>
    public sealed class WindowsAudioOutput : IAudioOutput, IDisposable
    {
        private const int ERender = 0;      // playback (not recording)
        private const int EConsole = 0;     // the default "games and system sounds" role
        private const int ClsCtxAll = 23;

        private static Guid _meterIid = new("C02216F6-8C67-4B5B-9D00-D008E73E0064");
        private static Guid _volumeIid = new("5CDF2C82-841E-4546-9722-0CF74078229A");
        private static Guid _noEvent = Guid.Empty;

        private readonly IAudioMeterInformation _meter;
        private readonly IAudioEndpointVolume _volume;

        public WindowsAudioOutput()
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(ERender, EConsole, out var device));

            Marshal.ThrowExceptionForHR(device.Activate(ref _meterIid, ClsCtxAll, IntPtr.Zero, out var meter));
            Marshal.ThrowExceptionForHR(device.Activate(ref _volumeIid, ClsCtxAll, IntPtr.Zero, out var volume));

            _meter = (IAudioMeterInformation)meter;
            _volume = (IAudioEndpointVolume)volume;
        }

        public float Peak
        {
            get
            {
                try { return _meter.GetPeakValue(out var peak) == 0 ? peak : 0f; }
                catch { return 0f; }
            }
        }

        public float Volume
        {
            get
            {
                try { return _volume.GetMasterVolumeLevelScalar(out var v) == 0 ? v : 1f; }
                catch { return 1f; }
            }
            set
            {
                try { _volume.SetMasterVolumeLevelScalar(Math.Clamp(value, 0f, 1f), ref _noEvent); }
                catch { /* device vanished (unplugged headset) - nothing to do */ }
            }
        }

        public void Dispose()
        {
            if (Marshal.IsComObject(_meter)) Marshal.ReleaseComObject(_meter);
            if (Marshal.IsComObject(_volume)) Marshal.ReleaseComObject(_volume);
        }

        // ---- Core Audio COM plumbing (declared only as far as the methods we call) ----

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
                [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        }

        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioMeterInformation
        {
            [PreserveSig] int GetPeakValue(out float peak);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            // Declared in vtable order - only the last two are actually called.
            [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int GetChannelCount(out uint count);
            [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid eventContext);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
            [PreserveSig] int GetMasterVolumeLevel(out float level);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        }
    }
}
