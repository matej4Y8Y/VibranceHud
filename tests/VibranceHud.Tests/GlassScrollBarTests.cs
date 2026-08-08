using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VibranceHud.Controls;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The app's own scrollbar, and the guarantee that every page has one.
    ///
    /// Hiding the Win32 bar left no indication that a page had more content, which meant a
    /// page that had silently stopped scrolling looked exactly like one with nothing below the
    /// fold. That is how Display stayed broken across several rounds of "it still doesn't
    /// scroll" - the symptom was invisible. A drawn bar makes the state impossible to hide.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class GlassScrollBarTests
    {
        private static GlassScrollBar Bar(int max = 1400, int viewport = 600)
        {
            Theme.Apply("Violet");
            return new GlassScrollBar { Size = new Size(10, 600), Maximum = max, Viewport = viewport };
        }

        [Fact]
        public void ABarWithNothingToScrollIsNotNeeded()
        {
            using var bar = Bar(max: 400, viewport: 600);
            Assert.False(bar.Needed);
        }

        [Fact]
        public void ABarWithMoreContentThanFitsIsNeeded()
        {
            using var bar = Bar();
            Assert.True(bar.Needed);
        }

        /// <summary>Value must never leave the scrollable span, or the thumb draws outside its
        /// own track and the page jumps somewhere it cannot be.</summary>
        [Fact]
        public void ValueIsClampedToTheScrollableSpan()
        {
            using var bar = Bar(max: 1400, viewport: 600);

            bar.Value = 99999;
            Assert.Equal(800, bar.Value);   // 1400 - 600

            bar.Value = -50;
            Assert.Equal(0, bar.Value);
        }

        /// <summary>
        /// Shrinking the content has to pull the value back with it. Otherwise collapsing the
        /// advanced section leaves the bar claiming a position past the end of the page.
        /// </summary>
        [Fact]
        public void ShrinkingTheContentPullsTheValueBack()
        {
            using var bar = Bar(max: 1400, viewport: 600);
            bar.Value = 800;

            bar.Maximum = 800;

            Assert.Equal(200, bar.Value);
        }

        [Fact]
        public void ItPaintsInEveryState()
        {
            Theme.Apply("Violet");

            foreach (var (max, viewport) in new[] { (1400, 600), (400, 600), (1, 1) })
            {
                using var bar = new GlassScrollBar { Size = new Size(10, 600), Maximum = max, Viewport = viewport };
                using var bmp = new Bitmap(10, 600);
                using var g = Graphics.FromImage(bmp);

                var onPaint = typeof(GlassScrollBar).GetMethod("OnPaint",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

                var ex = Record.Exception(() => onPaint.Invoke(bar,
                    new object[] { new PaintEventArgs(g, new Rectangle(0, 0, 10, 600)) }));

                Assert.Null(ex);
            }
        }

        // ---- every page gets one ----------------------------------------------------------

        /// <summary>
        /// The guarantee. A new page cannot be written without a scrollbar, because it comes
        /// from GlowPage rather than from each page remembering to add one.
        /// </summary>
        [Theory]
        [InlineData("Display")]
        [InlineData("Monitor")]
        [InlineData("Crosshair")]
        [InlineData("Settings")]
        [InlineData("Panel")]
        [InlineData("Legal")]
        [InlineData("Account")]
        public void EveryPageHasTheAppsOwnScrollBar(string page)
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "PxBar_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);

            using var built = RenderHarness.BuildPageForTest(page, dir);
            built.Size = new Size(830, 628);
            built.CreateControl();

            var bars = Descendants(built).OfType<GlassScrollBar>().ToList();

            // A page that does not scroll legitimately has none; one that does must show one.
            if (!built.AutoScroll) return;

            Assert.True(bars.Count == 1,
                $"{page} has {bars.Count} scrollbars - it should have exactly one, from GlowPage");
        }

        /// <summary>A page taller than its window must show the bar, not just own one.</summary>
        [Fact]
        public void ATallPageActuallyShowsTheBar()
        {
            using var page = RenderHarness.BuildDisplay();
            page.Size = new Size(830, 628);
            page.CreateControl();

            var bar = Descendants(page).OfType<GlassScrollBar>().Single();

            Assert.True(bar.Needed, "Display is 1400px tall in a 628px window and the bar says it is not needed");
            Assert.True(bar.Visible, "the bar exists but is hidden on a page that scrolls");
        }

        /// <summary>Scrolling with the wheel has to move the thumb, or the bar is decoration
        /// that lies about where the page is.</summary>
        [Fact]
        public void TheWheelMovesTheThumb()
        {
            using var page = RenderHarness.BuildDisplay();
            page.Size = new Size(830, 628);
            page.CreateControl();

            var bar = Descendants(page).OfType<GlassScrollBar>().Single();
            int before = bar.Value;

            for (int i = 0; i < 3; i++) page.TestScrollWheel(-120);

            Assert.True(bar.Value > before,
                $"the thumb stayed at {bar.Value} while the page scrolled");
        }

        private static System.Collections.Generic.IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }
    }
}
