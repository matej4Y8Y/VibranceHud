using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Device = SharpDX.Direct3D11.Device;

namespace VibranceHud
{
    /// <summary>
    /// Owns the DX11 device and one swap-chain per monitor. Lifecycle is create-once,
    /// dispose-once; per-frame work happens in DxCapture + DxShader driven by DxOverlay.
    ///
    /// Each monitor gets a borderless, click-through overlay window (NOT topmost - the
    /// spec forbids HWND_TOPMOST because it breaks DWM capture for the layered window
    /// group) sized to the output's desktop bounds. A flip-model SwapChain1 with
    /// premultiplied alpha (DXGI_ALPHA_MODE_PREMULTIPLIED) is bound to that window and
    /// composited at the DWM layer, which is exactly where OBS Desktop Capture, Discord
    /// screen share, NVIDIA ShadowPlay, and Windows Graphics Capture read from.
    /// </summary>
    internal sealed class DxDevice : IDisposable
    {
        /// <summary>Per-monitor render target: the swap-chain, its back-buffer RTV, the
        /// DXGI output (for desktop duplication) and the pixel dimensions.</summary>
        public sealed class OutputTarget
        {
            public SwapChain1 SwapChain = null!;
            public RenderTargetView Rtv = null!;
            public Output1 Output = null!;
            public IntPtr Hwnd;
            public int Width;
            public int Height;
        }

        public Device? Device { get; private set; }
        public List<SwapChain1> SwapChains { get; }
        public List<OutputTarget> Targets { get; }

        private Factory2? _factory;

        public bool IsAvailable => Device != null && Targets.Count > 0;

        // --- Win32 overlay window plumbing -------------------------------------------------
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int SW_SHOWNOACTIVATE = 4;

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

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

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

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        // Keep the delegate alive for the lifetime of the process so the WndProc pointer
        // stays valid (otherwise the GC would collect it and Windows would call freed memory).
        private static readonly WndProc s_wndProc = DefWindowProc;
        private static bool s_classRegistered;
        private const string OverlayClass = "PlexusXDxOverlayWindow";

        private readonly List<IntPtr> _windows = new();

        public DxDevice()
        {
            SwapChains = new List<SwapChain1>();
            Targets = new List<OutputTarget>();

            try
            {
                _factory = new Factory2();

                using var adapter = _factory.GetAdapter1(0);
                // BgraSupport is required for a flip-model swap-chain with alpha compositing.
                Device = new Device(adapter, DeviceCreationFlags.BgraSupport);

                EnsureWindowClass();
                CreateSwapChainsForOutputs(adapter);

                if (Targets.Count == 0)
                {
                    // No usable output - treat as unavailable so the caller falls back.
                    Dispose();
                }
            }
            catch (Exception)
            {
                // DX11 init failure - the caller checks IsAvailable and falls back to MagOverlay.
                Dispose();
                Device = null;
            }
        }

        private static void EnsureWindowClass()
        {
            if (s_classRegistered) return;
            var wc = new WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<WNDCLASSEX>(),
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
                hInstance = GetModuleHandle(null),
                lpszClassName = OverlayClass,
            };
            RegisterClassEx(ref wc);
            s_classRegistered = true;
        }

        private void CreateSwapChainsForOutputs(Adapter1 adapter)
        {
            foreach (var output in adapter.Outputs)
            {
                Output1 output1;
                try
                {
                    output1 = output.QueryInterface<Output1>();
                }
                catch
                {
                    output.Dispose();
                    continue;
                }

                RawRectangle bounds = output1.Description.DesktopBounds;
                int width = bounds.Right - bounds.Left;
                int height = bounds.Bottom - bounds.Top;
                if (width <= 0 || height <= 0)
                {
                    output1.Dispose();
                    output.Dispose();
                    continue;
                }

                // Borderless, non-activating, click-through overlay window covering this
                // monitor. Deliberately NOT WS_EX_TOPMOST (spec: topmost breaks DWM capture).
                IntPtr hwnd = CreateWindowEx(
                    WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT,
                    OverlayClass, "PlexusX Overlay",
                    WS_POPUP | WS_VISIBLE,
                    bounds.Left, bounds.Top, width, height,
                    IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);

                if (hwnd == IntPtr.Zero)
                {
                    output1.Dispose();
                    output.Dispose();
                    continue;
                }
                _windows.Add(hwnd);
                ShowWindow(hwnd, SW_SHOWNOACTIVATE);

                var desc = new SwapChainDescription1
                {
                    Width = width,
                    Height = height,
                    Format = Format.B8G8R8A8_UNorm,
                    Stereo = false,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = Usage.RenderTargetOutput,
                    BufferCount = 2,
                    Scaling = Scaling.Stretch,
                    // Flip model is mandatory for a premultiplied-alpha swap-chain.
                    SwapEffect = SwapEffect.FlipSequential,
                    AlphaMode = AlphaMode.Premultiplied,
                    Flags = SwapChainFlags.None,
                };

                var swapChain = new SwapChain1(_factory, Device, hwnd, ref desc);

                using (var backBuffer = swapChain.GetBackBuffer<Texture2D>(0))
                {
                    var rtv = new RenderTargetView(Device, backBuffer);
                    SwapChains.Add(swapChain);
                    Targets.Add(new OutputTarget
                    {
                        SwapChain = swapChain,
                        Rtv = rtv,
                        Output = output1,
                        Hwnd = hwnd,
                        Width = width,
                        Height = height,
                    });
                }

                output.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var t in Targets)
            {
                t.Rtv?.Dispose();
                t.SwapChain?.Dispose();
                t.Output?.Dispose();
            }
            Targets.Clear();
            SwapChains.Clear();

            foreach (var hwnd in _windows)
            {
                if (hwnd != IntPtr.Zero) DestroyWindow(hwnd);
            }
            _windows.Clear();

            Device?.Dispose();
            _factory?.Dispose();
        }
    }
}
