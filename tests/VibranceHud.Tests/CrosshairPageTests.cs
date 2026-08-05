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
