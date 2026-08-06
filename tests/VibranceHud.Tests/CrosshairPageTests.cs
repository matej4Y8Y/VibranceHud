using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using VibranceHud;
using VibranceHud.Controls;
using VibranceHud.Crosshair;
using VibranceHud.Pages;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The crosshair page as it is actually built.
    ///
    /// Driven through the real page rather than a mock, because the failures worth catching
    /// here are layout ones - a gallery whose cells land on top of the sliders, or a card too
    /// short to contain its own last row. A child cannot render past its immediate parent's
    /// bounds, so a card that is too short silently clips a row no matter how far the page
    /// can scroll.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class CrosshairPageTests
    {
        [Fact]
        public void TheGalleryShowsEveryCrosshair()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);

            var cells = Descendants(page).OfType<CrosshairCell>().ToList();
            Assert.Equal(CrosshairGallery.All.Count, cells.Count);
        }

        [Fact]
        public void EveryCellCarriesADistinctCrosshair()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);

            var ids = Descendants(page).OfType<CrosshairCell>().Select(c => c.Item.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        /// <summary>
        /// The card has to be tall enough for its own contents. This is the check that would
        /// have caught the gallery pushing SAVED off the bottom.
        /// </summary>
        [Fact]
        public void EveryRowFitsInsideTheCard()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);

            var card = Descendants(page).OfType<CardPanel>().First();

            foreach (Control child in card.Controls)
            {
                if (!child.Visible) continue;
                Assert.True(child.Bottom <= card.Height,
                    $"'{Describe(child)}' runs {child.Bottom - card.Height}px past the bottom of the card");
                Assert.True(child.Right <= card.Width + 2,
                    $"'{Describe(child)}' runs past the right edge of the card");
            }
        }

        [Fact]
        public void NothingOnTheCardOverlapsAnythingElse()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);

            var card = Descendants(page).OfType<CardPanel>().First();
            var kids = card.Controls.Cast<Control>()
                .Where(c => c.Visible && c.Width > 0 && c.Height > 0)
                .ToList();

            for (int i = 0; i < kids.Count; i++)
                for (int j = i + 1; j < kids.Count; j++)
                {
                    var a = kids[i].Bounds;
                    a.Intersect(kids[j].Bounds);
                    Assert.False(a.Width > 2 && a.Height > 2,
                        $"'{Describe(kids[i])}' overlaps '{Describe(kids[j])}'");
                }
        }

        /// <summary>The whole point of the gallery: a cell previews what the user would
        /// actually get, in their colour rather than the catalogue's white.</summary>
        [Fact]
        public void CellsPreviewInTheUsersOwnColour()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);

            var cells = Descendants(page).OfType<CrosshairCell>().ToList();
            Assert.All(cells, c => Assert.NotNull(c.PreviewStyle));
        }

        [Fact]
        public void FavouritesSortToTheTop()
        {
            using var temp = new TempDirectory();

            var settings = new AppSettings();
            // Something from the far end of the catalogue, so "first" cannot be a coincidence.
            var wanted = CrosshairGallery.All.Last();
            settings.FavouriteCrosshairs.Add(wanted.Id);

            using var page = BuildPage(temp.Path, settings);

            var first = Descendants(page).OfType<CrosshairCell>().First();
            Assert.Equal(wanted.Id, first.Item.Id);
            Assert.True(first.Favourite);
        }

        [Fact]
        public void WithNoFavouritesTheCatalogueOrderIsKept()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);

            var first = Descendants(page).OfType<CrosshairCell>().First();
            Assert.Equal(CrosshairGallery.All[0].Id, first.Item.Id);
        }

        [Fact]
        public void TheOpacitySliderIsOnThePage()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);

            var captions = Descendants(page).OfType<Label>()
                .Select(l => (l.Text ?? "").Replace(" ", ""))
                .ToList();

            Assert.Contains(captions, t => t.Contains("OPACITY", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The six swatches are shortcuts, not the whole palette - the wheel is what
        /// makes any colour reachable, which was the point of adding it.</summary>
        [Fact]
        public void TheColourWheelIsOnThePage()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);

            Assert.Single(Descendants(page).OfType<ColourWheel>());
        }

        [Fact]
        public void TheWheelStartsOnTheCrosshairsOwnColour()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);

            var wheel = Descendants(page).OfType<ColourWheel>().Single();
            var expected = Color.FromArgb(new CrosshairConfig().ColourArgb);

            Assert.InRange(wheel.Colour.R, expected.R - 1, expected.R + 1);
            Assert.InRange(wheel.Colour.G, expected.G - 1, expected.G + 1);
            Assert.InRange(wheel.Colour.B, expected.B - 1, expected.B + 1);
        }

        /// <summary>
        /// The two pickers have to agree. Before this, clicking a swatch changed the crosshair
        /// and left the wheel's marker sitting on the previous colour, so the page showed two
        /// different answers to "what colour is this crosshair".
        /// </summary>
        [Fact]
        public void ClickingASwatchMovesTheWheelToIt()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);

            var wheel = Descendants(page).OfType<ColourWheel>().Single();
            var swatches = Descendants(page)
                .Where(c => c.GetType().Name == "SwatchDot")
                .ToList();

            Assert.NotEmpty(swatches);

            foreach (var swatch in swatches)
            {
                var colour = (Color)swatch.GetType()
                    .GetProperty("Colour")!.GetValue(swatch)!;

                Click(swatch);

                Assert.InRange(wheel.Colour.R, colour.R - 1, colour.R + 1);
                Assert.InRange(wheel.Colour.G, colour.G - 1, colour.G + 1);
                Assert.InRange(wheel.Colour.B, colour.B - 1, colour.B + 1);
            }
        }

        /// <summary>
        /// The three colour controls have to agree at all times.
        ///
        /// Swatches, wheel and hex box all set the same value, and each has to move the other
        /// two. Getting this wrong does not throw - it leaves the page showing two different
        /// answers to "what colour is this crosshair", which only shows up by looking.
        /// </summary>
        [Fact]
        public void TypingAHexColourMovesTheWheelAndTheSwatches()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);

            var wheel = Descendants(page).OfType<ColourWheel>().Single();
            var hex = Descendants(page).OfType<TextBox>().Single();

            hex.Text = "FF3C3C";     // the red swatch

            Assert.InRange(wheel.Colour.R, 254, 255);
            Assert.InRange(wheel.Colour.G, 59, 61);
            Assert.InRange(wheel.Colour.B, 59, 61);

            var lit = Descendants(page)
                .Where(c => c.GetType().Name == "SwatchDot")
                .Where(c => (bool)c.GetType().GetProperty("Active")!.GetValue(c)!)
                .ToList();

            Assert.Single(lit);
        }

        [Fact]
        public void MovingTheWheelUpdatesTheHexBox()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);

            var wheel = Descendants(page).OfType<ColourWheel>().Single();
            var hex = Descendants(page).OfType<TextBox>().Single();

            wheel.TestPressKey(Keys.Right);

            Assert.Equal(ColourWheel.ToHex(wheel.Colour), hex.Text);
        }

        [Fact]
        public void ClickingASwatchUpdatesTheHexBox()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);

            var hex = Descendants(page).OfType<TextBox>().Single();
            var swatch = Descendants(page).First(c => c.GetType().Name == "SwatchDot"
                && (Color)c.GetType().GetProperty("Colour")!.GetValue(c)! == Color.White);

            Click(swatch);

            Assert.Equal("FFFFFF", hex.Text);
        }

        /// <summary>A half-typed hex must not be treated as a colour, or the crosshair would
        /// jump somewhere arbitrary between the first keystroke and the last.</summary>
        [Fact]
        public void AHalfTypedHexLeavesTheColourAlone()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);

            var wheel = Descendants(page).OfType<ColourWheel>().Single();
            var hex = Descendants(page).OfType<TextBox>().Single();

            var before = wheel.Colour;
            hex.Text = "FF3C";

            Assert.Equal(before, wheel.Colour);
        }

        /// <summary>SwatchDot is a private nested control, so its Click is raised the same way
        /// Windows would rather than through a public seam that only exists for tests.</summary>
        private static void Click(Control control) =>
            typeof(Control).GetMethod("OnClick",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(control, new object[] { EventArgs.Empty });

        // ---- helpers ---------------------------------------------------------------------

        private static CrosshairPage BuildPage(string directory, AppSettings? settings = null)
        {
            Theme.Apply("Violet");

            var page = new CrosshairPage(
                settings ?? new AppSettings(),
                new SettingsStore(directory),
                new CrosshairService());

            page.Size = new Size(900, 700);
            page.CreateControl();
            return page;
        }

        private static string Describe(Control c) =>
            (string.IsNullOrWhiteSpace(c.Text) ? c.GetType().Name : c.Text.Trim()) + " " + c.Bounds;

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "PlexusXCross_" + Guid.NewGuid().ToString("N"));

            public void Dispose()
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
        }
    }
}
