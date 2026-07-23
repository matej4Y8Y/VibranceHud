using System.Linq;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class DisplayModesTests
    {
        // Shaped like a real monitor: the same resolution reported at several refresh rates.
        private static readonly DisplayMode[] Modes =
        {
            new(1920, 1080, 60), new(1920, 1080, 144), new(1920, 1080, 240),
            new(1440, 1080, 60), new(1440, 1080, 240),
            new(1280, 1024, 60), new(1280, 1024, 75),
            new(1600, 900, 120),
        };

        [Fact]
        public void BestPerResolution_KeepsTheHighestRefreshRate()
        {
            var best = DisplayModes.BestPerResolution(Modes);

            Assert.Equal(240, best.Single(m => m is { Width: 1920, Height: 1080 }).RefreshHz);
            Assert.Equal(240, best.Single(m => m is { Width: 1440, Height: 1080 }).RefreshHz);
            Assert.Equal(75, best.Single(m => m is { Width: 1280, Height: 1024 }).RefreshHz);
        }

        [Fact]
        public void BestPerResolution_ReturnsOneEntryPerResolution()
        {
            var best = DisplayModes.BestPerResolution(Modes);

            Assert.Equal(4, best.Count); // 1920x1080, 1600x900, 1440x1080, 1280x1024
        }

        [Fact]
        public void BestPerResolution_SortsWidestFirst()
        {
            var best = DisplayModes.BestPerResolution(Modes);

            Assert.Equal(new DisplayMode(1920, 1080, 240), best[0]);
            Assert.Equal(1600, best[1].Width);
            Assert.Equal(1440, best[2].Width);
            Assert.Equal(1280, best[3].Width);
        }

        [Fact]
        public void BestPerResolution_IgnoresGarbageModes()
        {
            var best = DisplayModes.BestPerResolution(new DisplayMode[]
            {
                new(0, 0, 60), new(1920, 0, 60), new(1920, 1080, 60)
            });

            Assert.Single(best);
        }

        [Fact]
        public void MaxRefreshFor_FindsTheTopRateAtThatResolution()
        {
            Assert.Equal(240, DisplayModes.MaxRefreshFor(Modes, 1920, 1080));
            Assert.Equal(120, DisplayModes.MaxRefreshFor(Modes, 1600, 900));
        }

        [Fact]
        public void MaxRefreshFor_ReturnsZero_WhenTheMonitorCannotDoIt()
        {
            Assert.Equal(0, DisplayModes.MaxRefreshFor(Modes, 3840, 2160));
        }

        [Fact]
        public void IsSupported_GuardsAgainstUnlistedModes()
        {
            Assert.True(DisplayModes.IsSupported(Modes, 1440, 1080));
            Assert.False(DisplayModes.IsSupported(Modes, 1728, 1080)); // stretched, not reported
        }
    }
}
