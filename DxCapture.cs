using System;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Device = SharpDX.Direct3D11.Device;

namespace VibranceHud
{
    /// <summary>
    /// DXGI Desktop Duplication wrapper for a single output. Captures the current desktop
    /// frame into a GPU texture that can be bound as a shader resource. <see cref="TryCapture"/>
    /// returns false on timeout (session locked, fullscreen exclusive app foreground, UAC
    /// prompt active, etc.) so the caller can present the last frame unchanged - visible
    /// stutter is preferable to no saturation.
    /// </summary>
    internal sealed class DxCapture : IDisposable
    {
        private readonly OutputDuplication _duplication;
        private readonly Texture2D _frameTex;
        private readonly ShaderResourceView _srv;
        private readonly Device _device;

        public DxCapture(Device device, Output1 output)
        {
            _device = device;
            _duplication = output.DuplicateOutput(device);

            RawRectangle bounds = output.Description.DesktopBounds;
            int width = bounds.Right - bounds.Left;
            int height = bounds.Bottom - bounds.Top;

            // Default-usage texture bound as a shader resource - this is what the pixel
            // shader samples. (A Staging texture, as in the plan sketch, is CPU-read-only
            // and cannot be bound to the pipeline, so it can't feed the shader.)
            _frameTex = new Texture2D(device, new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                CpuAccessFlags = CpuAccessFlags.None,
                BindFlags = BindFlags.ShaderResource,
                OptionFlags = ResourceOptionFlags.None,
            });
            _srv = new ShaderResourceView(device, _frameTex);
        }

        /// <summary>Capture one frame. Returns true on success; false on timeout.</summary>
        public bool TryCapture()
        {
            SharpDX.DXGI.Resource? frame = null;
            try
            {
                var result = _duplication.TryAcquireNextFrame(0, out var info, out frame);
                if (result.Failure)
                {
                    return false; // timeout or transient - present last frame
                }
                if (info.LastPresentTime == 0)
                {
                    // Only the mouse moved (no new desktop image); keep the last frame.
                    return false;
                }
                using (var desktopImage = frame.QueryInterface<Texture2D>())
                {
                    _device.ImmediateContext.CopyResource(desktopImage, _frameTex);
                }
                return true;
            }
            catch (SharpDX.SharpDXException)
            {
                return false;
            }
            finally
            {
                frame?.Dispose();
                try { _duplication.ReleaseFrame(); } catch { /* nothing to release */ }
            }
        }

        public Texture2D Frame => _frameTex;
        public ShaderResourceView FrameView => _srv;

        public void Dispose()
        {
            _srv.Dispose();
            _frameTex.Dispose();
            _duplication.Dispose();
        }
    }
}
