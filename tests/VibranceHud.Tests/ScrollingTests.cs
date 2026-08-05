using System.Drawing;
using System.Windows.Forms;
using VibranceHud.Pages;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Scrolling still works with the native scrollbars hidden.
    ///
    /// The bars were hidden because they are the one part of the app that cannot be themed -
    /// a flat grey-and-white strip down the side of a dark glass panel. Hiding them must not
    /// take the scrolling with it: a page taller than its window with no way to reach the
    /// bottom is far worse than an ugly scrollbar.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class ScrollingTests
    {
        /// <summary>A page with more content than fits, so it genuinely needs to scroll.</summary>
        private sealed class TallPage : GlowPage
        {
            public TallPage()
            {
                AutoScroll = true;
                Size = new Size(400, 200);

                Controls.Add(new Label
                {
                    Text = "content",
                    Location = new Point(0, 0),
                    Size = new Size(300, 900),
                });

                AutoScrollMinSize = new Size(0, 900);
            }
        }

        /// <summary>
        /// The wheel, not the property.
        ///
        /// The original tests here set AutoScrollPosition directly, which works whether or
        /// not a scrollbar is showing - so they passed while the wheel was completely dead.
        /// ScrollableControl checks VScroll before acting on the wheel, and hiding the native
        /// bars makes that false. Setting a property is not the same as using the control.
        /// </summary>
        [Fact]
        public void TheMouseWheelActuallyScrolls()
        {
            Theme.Apply("Violet");
            using var page = new TallPage();
            page.CreateControl();

            page.TestScrollWheel(-120);   // one notch down

            Assert.True(page.AutoScrollPosition.Y < 0,
                "the wheel did nothing - hiding the scrollbars killed wheel scrolling");
        }

        [Fact]
        public void TheWheelScrollsBackUpAgain()
        {
            Theme.Apply("Violet");
            using var page = new TallPage();
            page.CreateControl();

            page.TestScrollWheel(-120);
            page.TestScrollWheel(-120);
            int down = -page.AutoScrollPosition.Y;

            page.TestScrollWheel(120);

            Assert.True(-page.AutoScrollPosition.Y < down, "scrolling up did nothing");
        }

        [Fact]
        public void TheWheelStopsAtTheTopAndBottom()
        {
            Theme.Apply("Violet");
            using var page = new TallPage();
            page.CreateControl();

            for (int i = 0; i < 60; i++) page.TestScrollWheel(-120);
            int bottom = -page.AutoScrollPosition.Y;
            Assert.True(bottom > 0 && bottom <= 900, $"ran past the content to {bottom}px");

            for (int i = 0; i < 60; i++) page.TestScrollWheel(120);
            Assert.Equal(0, page.AutoScrollPosition.Y);
        }

        /// <summary>
        /// The wheel has to work with the cursor over a child control.
        ///
        /// This is why Display would not scroll at all: Windows sends the wheel to whatever
        /// is under the cursor, and on these pages that is almost always a card or a slider.
        /// None of them scroll, none of them passed it on, so the page only ever saw the
        /// wheel in the gaps between controls - and on Display the card covers everything
        /// worth pointing at.
        /// </summary>
        [Fact]
        public void TheWheelWorksWhenTheCursorIsOverAChildControl()
        {
            Theme.Apply("Violet");
            using var page = new TallPage();
            page.CreateControl();

            var child = page.Controls.Cast<Control>().First();
            child.CreateControl();

            // Exactly what Windows does: raise the wheel on the control under the cursor.
            typeof(Control)
                .GetMethod("OnMouseWheel", System.Reflection.BindingFlags.Instance
                                         | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(child, new object[]
                {
                    new MouseEventArgs(MouseButtons.None, 0, 0, 0, -120)
                });

            Assert.True(page.AutoScrollPosition.Y < 0,
                "the wheel over a child did nothing - the page never received it");
        }

        [Fact]
        public void AWheelNotchMovesAUsefulDistance()
        {
            Theme.Apply("Violet");
            using var page = new TallPage();
            page.CreateControl();

            page.TestScrollWheel(-120);
            int moved = -page.AutoScrollPosition.Y;

            // The first attempt moved 18px a notch, which read as the page being stuck.
            Assert.True(moved >= 40, $"one notch moved only {moved}px - that feels stuck");
            Assert.True(moved <= 200, $"one notch moved {moved}px - that overshoots");
        }

        [Fact]
        public void TheWheelDoesNothingOnAPageThatFits()
        {
            Theme.Apply("Violet");
            using var page = new ShortPage();
            page.CreateControl();

            page.TestScrollWheel(-120);

            Assert.Equal(0, page.AutoScrollPosition.Y);
        }

        [Fact]
        public void APageTallerThanItsWindowCanStillScroll()
        {
            Theme.Apply("Violet");
            using var page = new TallPage();
            page.CreateControl();

            page.AutoScrollPosition = new Point(0, 300);

            // AutoScrollPosition reads back negative - that is WinForms' convention, not a bug.
            Assert.True(page.AutoScrollPosition.Y < 0,
                "the page did not scroll; hiding the bars took the scrolling with them");
        }

        [Fact]
        public void ScrollingReachesTheBottomOfTheContent()
        {
            Theme.Apply("Violet");
            using var page = new TallPage();
            page.CreateControl();

            page.AutoScrollPosition = new Point(0, 10_000);

            // Clamped to the real extent rather than refused outright.
            int scrolled = -page.AutoScrollPosition.Y;
            Assert.True(scrolled > 400,
                $"only reached {scrolled}px into 900px of content - the bottom is unreachable");
        }

        [Fact]
        public void ScrollingBackToTheTopWorks()
        {
            Theme.Apply("Violet");
            using var page = new TallPage();
            page.CreateControl();

            page.AutoScrollPosition = new Point(0, 300);
            page.AutoScrollPosition = new Point(0, 0);

            Assert.Equal(0, page.AutoScrollPosition.Y);
        }

        /// <summary>A page whose content fits should not pretend it can scroll.</summary>
        private sealed class ShortPage : GlowPage
        {
            public ShortPage()
            {
                AutoScroll = true;
                Size = new Size(400, 400);
            }
        }

        [Fact]
        public void AShortPageDoesNotScroll()
        {
            Theme.Apply("Violet");
            using var page = new ShortPage();
            page.CreateControl();

            page.AutoScrollPosition = new Point(0, 200);

            Assert.Equal(0, page.AutoScrollPosition.Y);
        }
    }
}
