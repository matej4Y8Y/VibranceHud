using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VibranceHud;
using VibranceHud.Controls;
using VibranceHud.Display;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The preset tile's behaviour, as opposed to its painting.
    ///
    /// ControlPaintTests already renders it in every state. What was untested is the part a
    /// user drives: the keyboard path had a test seam added for it and nothing ever called
    /// that seam, so Space and Enter were never shown to do anything at all.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class PresetTileTests
    {
        private static ColourPreset Loud() => GameColourPresets.All[0].Presets[3];

        private static PresetTile Build(ColourPreset? preset = null)
        {
            Theme.Apply("Violet");
            return new PresetTile(preset ?? Loud()) { Size = new Size(140, 74) };
        }

        [Theory]
        [InlineData(Keys.Space)]
        [InlineData(Keys.Enter)]
        public void SpaceAndEnterApplyTheTile(Keys key)
        {
            using var tile = Build();
            int clicks = 0;
            tile.Click += (_, _) => clicks++;

            tile.TestPressKey(key);

            Assert.Equal(1, clicks);
        }

        [Fact]
        public void OtherKeysDoNothing()
        {
            using var tile = Build();
            int clicks = 0;
            tile.Click += (_, _) => clicks++;

            tile.TestPressKey(Keys.A);
            tile.TestPressKey(Keys.Escape);

            Assert.Equal(0, clicks);
        }

        /// <summary>A tile has to be reachable and announced, or the preset row is invisible
        /// to the keyboard and to a screen reader.</summary>
        [Fact]
        public void ATileIsReachableAndAnnounced()
        {
            using var tile = Build();

            Assert.True(tile.TabStop);
            Assert.Equal(AccessibleRole.RadioButton, tile.AccessibleRole);
            Assert.Equal(tile.Preset.Name, tile.AccessibleName);
        }

        [Fact]
        public void ActiveIsSettableAndReported()
        {
            using var tile = Build();

            Assert.False(tile.Active);
            tile.Active = true;
            Assert.True(tile.Active);
        }

        /// <summary>
        /// The strip is computed once from the preset, so a tile built for a loud preset must
        /// not preview the same colours as one built for neutral. If those matched, the cache
        /// would be returning one preset's answer for another.
        /// </summary>
        /// <summary>
        /// Hiding the scrollbar must not take the scrolling with it.
        ///
        /// This exact combination has already shipped broken once at page level:
        /// ScrollableControl checks whether its scrollbar is visible before acting on the
        /// wheel, so hiding the bar silently kills the wheel. A gallery nobody can scroll is
        /// worse than an ugly bar.
        /// </summary>
        [Fact]
        public void TheGalleryStillScrollsWithItsScrollbarHidden()
        {
            Theme.Apply("Violet");
            using var panel = new QuietScrollPanel { Size = new Size(300, 100) };
            panel.Controls.Add(new Label { Location = new Point(0, 0), Size = new Size(200, 600) });
            panel.AutoScrollMinSize = new Size(0, 600);
            panel.CreateControl();

            panel.TestScrollWheel(-120);

            Assert.True(panel.AutoScrollPosition.Y < 0,
                "the wheel did nothing - hiding the bar killed the scrolling");
        }

        /// <summary>The wheel arrives at whatever is under the cursor, and in a full gallery
        /// that is always a cell - so a cell has to pass it on.</summary>
        [Fact]
        public void TheWheelWorksWithTheCursorOverACell()
        {
            Theme.Apply("Violet");
            using var panel = new QuietScrollPanel { Size = new Size(300, 100) };
            var child = new Label { Location = new Point(0, 0), Size = new Size(200, 600) };
            panel.Controls.Add(child);
            panel.AutoScrollMinSize = new Size(0, 600);
            panel.CreateControl();
            child.CreateControl();

            typeof(Control)
                .GetMethod("OnMouseWheel", System.Reflection.BindingFlags.Instance
                                         | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(child, new object[] { new MouseEventArgs(MouseButtons.None, 0, 0, 0, -120) });

            Assert.True(panel.AutoScrollPosition.Y < 0,
                "the wheel over a child did nothing - the panel never received it");
        }

        /// <summary>
        /// The unactivated Account page must offer a way forward.
        ///
        /// It stated the problem - "needs a valid activation key to run" - and hid its only
        /// button, so the screen somebody sees at the moment they want to pay was a dead end.
        /// </summary>
        [Fact]
        public void TheUnactivatedAccountPageOffersAWayToActivate()
        {
            Theme.Apply("Violet");
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "PxAcct_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);

            using var page = new Pages.AccountPage(new License.LicenseService(dir));
            page.Size = new Size(900, 700);
            page.CreateControl();

            var visible = Descendants(page).OfType<GlassButton>().Where(b => b.Visible).ToList();

            Assert.Contains(visible, b => b.Text.Contains("Enter", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(visible, b => b.Text.Contains("Get", System.StringComparison.OrdinalIgnoreCase));
        }

        private static System.Collections.Generic.IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        [Fact]
        public void DifferentPresetsPreviewDifferently()
        {
            var neutral = GameColourPresets.Neutral;
            var loud = Loud();

            bool differs = false;
            foreach (var sample in GameColourPresets.SampleColours)
                if (GameColourPresets.Preview(neutral, sample) != GameColourPresets.Preview(loud, sample))
                    differs = true;

            Assert.True(differs, $"'{loud.Name}' previews identically to Neutral");
        }
    }
}
