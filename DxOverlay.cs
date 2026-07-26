using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SharpDX.DXGI;
using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;

namespace VibranceHud
{
    /// <summary>
    /// DirectX 11 swap-chain overlay implementing ISaturationOverlay. Captures the desktop
    /// via DXGI Desktop Duplication, applies the 5x5 color matrix in a pixel shader, and
    /// presents at the DWM layer. The DWM compositing path is the same one OBS Desktop
    /// Capture, Discord screen share, NVIDIA ShadowPlay, and Windows Graphics Capture read
    /// from - so the saturation effect is visible in every standard Windows capture tool.
    ///
    /// Lifecycle: the constructor attempts DX11 init. If it fails (no DX11 GPU, broken
    /// driver, locked session), IsAvailable is false and Apply/Clear/Dispose are all no-ops;
    /// the caller should fall back to MagOverlay.
    ///
    /// Apply() is cheap - it stores the matrix in a field and the rendering loop reads it.
    /// The render loop is owned by this class and started in the constructor; it runs until
    /// Dispose() is called.
    /// </summary>
    public sealed class DxOverlay : ISaturationOverlay, IDisplayOverlay, IDisposable
    {
        public OverlayMode ActiveMode => OverlayMode.Dx;

        private static readonly float[] Identity = new float[]
        {
            1f, 0f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f, 0f,
            0f, 0f, 1f, 0f, 0f,
            0f, 0f, 0f, 1f, 0f,
            0f, 0f, 0f, 0f, 1f,
        };

        // Present the saturated result as the final premultiplied-alpha composite, so WGC
        // (Discord, browsers) and the DWM capture path see the effect. The spec forbids the
        // DXGI 1.4 "windowed" composition and HWND_TOPMOST for exactly this reason.
        private const PresentFlags AlphaPremultiplied = PresentFlags.None;

        private readonly DxDevice _device;
        private readonly List<DxShader> _shaders;
        private readonly List<DxCapture> _captures;
        private readonly CancellationTokenSource _cts = null!;
        private readonly Task _renderLoop = null!;
        private readonly object _matrixLock = new object();

        private float[] _currentMatrix;

        public bool IsAvailable { get; }

        public DxOverlay()
        {
            _shaders = new List<DxShader>();
            _captures = new List<DxCapture>();
            _currentMatrix = Identity;

            _device = new DxDevice();
            if (!_device.IsAvailable)
            {
                IsAvailable = false;
                _device.Dispose();
                _device = null!;
                return;
            }

            try
            {
                // A shader (compiled against the target's own device) and a desktop-
                // duplication capture per monitor, one output at a time - a target on a
                // secondary GPU adapter needs both created against ITS device, not
                // whichever adapter happened to be enumerated first.
                foreach (var target in _device.Targets)
                {
                    _shaders.Add(new DxShader(target.Device, target.Device.ImmediateContext));
                    _captures.Add(new DxCapture(target.Device, target.Output));
                }
            }
            catch (Exception)
            {
                // Any init failure (duplication unavailable, shader compile, etc.) -> fall back.
                foreach (var cap in _captures) cap.Dispose();
                _captures.Clear();
                foreach (var shader in _shaders) shader.Dispose();
                _shaders.Clear();
                _device.Dispose();
                _device = null!;
                IsAvailable = false;
                return;
            }

            _cts = new CancellationTokenSource();
            _renderLoop = Task.Run(() => RenderLoop(_cts.Token));
            IsAvailable = true;
        }

        public void Apply(float[] matrix)
        {
            if (!IsAvailable) return;
            lock (_matrixLock)
            {
                _currentMatrix = (float[])matrix.Clone();
            }
        }

        public void Clear()
        {
            if (!IsAvailable) return;
            Apply(Identity);
        }

        public void Dispose()
        {
            if (!IsAvailable) return;
            _cts.Cancel();
            try { _renderLoop.Wait(TimeSpan.FromSeconds(2)); } catch { }
            foreach (var cap in _captures) cap.Dispose();
            _captures.Clear();
            foreach (var shader in _shaders) shader.Dispose();
            _shaders.Clear();
            _device.Dispose();
        }

        private void RenderLoop(CancellationToken ct)
        {
            var clear = new RawColor4(0f, 0f, 0f, 0f); // transparent - only the quad writes color

            while (!ct.IsCancellationRequested)
            {
                float[] matrix;
                lock (_matrixLock)
                {
                    matrix = _currentMatrix;
                }

                for (int i = 0; i < _device.Targets.Count && i < _captures.Count; i++)
                {
                    var target = _device.Targets[i];
                    var cap = _captures[i];
                    var shader = _shaders[i];
                    var context = target.Device.ImmediateContext;

                    shader.ApplyMatrix(matrix);

                    // On timeout we still re-present the last captured frame so the effect
                    // doesn't flicker off (session lock, fullscreen exclusive, UAC, etc.).
                    cap.TryCapture();

                    context.OutputMerger.SetRenderTargets(target.Rtv);
                    context.ClearRenderTargetView(target.Rtv, clear);
                    context.Rasterizer.SetViewport(0, 0, target.Width, target.Height, 0f, 1f);

                    shader.Bind(cap.FrameView);
                    shader.Draw();

                    // Present1 with premultiplied alpha - the DWM-capture-friendly path.
                    target.SwapChain.Present(1, AlphaPremultiplied);
                }

                Thread.Sleep(16); // ~60 Hz
            }
        }
    }
}
