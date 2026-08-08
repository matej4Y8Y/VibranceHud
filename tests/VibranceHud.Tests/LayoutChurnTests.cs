using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VibranceHud.Pages;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Scrolling must not re-lay the page out.
    ///
    /// A live trace counted 285 full layout passes for 92 wheel notches - three redundant
    /// passes per notch, each repositioning forty-odd controls. Assigning AutoScrollMinSize
    /// raises a layout, which fires Resize, which lays out again, which assigns it again. That
    /// is why scrolling did not feel smooth, and it is invisible to every geometric assertion
    /// because the final positions were always correct.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class LayoutChurnTests
    {
        /// <summary>Counts how many times the page moves its own children.</summary>
        private static int CountLayouts(VibrancePage page, System.Action act)
        {
            int layouts = 0;
            var card = page.Controls.OfType<CardPanel>().First();

            void OnLayout(object? s, LayoutEventArgs e) => layouts++;
            card.Layout += OnLayout;
            try { act(); }
            finally { card.Layout -= OnLayout; }

            return layouts;
        }

        /// <summary>
        /// The card must actually MOVE when the page scrolls, and stay moved after a layout.
        ///
        /// This is the test that was missing. AutoScrollPosition changed correctly all along -
        /// every earlier test asserted on that and passed - while the card was being put
        /// straight back at the top, because LayoutContent wrote absolute client coordinates
        /// into a scrolled container. The page reported itself as scrolled and looked frozen.
        /// </summary>
        [Fact]
        public void ScrollingActuallyMovesTheCard()
        {
            using var page = RenderHarness.BuildDisplay();
            page.Size = new Size(830, 628);
            page.CreateControl();

            var card = page.Controls.OfType<CardPanel>().First();
            int before = card.Top;

            for (int i = 0; i < 5; i++) page.TestScrollWheel(-120);

            Assert.True(card.Top < before,
                $"the card stayed at {card.Top} - the page reports scrolling but nothing moved");
        }

        /// <summary>
        /// And it must survive a re-layout. A scroll that is undone by the next layout pass is
        /// exactly the bug, and it only shows up if something forces a layout afterwards.
        /// </summary>
        [Fact]
        public void AScrollSurvivesALaterLayout()
        {
            using var page = RenderHarness.BuildDisplay();
            page.Size = new Size(830, 628);
            page.CreateControl();

            var card = page.Controls.OfType<CardPanel>().First();

            for (int i = 0; i < 5; i++) page.TestScrollWheel(-120);
            int scrolled = card.Top;

            page.PerformLayout();

            Assert.Equal(scrolled, card.Top);
        }

        [Fact]
        public void ScrollingDoesNotRelayoutTheCard()
        {
            using var page = RenderHarness.BuildDisplay();
            page.Size = new Size(830, 628);
            page.CreateControl();

            // Settle first - the initial layout is legitimate.
            page.TestScrollWheel(-120);

            int layouts = CountLayouts(page, () =>
            {
                for (int i = 0; i < 10; i++) page.TestScrollWheel(-120);
            });

            Assert.True(layouts <= 2,
                $"ten wheel notches caused {layouts} card layouts - scrolling is re-laying the page out");
        }

        /// <summary>
        /// The extent must still be correct after the guard, or the fix trades smoothness for
        /// an unreachable bottom.
        /// </summary>
        [Fact]
        public void TheExtentIsStillRightAfterTheGuard()
        {
            using var page = RenderHarness.BuildDisplay();
            page.Size = new Size(830, 628);
            page.CreateControl();

            var card = page.Controls.OfType<CardPanel>().First();

            Assert.True(page.AutoScrollMinSize.Height >= card.Bottom,
                $"extent {page.AutoScrollMinSize.Height} does not cover the card ending at {card.Bottom}");
        }

        /// <summary>Resizing still re-lays out - the guard must not make the page ignore a
        /// genuine size change.</summary>
        [Fact]
        public void ResizingStillLaysOut()
        {
            using var page = RenderHarness.BuildDisplay();
            page.Size = new Size(830, 628);
            page.CreateControl();

            var card = page.Controls.OfType<CardPanel>().First();
            int before = card.Left;

            page.Size = new Size(1200, 628);

            Assert.NotEqual(before, card.Left);
        }
    }
}
