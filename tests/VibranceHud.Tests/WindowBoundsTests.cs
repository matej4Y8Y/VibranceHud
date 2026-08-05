using System.Collections.Generic;
using System.Drawing;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class WindowBoundsTests
    {
        private static readonly List<Rectangle> OneScreen = new() { new Rectangle(0, 0, 1920, 1080) };

        private static readonly List<Rectangle> TwoScreens = new()
        {
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(1920, 0, 2560, 1440),
        };

        /// <summary>A monitor to the LEFT of the primary sits at negative coordinates. This is
        /// the arrangement that breaks naive validation.</summary>
        private static readonly List<Rectangle> NegativeLayout = new()
        {
            new Rectangle(-1920, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080),
        };

        [Fact]
        public void BoundsAlreadyVisibleAreUnchanged()
        {
            var saved = new Rectangle(100, 100, 1040, 680);
            Assert.Equal(saved, WindowBounds.ClampToVisible(saved, OneScreen));
        }

        [Fact]
        public void BoundsOnAMonitorLeftOfPrimaryAreKept()
        {
            var saved = new Rectangle(-1500, 120, 1040, 680);
            Assert.Equal(saved, WindowBounds.ClampToVisible(saved, NegativeLayout));
        }

        [Fact]
        public void BoundsOnAnUnpluggedMonitorComeBack()
        {
            var saved = new Rectangle(3000, 200, 1040, 680);
            var fixedUp = WindowBounds.ClampToVisible(saved, OneScreen);

            Assert.True(OneScreen[0].IntersectsWith(fixedUp), "window must land on a real screen");
            Assert.Equal(1040, fixedUp.Width);
            Assert.Equal(680, fixedUp.Height);
        }

        [Fact]
        public void AWindowBarelyPeekingOnScreenIsRecovered()
        {
            // 10px of a 1040px window visible at the right edge - technically intersecting,
            // useless in practice.
            var saved = new Rectangle(1910, 500, 1040, 680);
            var fixedUp = WindowBounds.ClampToVisible(saved, OneScreen);

            Assert.NotEqual(saved, fixedUp);
            Assert.True(fixedUp.X >= 0 && fixedUp.Right <= 1920);
        }

        [Fact]
        public void TheSecondMonitorIsUsedWhenTheWindowLivesThere()
        {
            var saved = new Rectangle(2200, 300, 1040, 680);
            Assert.Equal(saved, WindowBounds.ClampToVisible(saved, TwoScreens));
        }

        [Fact]
        public void AWindowLargerThanTheScreenIsShrunkToFit()
        {
            var fixedUp = WindowBounds.ClampToVisible(new Rectangle(0, 0, 4000, 3000), OneScreen);

            Assert.True(fixedUp.Width <= 1920);
            Assert.True(fixedUp.Height <= 1080);
        }

        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(10, 10, 0, 400)]
        [InlineData(10, 10, 400, 0)]
        [InlineData(10, 10, -5, -5)]
        public void DegenerateSavedBoundsAreRejected(int x, int y, int w, int h)
        {
            Assert.Equal(Rectangle.Empty,
                WindowBounds.ClampToVisible(new Rectangle(x, y, w, h), OneScreen));
        }

        [Fact]
        public void NoScreensAtAllIsSafe()
        {
            Assert.Equal(Rectangle.Empty,
                WindowBounds.ClampToVisible(new Rectangle(0, 0, 800, 600), new List<Rectangle>()));
            Assert.Equal(Rectangle.Empty,
                WindowBounds.ClampToVisible(new Rectangle(0, 0, 800, 600), null!));
        }
    }
}
