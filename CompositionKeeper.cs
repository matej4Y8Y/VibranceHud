using System;
using System.Runtime.InteropServices;

namespace VibranceHud
{
    /// <summary>
    /// Keeps Windows compositing the desktop, so the colour effect ends up in what screen
    /// capture sees.
    ///
    /// Windows presents a fullscreen window one of two ways. Under "Composed Flip" DWM builds
    /// the desktop image and everything lands in it, which is what DXGI Desktop Duplication and
    /// Windows.Graphics.Capture read - so OBS, Discord screen share and Medal all see it. Under
    /// "Independent Flip" the content is handed straight to display scanout, bypassing DWM
    /// entirely; the user sees it on their monitor and every capture tool misses it.
    ///
    /// Which path a machine takes depends on GPU, driver and display configuration, not on
    /// anything the app does - which is exactly why "colours show up in my screen share" worked
    /// for some users and not others on an identical build. It looked random; it was the
    /// composition path.
    ///
    /// The lever: a permanently topmost window makes the fullscreen window ineligible for
    /// Independent Flip, so DWM has to compose. One 1x1 pixel is enough. Same trick
    /// ForceComposedFlip uses to get frame-generated frames into Sunshine captures
    /// (https://github.com/fernandoenzo/ForceComposedFlip), which also documents the
    /// heavier alternative - HKLM\SOFTWARE\Microsoft\Windows\Dwm OverlayTestMode=5 to disable
    /// Multiplane Overlay. Deliberately NOT doing that: it needs admin, changes behaviour
    /// system-wide for every application, and persists after PlexusX exits. This window costs
    /// one pixel and disappears when the process does.
    ///
    /// Topmost has to be reasserted periodically - anything else going topmost (a game, an
    /// overlay, a notification) pushes us down, and once we're not on top the optimisation can
    /// kick back in.
    /// </summary>
    public sealed class CompositionKeeper : IDisposable
    {
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_VISIBLE = 0x10000000;

        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TRANSPARENT = 0x00000020;   // click-through
        private const int WS_EX_TOOLWINDOW = 0x00000080;    // no taskbar entry, no alt-tab
        private const int WS_EX_NOACTIVATE = 0x08000000;    // never takes focus
        private const int WS_EX_LAYERED = 0x00080000;       // lets us set alpha

        private const int LWA_ALPHA = 0x00000002;
        private const int SW_SHOWNOACTIVATE = 4;

        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        /// <summary>How often topmost is reasserted. Matches ForceComposedFlip; frequent enough
        /// to win back the top spot quickly, rare enough to be free.</summary>
        private const int ReassertIntervalMs = 500;

        [StructLayout(LayoutKind.Sequential)]
        private struct WNDCLASSEX
        {
            public int cbSize;
            public int style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
            public IntPtr hIconSm;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        // Held for the process lifetime so the WndProc pointer stays valid - if the GC took the
        // delegate, Windows would call freed memory.
        private static readonly WndProcDelegate s_wndProc = DefWindowProc;
        private static bool s_classRegistered;
        private const string KeeperClass = "PlexusXCompositionKeeper";

        private IntPtr _hwnd;
        private System.Windows.Forms.Timer? _reassert;
        private bool _disposed;

        /// <summary>True when the pixel is up and holding topmost.</summary>
        public bool IsActive => _hwnd != IntPtr.Zero;

        public CompositionKeeper()
        {
            try
            {
                EnsureClass();

                _hwnd = CreateWindowEx(
                    WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW
                        | WS_EX_NOACTIVATE | WS_EX_LAYERED,
                    KeeperClass, "PlexusX Composition Keeper",
                    WS_POPUP | WS_VISIBLE,
                    0, 0, 1, 1,
                    IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);

                if (_hwnd == IntPtr.Zero) return;

                // Alpha 1, not 0: it has to actually take part in composition to defeat the
                // optimisation, and a fully transparent window can be skipped. One pixel at
                // 1/255 is not perceptible.
                SetLayeredWindowAttributes(_hwnd, 0, 1, LWA_ALPHA);
                ShowWindow(_hwnd, SW_SHOWNOACTIVATE);
                Reassert();

                _reassert = new System.Windows.Forms.Timer { Interval = ReassertIntervalMs };
                _reassert.Tick += (s, e) => Reassert();
                _reassert.Start();
            }
            catch
            {
                // Best-effort: this is an optimisation defeat, not a feature. If it can't be
                // created the app runs exactly as it did before.
                Dispose();
            }
        }

        private void Reassert()
        {
            if (_hwnd == IntPtr.Zero) return;
            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 1, 1,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        private static void EnsureClass()
        {
            if (s_classRegistered) return;
            var wc = new WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<WNDCLASSEX>(),
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
                hInstance = GetModuleHandle(null),
                lpszClassName = KeeperClass,
            };
            RegisterClassEx(ref wc);
            s_classRegistered = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_reassert != null)
            {
                // Stop before Dispose - a queued WM_TIMER is still dispatched otherwise, and
                // it would run Reassert against a destroyed window.
                _reassert.Stop();
                _reassert.Dispose();
                _reassert = null;
            }

            if (_hwnd != IntPtr.Zero)
            {
                try { DestroyWindow(_hwnd); } catch { /* nothing useful to do at teardown */ }
                _hwnd = IntPtr.Zero;
            }
        }
    }
}
