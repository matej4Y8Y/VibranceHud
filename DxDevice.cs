using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Device = SharpDX.Direct3D11.Device;

namespace VibranceHud
{
    /// <summary>
    /// Owns one DX11 device per GPU adapter and one swap-chain per monitor across every
    /// adapter. Lifecycle is create-once, dispose-once; per-frame work happens in
    /// DxCapture + DxShader driven by DxOverlay.
    ///
    /// A multi-GPU PC (a laptop with integrated + discrete graphics, or a desktop with two
    /// cards) can have monitors attached to different adapters, and DXGI Desktop Duplication
    /// requires a device created on the SAME adapter as the output it duplicates - so each
    /// adapter gets its own device, and every one of its outputs shares that device.
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
        /// DXGI output (for desktop duplication), the device that owns it, and the pixel
        /// dimensions.</summary>
        public sealed class OutputTarget
        {
            public SwapChain1 SwapChain = null!;
            public RenderTargetView Rtv = null!;
            public Output1 Output = null!;
            public Device Device = null!;
            public IntPtr Hwnd;
            public int Width;
            public int Height;
        }

        public List<OutputTarget> Targets { get; }

        private Factory2? _factory;

        // One device per adapter that produced at least one usable target; tracked
        // separately from OutputTarget so a multi-monitor adapter's shared device is
        // disposed exactly once instead of once per output.
        private readonly List<Device> _devices = new();

        public bool IsAvailable => Targets.Count > 0;

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
            Targets = new List<OutputTarget>();

            try
            {
                _factory = new Factory2();
                EnsureWindowClass();

                for (int i = 0; ; i++)
                {
                    Adapter1 adapter;
                    try
                    {
                        adapter = _factory.GetAdapter1(i);
                    }
                    catch (SharpDXException)
                    {
                        break; // no more adapters
                    }

                    using (adapter)
                    {
                        Device device;
                        try
                        {
                            // BgraSupport is required for a flip-model swap-chain with alpha
                            // compositing.
                            device = new Device(adapter, DeviceCreationFlags.BgraSupport);
                        }
                        catch (Exception)
                        {
                            continue; // this adapter has no usable D3D11 driver - skip it
                        }

                        int before = Targets.Count;
                        CreateSwapChainsForOutputs(adapter, device);

                        if (Targets.Count > before) _devices.Add(device);
                        else device.Dispose(); // adapter had no usable output
                    }
                }

                if (Targets.Count == 0)
                {
                    // No usable output on any adapter - treat as unavailable so the caller
                    // falls back to MagOverlay.
                    Dispose();
                }
            }
            catch (Exception)
            {
                // DX11 init failure - the caller checks IsAvailable and falls back to MagOverlay.
                Dispose();
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

        private void CreateSwapChainsForOutputs(Adapter1 adapter, Device device)
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

                var swapChain = new SwapChain1(_factory, device, hwnd, ref desc);

                using (var backBuffer = swapChain.GetBackBuffer<Texture2D>(0))
                {
                    var rtv = new RenderTargetView(device, backBuffer);
                    Targets.Add(new OutputTarget
                    {
                        SwapChain = swapChain,
                        Rtv = rtv,
                        Output = output1,
                        Device = device,
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

            foreach (var hwnd in _windows)
            {
                if (hwnd != IntPtr.Zero) DestroyWindow(hwnd);
            }
            _windows.Clear();

            foreach (var device in _devices) device.Dispose();
            _devices.Clear();

            _factory?.Dispose();
        }
    }
}
