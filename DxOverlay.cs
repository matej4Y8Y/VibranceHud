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

        // The categorised reason DX11 couldn't be used. Stored in fields rather than read
        // through _device, because every failure path disposes the device and sets it to
        // null - so a computed property would report None precisely when a reason exists,
        // which is the whole thing the Settings page needs. Set once during construction.
        public DxInitFailureKind LastFailure { get; private set; } = DxInitFailureKind.None;
        public string LastFailureMessage { get; private set; } = "";

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

        private const int ActiveSleepMs = 16;   // ~60 Hz while actually saturating
        private const int IdleSleepMs = 250;    // coarse poll while suspended at identity

        /// <summary>One monitor we can actually render: its swap-chain target plus the shader
        /// and desktop-duplication capture built against that target's own device.
        ///
        /// These used to be three index-parallel lists (_shaders[i] / _captures[i] /
        /// Targets[i]). Grouping them removes the requirement that all three stay the same
        /// length, which is what made it impossible to skip a single broken monitor without
        /// silently pairing one monitor's capture with another monitor's swap-chain.</summary>
        private sealed class ActiveOutput
        {
            public DxDevice.OutputTarget Target = null!;
            public DxShader Shader = null!;
            public DxCapture Capture = null!;
        }

        private readonly DxDevice _device;
        private readonly List<ActiveOutput> _active;
        private readonly CancellationTokenSource _cts = null!;
        private readonly Task _renderLoop = null!;
        private readonly object _matrixLock = new object();

        private float[] _currentMatrix;
        private volatile bool _isRendering;

        public bool IsAvailable { get; }

        /// <summary>True while the render loop is actively capturing/drawing/presenting at
        /// ~60Hz. False while suspended at an identity matrix (nothing to saturate) - see
        /// <see cref="RenderLoop"/>.</summary>
        public bool IsRendering => _isRendering;

        public DxOverlay()
        {
            _active = new List<ActiveOutput>();
            _currentMatrix = Identity;

            _device = new DxDevice();
            if (!_device.IsAvailable)
            {
                IsAvailable = false;
                LastFailure = _device.LastFailure;
                LastFailureMessage = _device.LastFailureMessage;
                _device.Dispose();
                _device = null!;
                return;
            }

            // A shader (compiled against the target's own device) and a desktop-duplication
            // capture per monitor - a target on a secondary GPU adapter needs both created
            // against ITS device, not whichever adapter happened to be enumerated first.
            //
            // Built per-monitor and tolerant of individual failures. This used to be one
            // try/catch around the whole loop, so a single monitor that couldn't be
            // duplicated tore down every other monitor and dropped the user to the
            // Magnification fallback, which no capture tool can see. Desktop Duplication
            // failing for one output is ordinary: something else may already be duplicating
            // it (OBS "Display Capture" uses this same API, as do ShadowPlay and various
            // overlays), or it's a virtual/phantom display that can't be duplicated at all.
            Exception? firstFailure = null;
            TolerantOutputBuilder.Build(_device.Targets.Count,
                i =>
                {
                    var target = _device.Targets[i];
                    DxShader? shader = null;
                    try
                    {
                        shader = new DxShader(target.Device, target.Device.ImmediateContext);
                        var capture = new DxCapture(target.Device, target.Output);
                        _active.Add(new ActiveOutput { Target = target, Shader = shader, Capture = capture });
                    }
                    catch
                    {
                        // Don't leak the half-built shader when the capture is what failed.
                        shader?.Dispose();
                        throw;
                    }
                },
                (i, ex) => firstFailure ??= ex);

            if (_active.Count == 0)
            {
                // Not one monitor could be rendered - this is the one case where falling back
                // to Magnification is the right answer.
                var (kind, label, _) = DxInitFailureMapper.MapGeneric(
                    firstFailure ?? new InvalidOperationException("No renderable display output"));
                LastFailure = kind;
                LastFailureMessage = label;
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
            foreach (var o in _active)
            {
                o.Capture.Dispose();
                o.Shader.Dispose();
            }
            _active.Clear();
            _device.Dispose();
        }

        private void RenderLoop(CancellationToken ct)
        {
            var clear = new RawColor4(0f, 0f, 0f, 0f); // transparent - only the quad writes color
            bool idle = false;

            while (!ct.IsCancellationRequested)
            {
                float[] matrix;
                lock (_matrixLock)
                {
                    matrix = _currentMatrix;
                }

                if (IsIdentityMatrix(matrix))
                {
                    // Nothing to saturate - capturing, shading and presenting every 16ms was
                    // pure wasted GPU/CPU work whenever vibrance/saturation/brightness/eye
                    // care were all at neutral (e.g. right after launch, or after Clear()).
                    // Suspend the loop, but present one fully-transparent frame per monitor
                    // first so the overlay window doesn't freeze on whatever it last drew -
                    // a suspended, non-topmost layered window otherwise keeps showing its
                    // last composited frame forever, which would look like a stuck screenshot
                    // pasted over the real (now-changing) desktop underneath.
                    if (!idle)
                    {
                        foreach (var target in _device.Targets)
                        {
                            var context = target.Device.ImmediateContext;
                            context.OutputMerger.SetRenderTargets(target.Rtv);
                            context.ClearRenderTargetView(target.Rtv, clear);
                            target.SwapChain.Present(1, AlphaPremultiplied);
                        }
                        idle = true;
                        _isRendering = false;
                    }
                    Thread.Sleep(IdleSleepMs);
                    continue;
                }

                idle = false;
                _isRendering = true;

                // Only the monitors that actually built. A monitor whose duplication failed
                // keeps the fully-transparent frame the idle branch presented, so it shows
                // the untouched desktop rather than a stale or garbage overlay.
                foreach (var o in _active)
                {
                    var target = o.Target;
                    var cap = o.Capture;
                    var shader = o.Shader;
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

                Thread.Sleep(ActiveSleepMs);
            }
        }

        private static bool IsIdentityMatrix(float[] m)
        {
            if (ReferenceEquals(m, Identity)) return true;
            for (int i = 0; i < 25; i++)
            {
                if (Math.Abs(m[i] - Identity[i]) > 0.0001f) return false;
            }
            return true;
        }
    }
}
