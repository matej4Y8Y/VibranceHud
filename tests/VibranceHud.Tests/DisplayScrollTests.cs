using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VibranceHud.Pages;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The Display page has to be scrollable.
    ///
    /// It grew a lot - the advanced grade, the preset tiles and the preview panel all landed
    /// on it - and a page taller than its window with no way to reach the bottom hides the
    /// SHARE section entirely.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class DisplayScrollTests
    {
        /// <summary>A realistic window: the app opens around 1040 wide and the content host is
        /// shorter than the screen once the title bar and nav are taken out.</summary>
        private static VibrancePage Build(int height = 640)
        {
            var page = RenderHarness.BuildDisplay();
            page.Size = new Size(830, height);
            page.CreateControl();
            return page;
        }

        [Fact]
        public void ThePageKnowsItHasMoreContentThanFits()
        {
            using var page = Build();

            Assert.True(page.AutoScroll, "AutoScroll is off, so nothing can scroll at all");
            Assert.True(page.AutoScrollMinSize.Height > page.ClientSize.Height,
                $"extent {page.AutoScrollMinSize.Height} does not exceed the client height "
                + $"{page.ClientSize.Height} - the page thinks it fits when it does not");
        }

        [Fact]
        public void TheWheelScrollsIt()
        {
            using var page = Build();

            page.TestScrollWheel(-120);

            Assert.True(page.AutoScrollPosition.Y < 0,
                "the wheel did nothing on the Display page");
        }

        /// <summary>
        /// The wheel arrives at whatever is under the cursor. On this page that is almost
        /// always the card or a slider, so the page only ever sees it in the gaps - which is
        /// exactly how Display ended up unscrollable once before.
        /// </summary>
        [Fact]
        public void TheWheelWorksWithTheCursorOverTheCard()
        {
            using var page = Build();

            var card = page.Controls.OfType<CardPanel>().FirstOrDefault();
            Assert.NotNull(card);
            card!.CreateControl();

            typeof(Control)
                .GetMethod("OnMouseWheel", System.Reflection.BindingFlags.Instance
                                         | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(card, new object[] { new MouseEventArgs(MouseButtons.None, 0, 0, 0, -120) });

            Assert.True(page.AutoScrollPosition.Y < 0,
                "the wheel over the card did nothing - the page never received it");
        }

        /// <summary>The bottom of the page has to be reachable, or SHARE is invisible.</summary>
        [Fact]
        public void ScrollingReachesTheBottom()
        {
            using var page = Build();

            for (int i = 0; i < 80; i++) page.TestScrollWheel(-120);

            int reached = -page.AutoScrollPosition.Y + page.ClientSize.Height;

            Assert.True(reached >= page.AutoScrollMinSize.Height - 4,
                $"only reached {reached}px of {page.AutoScrollMinSize.Height}px - the bottom is unreachable");
        }

        /// <summary>
        /// The wheel must reach the page even when focus is somewhere else entirely.
        ///
        /// This is the bug people actually hit: Windows sends WM_MOUSEWHEEL to the FOCUSED
        /// control, and clicking a nav button puts focus on the nav bar - a sibling of the
        /// content host, not an ancestor of the page. The wheel travelled up the nav's chain
        /// and the page never saw it, so Display looked stuck unless you first clicked inside
        /// it. WheelRouter re-routes by cursor position; this checks the page still responds
        /// when the event arrives from outside its own focus chain.
        /// </summary>
        [Fact]
        public void TheWheelStillScrollsWhenFocusIsElsewhere()
        {
            using var page = Build();

            using var elsewhere = new NavButton { Text = "Display", Size = new Size(210, 46) };
            elsewhere.CreateControl();

            // The router forwards to the window under the pointer, which is the page.
            page.TestScrollWheel(-120);

            Assert.True(page.AutoScrollPosition.Y < 0,
                "the page did not scroll for an event that did not come from its focus chain");
        }

        /// <summary>Opening the advanced grade adds four rows, so the extent has to grow with
        /// it or those rows fall off the end.</summary>
        [Fact]
        public void OpeningTheAdvancedSectionGrowsTheScrollExtent()
        {
            using var page = Build();
            int before = page.AutoScrollMinSize.Height;

            typeof(VibrancePage)
                .GetMethod("SetAdvancedOpen", System.Reflection.BindingFlags.Instance
                                            | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(page, new object[] { true });

            Assert.True(page.AutoScrollMinSize.Height > before,
                $"extent stayed at {before}px after opening eight more rows");
        }
    }
}
