using System.Linq;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Picking a refresh rate, and not silently changing somebody's.
    /// </summary>
    public sealed class RefreshRateTests
    {
        private static readonly DisplayMode[] Panel =
        {
            new(1920, 1080, 240), new(1920, 1080, 144), new(1920, 1080, 120), new(1920, 1080, 60),
            new(2560, 1440, 165), new(2560, 1440, 60),
            new(1280, 1024, 75),
        };

        [Fact]
        public void RatesComeBackHighestFirst()
        {
            var rates = DisplayModes.RefreshRatesFor(Panel, 1920, 1080);
            Assert.Equal(new[] { 240, 144, 120, 60 }, rates);
        }

        [Fact]
        public void RatesAreScopedToTheResolutionAsked()
        {
            Assert.Equal(new[] { 165, 60 }, DisplayModes.RefreshRatesFor(Panel, 2560, 1440));
            Assert.Equal(new[] { 75 }, DisplayModes.RefreshRatesFor(Panel, 1280, 1024));
        }

        [Fact]
        public void AnUnsupportedResolutionHasNoRates()
        {
            Assert.Empty(DisplayModes.RefreshRatesFor(Panel, 3840, 2160));
        }

        [Fact]
        public void DuplicatesAreCollapsed()
        {
            var withDupes = Panel.Concat(new[] { new DisplayMode(1920, 1080, 240) });
            Assert.Equal(4, DisplayModes.RefreshRatesFor(withDupes, 1920, 1080).Count);
        }

        [Fact]
        public void ZeroRatesAreIgnored()
        {
            var odd = Panel.Concat(new[] { new DisplayMode(1920, 1080, 0) });
            Assert.DoesNotContain(0, DisplayModes.RefreshRatesFor(odd, 1920, 1080));
        }

        // ---- exact-mode support -----------------------------------------------------

        [Fact]
        public void AnExactModeIsOnlySupportedIfItWasReported()
        {
            Assert.True(DisplayModes.IsSupported(Panel, 1920, 1080, 144));

            // Right resolution, rate the monitor never offered at it.
            Assert.False(DisplayModes.IsSupported(Panel, 1920, 1080, 165));
            // Right rate, wrong resolution.
            Assert.False(DisplayModes.IsSupported(Panel, 2560, 1440, 240));
        }

        /// <summary>
        /// The regression behind restoring the rate as well as the resolution: Restore used to
        /// go through the resolution-only path, which reapplies at the monitor's maximum. A
        /// user running 120Hz on a 240Hz panel was silently put back to 240 after every game
        /// and had to fix it in Windows each time.
        /// </summary>
        [Fact]
        public void TheMaximumIsNotTheOnlyRateAPanelOffers()
        {
            int max = DisplayModes.MaxRefreshFor(Panel, 1920, 1080);
            var all = DisplayModes.RefreshRatesFor(Panel, 1920, 1080);

            Assert.Equal(240, max);
            Assert.True(all.Count > 1,
                "if a panel only ever had one rate, restoring at the maximum would be harmless");
            Assert.Contains(120, all);
        }
    }
}
