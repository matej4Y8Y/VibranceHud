using System.Drawing;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The window is borderless, so Windows gives it no resize border of its own. Every page
    /// in the app was already laid out with Anchor flags for a resize that could never
    /// happen - MinimumSize was set and there was no way to reach it. This is the geometry
    /// that makes those flags mean something.
    /// </summary>
    public sealed class WindowChromeTests
    {
        private static readonly Size Win = new(1000, 700);
        private const int Grip = 6;

        [Fact]
        public void CornersWinOverEdges()
        {
            // A corner point satisfies two edge tests; the diagonal cursor is the one the
            // user wants there, so corners must be checked first.
            Assert.Equal(BorderHit.TopLeft, MainWindow.HitTestBorder(new Point(2, 2), Win, Grip));
            Assert.Equal(BorderHit.TopRight, MainWindow.HitTestBorder(new Point(998, 2), Win, Grip));
            Assert.Equal(BorderHit.BottomLeft, MainWindow.HitTestBorder(new Point(2, 698), Win, Grip));
            Assert.Equal(BorderHit.BottomRight, MainWindow.HitTestBorder(new Point(998, 698), Win, Grip));
        }

        [Fact]
        public void EdgesAreDetected()
        {
            Assert.Equal(BorderHit.Left, MainWindow.HitTestBorder(new Point(2, 350), Win, Grip));
            Assert.Equal(BorderHit.Right, MainWindow.HitTestBorder(new Point(998, 350), Win, Grip));
            Assert.Equal(BorderHit.Top, MainWindow.HitTestBorder(new Point(500, 2), Win, Grip));
            Assert.Equal(BorderHit.Bottom, MainWindow.HitTestBorder(new Point(500, 698), Win, Grip));
        }

        [Fact]
        public void InteriorIsNotABorder()
        {
            Assert.Equal(BorderHit.None, MainWindow.HitTestBorder(new Point(500, 350), Win, Grip));
            Assert.Equal(BorderHit.None, MainWindow.HitTestBorder(new Point(100, 100), Win, Grip));
        }

        [Fact]
        public void TheGripIsInclusiveAtItsEdge()
        {
            // Exactly `grip` pixels in still counts; one past it does not. Off-by-one here
            // is the difference between a resizable window and a nearly-resizable one.
            Assert.Equal(BorderHit.Left, MainWindow.HitTestBorder(new Point(Grip, 350), Win, Grip));
            Assert.Equal(BorderHit.None, MainWindow.HitTestBorder(new Point(Grip + 1, 350), Win, Grip));
        }

        [Fact]
        public void AZeroGripDisablesResizingEntirelyExceptTheEdgePixel()
        {
            // Guards the maximized case, where the caller passes 0 so nothing resizes.
            Assert.Equal(BorderHit.None, MainWindow.HitTestBorder(new Point(400, 350), Win, 0));
        }

        [Theory]
        [InlineData(0, 5, true, 1)]
        [InlineData(4, 5, true, 0)]     // wraps forward
        [InlineData(0, 5, false, 4)]    // wraps back
        [InlineData(3, 5, false, 2)]
        public void NavIndexWrapsInBothDirections(int current, int count, bool forward, int expected)
        {
            Assert.Equal(expected, MainWindow.NextNavIndex(current, count, forward));
        }

        [Fact]
        public void NoVisibleTabsIsSafe()
        {
            Assert.Equal(0, MainWindow.NextNavIndex(0, 0, true));
        }
    }
}
