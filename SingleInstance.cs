using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace VibranceHud
{
    /// <summary>
    /// Keeps PlexusX to one running copy per user session.
    ///
    /// Without this, double-clicking the desktop icon while PlexusX was already
    /// sitting in the tray started a second full instance, which is broken in
    /// several independent ways at once:
    ///   - two activation dialogs, both writing license.json (last writer wins,
    ///     and a half-written file reads back as Tampered),
    ///   - two fullscreen colour overlays fighting over the same display, so the
    ///     saturation/vibrance the user sees is whichever instance wrote last,
    ///   - the second instance's RegisterHotKey silently fails, so the global
    ///     hotkey appears to "randomly stop working",
    ///   - two auto-updaters able to launch the installer simultaneously.
    ///
    /// The second copy hands off to the first (which un-hides its window) and exits,
    /// which is what users expect from a tray app anyway.
    /// </summary>
    internal static class SingleInstance
    {
        // Session-scoped (no "Global\" prefix): two different Windows users each get
        // their own PlexusX, which is correct - the license and settings are per-user.
        private const string MutexName = "PlexusX.SingleInstance.v1";

        private static Mutex? _mutex;

        /// <summary>Broadcast by a second copy to ask the running one to show itself.
        /// Registered messages are unique process-wide and safe to broadcast, unlike
        /// picking a raw WM_USER+n that another app might also be using.</summary>
        public static readonly int ShowWindowMessage = RegisterWindowMessage("PlexusX.ShowMainWindow");

        private static readonly IntPtr HwndBroadcast = new(0xFFFF);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>True when this process is the first copy and may continue starting.
        /// False means another copy already owns the session.</summary>
        public static bool TryAcquire()
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                _mutex.Dispose();
                _mutex = null;
            }
            return createdNew;
        }

        /// <summary>Ask the already-running copy to bring its window up.</summary>
        public static void SignalExistingInstance() =>
            PostMessage(HwndBroadcast, ShowWindowMessage, IntPtr.Zero, IntPtr.Zero);

        public static void Release()
        {
            if (_mutex == null) return;
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { /* not owned - process is exiting anyway */ }
            _mutex.Dispose();
            _mutex = null;
        }
    }
}
