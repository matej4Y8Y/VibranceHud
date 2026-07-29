using System;
using System.Collections.Generic;

namespace VibranceHud
{
    /// <summary>
    /// Why DX11 init failed (or "None" if it succeeded). Used by the Settings
    /// page to surface an actionable reason instead of a silent fallback.
    ///
    /// Categories are designed around what the USER can do, not what the OS
    /// is complaining about internally:
    ///  - DriverIssue: usually "update GPU driver" or "close other 3D apps"
    ///  - NoOutputs:   usually "plug in your display" or "Windows isn't ready"
    ///  - OutOfMemory: usually "close a game / browser / heavy app"
    ///  - SdkIssue:    usually "install DirectX End-User Runtime"
    ///  - Unknown:     safe fallback - never say "your PC is broken" without evidence
    /// </summary>
    public enum DxInitFailureKind
    {
        None,
        NoCompatibleAdapter,
        DeviceCreationFailed,
        NoOutputs,
        DriverIssue,
        SdkIssue,
        OutOfMemory,
        Unknown,
    }

    /// <summary>
    /// Pure mapping from a SharpDX HRESULT to a categorised failure kind +
    /// a short human-readable label and a one-sentence user-facing hint. Testable
    /// without instantiating any DxDevice / DirectX runtime.
    /// </summary>
    public static class DxInitFailureMapper
    {
        /// <summary>Return kind, short label, and a one-sentence hint for the user.</summary>
        public static (DxInitFailureKind Kind, string Short, string Hint) Map(int hresult)
        {
            // DirectX / DXGI error codes we care about. See:
            //   https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/dxgi-error
            switch (hresult)
            {
                case unchecked((int)0x887A0002): // DXGI_ERROR_NOT_FOUND
                    return (DxInitFailureKind.NoOutputs,
                        "No display found",
                        "Make sure your monitor is plugged in and Windows is fully loaded before opening PlexusX.");
                case unchecked((int)0x887A0004): // DXGI_ERROR_UNSUPPORTED
                    return (DxInitFailureKind.DriverIssue,
                        "Display driver doesn't support DX11",
                        "Update your GPU driver from the manufacturer's website (nvidia.com / amd.com / intel.com).");
                case unchecked((int)0x887A0005): // DXGI_ERROR_DEVICE_REMOVED
                    return (DxInitFailureKind.DriverIssue,
                        "GPU was reset",
                        "Your GPU driver restarted. Click Retry - this often works the second time.");
                case unchecked((int)0x887A0006): // DXGI_ERROR_DEVICE_RESET
                    return (DxInitFailureKind.DriverIssue,
                        "GPU recovered from an error",
                        "Your GPU recovered from a hang. Click Retry.");
                case unchecked((int)0x887A0020): // DXGI_ERROR_DRIVER_INTERNAL_ERROR
                    return (DxInitFailureKind.DriverIssue,
                        "GPU driver crashed",
                        "Reinstall or update your GPU driver.");
                case unchecked((int)0x8007000E): // E_OUTOFMEMORY
                    return (DxInitFailureKind.OutOfMemory,
                        "Not enough GPU memory",
                        "Close other 3D apps (game, browser with hardware accel, recording software) and try again.");
                case unchecked((int)0x887A0027): // DXGI_ERROR_SDK_COMPONENT_MISSING (rare on modern Win10/11)
                    return (DxInitFailureKind.SdkIssue,
                        "DX11 runtime missing",
                        "Install the DirectX End-User Runtime from microsoft.com (search 'DirectX End-User Runtime').");
                default:
                    return (DxInitFailureKind.Unknown,
                        "DX11 init failed",
                        $"Restart PlexusX to retry. If it keeps failing, report the code 0x{hresult:X} in a bug report.");
            }
        }

        /// <summary>Same mapping but takes a SharpDX exception (convenience for the
        /// caller - we don't have to remember to cast the HRESULT ourselves).</summary>
        public static (DxInitFailureKind Kind, string Short, string Hint) Map(SharpDX.SharpDXException ex) =>
            Map(ex?.HResult ?? 0);

        /// <summary>For the rare case we caught a non-SharpDX exception (e.g.
        /// ArgumentException from a bad adapter description). The message is the
        /// only useful diagnostic we have.</summary>
        public static (DxInitFailureKind Kind, string Short, string Hint) MapGeneric(Exception ex)
        {
            if (ex == null) return (DxInitFailureKind.Unknown, "DX11 init failed", "Restart PlexusX to retry.");
            // SharpDX sometimes wraps the COM HRESULT inside an inner exception -
            // drill down one level before giving up.
            var inner = ex.InnerException as SharpDX.SharpDXException;
            if (inner != null) return Map(inner);
            return (DxInitFailureKind.Unknown, "DX11 init failed", $"Restart PlexusX to retry. ({ex.GetType().Name})");
        }

        /// <summary>Look up the user-facing hint for a categorised kind, used by
        /// the Settings page where the original HRESULT is no longer available
        /// (we only persisted the kind, not the code). Maps each known kind to
        /// a representative HRESULT from <see cref="Map"/> and returns the
        /// hint string.</summary>
        public static string HintForKind(DxInitFailureKind kind)
        {
            int representative;
            switch (kind)
            {
                case DxInitFailureKind.NoOutputs:        representative = unchecked((int)0x887A0002); break;
                case DxInitFailureKind.DriverIssue:      representative = unchecked((int)0x887A0004); break;
                case DxInitFailureKind.SdkIssue:         representative = unchecked((int)0x887A0027); break;
                case DxInitFailureKind.OutOfMemory:      representative = unchecked((int)0x8007000E); break;
                case DxInitFailureKind.NoCompatibleAdapter:
                case DxInitFailureKind.DeviceCreationFailed:
                case DxInitFailureKind.Unknown:
                default:
                    representative = unchecked((int)0x80004005); // E_FAIL
                    break;
            }
            return Map(representative).Hint;
        }
    }
}