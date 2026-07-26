using System;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class DxOverlayTests
    {
        [Fact(Skip = "Requires DX11 GPU; runs on user machine only")]
        public void DxOverlay_ApplyMatrix_RoundTripsToSwapChain()
        {
            using var overlay = new DxOverlay();
            if (!overlay.IsAvailable) return; // no DX11 GPU - silently skip

            // Identity matrix first - verify nothing breaks.
            overlay.Apply(new float[]
            {
                1, 0, 0, 0, 0,
                0, 1, 0, 0, 0,
                0, 0, 1, 0, 0,
                0, 0, 0, 1, 0,
                0, 0, 0, 0, 1,
            });
            // Allow one render frame.
            System.Threading.Thread.Sleep(100);
            overlay.Clear();

            // Full saturation: every output channel is the average of the input channels.
            // For input (1, 0, 0) -> output (0.2126, 0.7152, 0.0722) per Rec. 709.
            // (The render loop reads the desktop, so we cannot assert a specific pixel
            // value - this test only verifies Apply/Clear don't throw and the render
            // loop runs without crashing.)
        }
    }
}
