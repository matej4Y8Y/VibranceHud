using System.Drawing;
using System.Linq;
using VibranceHud.Crosshair;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// All crosshair maths lives here, away from any window, because this is where a
    /// crosshair actually goes wrong: an arm a pixel longer than its opposite, or a shape
    /// half a pixel off centre, is invisible in code review and obvious in a firefight.
    ///
    /// Every shape is built around the origin, so the window only has to translate.
    /// </summary>
    public class CrosshairGeometryTests
    {
        private static CrosshairConfig Cfg(CrosshairShape shape = CrosshairShape.Cross,
            int size = 8, int thickness = 2, int gap = 4, bool dot = false) => new()
            {
                Shape = shape,
                Size = size,
                Thickness = thickness,
                Gap = gap,
                CentreDot = dot
            };

        [Fact]
        public void Cross_HasFourArms()
        {
            var g = CrosshairGeometry.Build(Cfg());

            Assert.Equal(4, g.Bars.Count);
            Assert.Null(g.Circle);
        }

        [Fact]
        public void T_DropsTheTopArm()
        {
            var cross = CrosshairGeometry.Build(Cfg());
            var t = CrosshairGeometry.Build(Cfg(CrosshairShape.T));

            Assert.Equal(3, t.Bars.Count);
            // The one missing is the arm above centre.
            Assert.Contains(cross.Bars, b => b.Bottom <= 0);
            Assert.DoesNotContain(t.Bars, b => b.Bottom <= 0);
        }

        [Fact]
        public void Dot_IsASingleMarkAtTheCentre()
        {
            var g = CrosshairGeometry.Build(Cfg(CrosshairShape.Dot, thickness: 4));

            var only = Assert.Single(g.Bars);
            Assert.Equal(0f, only.X + only.Width / 2, 3);
            Assert.Equal(0f, only.Y + only.Height / 2, 3);
            Assert.Equal(4f, only.Width, 3);
        }

        [Fact]
        public void Circle_HasNoArms_AndIsCentred()
        {
            var g = CrosshairGeometry.Build(Cfg(CrosshairShape.Circle, size: 6, gap: 5));

            Assert.Empty(g.Bars);
            var c = Assert.NotNull(g.Circle) is var _ ? g.Circle!.Value : default;
            Assert.Equal(0f, c.X + c.Width / 2, 3);
            Assert.Equal(0f, c.Y + c.Height / 2, 3);
            // Radius is gap + size, so the diameter is twice that.
            Assert.Equal(22f, c.Width, 3);
        }

        [Fact]
        public void Gap_IsTheDistanceFromCentreToTheInnerEndOfEachArm()
        {
            var g = CrosshairGeometry.Build(Cfg(gap: 6, size: 10));

            var right = g.Bars.Single(b => b.X > 0);
            var left = g.Bars.Single(b => b.Right < 0);
            var below = g.Bars.Single(b => b.Y > 0);

            Assert.Equal(6f, right.X, 3);
            Assert.Equal(-6f, left.Right, 3);
            Assert.Equal(6f, below.Y, 3);
        }

        [Fact]
        public void OpposingArms_AreTheSameLength()
        {
            var g = CrosshairGeometry.Build(Cfg(size: 9, gap: 3));

            var right = g.Bars.Single(b => b.X > 0);
            var left = g.Bars.Single(b => b.Right < 0);
            var above = g.Bars.Single(b => b.Bottom <= 0);
            var below = g.Bars.Single(b => b.Y > 0);

            Assert.Equal(right.Width, left.Width, 3);
            Assert.Equal(above.Height, below.Height, 3);
            Assert.Equal(9f, right.Width, 3);
        }

        [Fact]
        public void Thickness_SetsTheNarrowSideOfEveryArm()
        {
            var g = CrosshairGeometry.Build(Cfg(thickness: 5));

            foreach (var bar in g.Bars)
                Assert.Equal(5f, System.Math.Min(bar.Width, bar.Height), 3);
        }

        [Fact]
        public void CentreDot_AddsOneMark_OnlyWhenEnabled()
        {
            var without = CrosshairGeometry.Build(Cfg(dot: false));
            var with = CrosshairGeometry.Build(Cfg(dot: true));

            Assert.Equal(without.Bars.Count + 1, with.Bars.Count);
            Assert.Contains(with.Bars, b => b.X < 0 && b.Right > 0 && b.Y < 0 && b.Bottom > 0);
        }

        [Fact]
        public void Dot_ShapeIgnoresTheCentreDotToggle()
        {
            // It is already only a dot - the toggle must not double it up.
            var g = CrosshairGeometry.Build(Cfg(CrosshairShape.Dot, dot: true));

            Assert.Single(g.Bars);
        }

        [Theory]
        [InlineData(1)]  // odd thickness
        [InlineData(2)]  // even thickness
        [InlineData(7)]
        public void EveryShape_StaysSymmetricAboutTheCentre(int thickness)
        {
            // The classic off-by-one: an even-width arm rounded the wrong way sits a pixel
            // left of centre and the whole crosshair feels subtly wrong to aim with.
            var g = CrosshairGeometry.Build(Cfg(thickness: thickness));

            float minX = g.Bars.Min(b => b.X), maxX = g.Bars.Max(b => b.Right);
            float minY = g.Bars.Min(b => b.Y), maxY = g.Bars.Max(b => b.Bottom);

            Assert.Equal(-minX, maxX, 3);
            Assert.Equal(-minY, maxY, 3);
        }

        [Fact]
        public void Bounds_CoverEverythingDrawn()
        {
            var g = CrosshairGeometry.Build(Cfg(size: 10, gap: 4, thickness: 3));

            foreach (var bar in g.Bars)
            {
                Assert.True(bar.X >= g.Bounds.X && bar.Right <= g.Bounds.Right);
                Assert.True(bar.Y >= g.Bounds.Y && bar.Bottom <= g.Bounds.Bottom);
            }
        }

        [Fact]
        public void Bounds_GrowWithSizeAndGap()
        {
            var small = CrosshairGeometry.Build(Cfg(size: 4, gap: 2));
            var big = CrosshairGeometry.Build(Cfg(size: 14, gap: 9));

            Assert.True(big.Bounds.Width > small.Bounds.Width);
        }
    }
}
