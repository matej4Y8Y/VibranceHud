using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class OverlayModeResolverTests
    {
        private sealed class FakeOverlay : ISaturationOverlay, IDisplayOverlay
        {
            public OverlayMode ActiveMode { get; set; }
            public void Apply(float[] matrix) { }
            public void Clear() { }
        }

        private sealed class ReportlessOverlay : ISaturationOverlay
        {
            public void Apply(float[] matrix) { }
            public void Clear() { }
        }

        [Fact]
        public void Resolve_ReportsDx_WhenDxOverlayIsActive()
        {
            var fake = new FakeOverlay { ActiveMode = OverlayMode.Dx };
            Assert.Equal(OverlayMode.Dx, OverlayModeResolver.Resolve(fake));
        }

        [Fact]
        public void Resolve_ReportsMag_WhenDx11FailedAndMagIsTheFallback()
        {
            // Simulates TryCreateOverlay() having fallen back after DX11 init failure -
            // this is the case that used to be silent (BUG2).
            var fake = new FakeOverlay { ActiveMode = OverlayMode.Mag };
            Assert.Equal(OverlayMode.Mag, OverlayModeResolver.Resolve(fake));
        }

        [Fact]
        public void Resolve_DefaultsToDx_WhenOverlayDoesNotReportAMode()
        {
            Assert.Equal(OverlayMode.Dx, OverlayModeResolver.Resolve(new ReportlessOverlay()));
        }

        [Fact]
        public void RealDxOverlay_ReportsDxAsItsIdentity()
        {
            using var overlay = new DxOverlay();
            Assert.Equal(OverlayMode.Dx, overlay.ActiveMode);
        }

        [Fact]
        public void RealMagOverlay_ReportsMagAsItsIdentity()
        {
            using var overlay = new MagOverlay();
            Assert.Equal(OverlayMode.Mag, overlay.ActiveMode);
        }
    }
}
