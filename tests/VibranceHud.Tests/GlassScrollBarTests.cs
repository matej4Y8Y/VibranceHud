using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
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
        /// No page may contain a scrollbar.
        ///
        /// This is the rule the bug broke. A scrollbar inside the container it scrolls makes
        /// WinForms scroll that container to bring the moved thumb into view, so scrolling
        /// down and refusing to come back up was the thumb dragging the page after it. The bar
        /// belongs to the shell, outside every scroll region.
        /// </summary>
        [Theory]
        [InlineData("Display")]
        [InlineData("Monitor")]
        [InlineData("Crosshair")]
        [InlineData("Settings")]
        [InlineData("Panel")]
        [InlineData("Legal")]
        [InlineData("Account")]
        public void NoPageContainsItsOwnScrollBar(string page)
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "PxBar_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);

            using var built = RenderHarness.BuildPageForTest(page, dir);
            built.Size = new Size(830, 628);
            built.CreateControl();

            Assert.Empty(Descendants(built).OfType<GlassScrollBar>());
        }

        /// <summary>Every page still reports what a bar needs to draw itself.</summary>
        [Fact]
        public void ATallPageReportsSomethingToScroll()
        {
            using var page = RenderHarness.BuildDisplay();
            page.Size = new Size(830, 628);
            page.CreateControl();

            Assert.True(page.ScrollExtent > page.ScrollViewport,
                $"extent {page.ScrollExtent} does not exceed viewport {page.ScrollViewport}");
        }

        /// <summary>
        /// Down, then all the way back up.
        ///
        /// The reported symptom exactly: scrolling down worked and scrolling up did not,
        /// because every thumb reposition pulled the page back down again.
        /// </summary>
        [Fact]
        public void ScrollingDownThenUpReturnsToTheTop()
        {
            using var page = RenderHarness.BuildDisplay();
            page.Size = new Size(830, 628);
            page.CreateControl();

            for (int i = 0; i < 20; i++) page.TestScrollWheel(-120);
            int bottom = page.ScrollOffset;
            Assert.True(bottom > 0, "the page never scrolled down at all");

            for (int i = 0; i < 40; i++) page.TestScrollWheel(120);

            Assert.Equal(0, page.ScrollOffset);
        }

        /// <summary>And the card has to come back with it, not just the reported offset.</summary>
        [Fact]
        public void TheCardReturnsToTheTopToo()
        {
            using var page = RenderHarness.BuildDisplay();
            page.Size = new Size(830, 628);
            page.CreateControl();

            var card = page.Controls.OfType<CardPanel>().First();
            int top = card.Top;

            for (int i = 0; i < 20; i++) page.TestScrollWheel(-120);
            for (int i = 0; i < 40; i++) page.TestScrollWheel(120);

            Assert.Equal(top, card.Top);
        }

        /// <summary>The shell can drive the page directly, which is what the bar does.</summary>
        [Fact]
        public void ScrollToMovesThePage()
        {
            using var page = RenderHarness.BuildDisplay();
            page.Size = new Size(830, 628);
            page.CreateControl();

            page.ScrollTo(300);
            Assert.Equal(300, page.ScrollOffset);

            page.ScrollTo(0);
            Assert.Equal(0, page.ScrollOffset);
        }

        /// <summary>
        /// Only ONE scrollbar may be visible.
        ///
        /// Ours is drawn and Windows' is hidden - but moving our bar is itself a layout
        /// change, and Windows puts its own back whenever the non-client area is recalculated.
        /// Hiding it before that ran left the two side by side, one of them the flat grey strip
        /// the whole exercise existed to remove.
        /// </summary>
        [Fact]
        public void TheNativeScrollBarStaysHiddenAfterTheGlassOneMoves()
        {
            using var page = RenderHarness.BuildDisplay();
            page.Size = new Size(830, 628);
            page.CreateControl();

            // Force the sequence that re-showed it: layout, scroll, layout again.
            page.PerformLayout();
            for (int i = 0; i < 5; i++) page.TestScrollWheel(-120);
            page.PerformLayout();

            Assert.False(NativeVerticalBarVisible(page),
                "Windows' own scrollbar is showing next to ours");
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(System.IntPtr hWnd, int nIndex);

        private const int GWL_STYLE = -16;
        private const int WS_VSCROLL = 0x00200000;

        private static bool NativeVerticalBarVisible(Control c) =>
            c.IsHandleCreated && (GetWindowLong(c.Handle, GWL_STYLE) & WS_VSCROLL) != 0;

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
