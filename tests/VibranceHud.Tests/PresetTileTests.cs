using System.Drawing;
using System.Windows.Forms;
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
