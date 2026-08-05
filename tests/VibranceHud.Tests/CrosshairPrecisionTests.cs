using System.Linq;
using VibranceHud.Crosshair;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Sub-pixel crosshair sizing, and the presets built on it.
    ///
    /// Whole pixels were too coarse to aim with: at the sizes people actually use, one step of
    /// thickness is the difference between a usable crosshair and an unusable one, and there
    /// was nothing between 2 and 3.
    /// </summary>
    public sealed class CrosshairPrecisionTests
    {
        // ---- migration -------------------------------------------------------------------

        /// <summary>
        /// The one that must not break. A crosshair saved before decimals existed has to load
        /// at exactly the shape its owner set - silently changing somebody's aim point on
        /// upgrade is about the worst thing this feature could do.
        /// </summary>
        [Fact]
        public void ACrosshairSavedBeforeDecimalsKeepsItsExactShape()
        {
            var legacy = new CrosshairConfig { Size = 8, Thickness = 2, Gap = 4 };

            Assert.Equal(8f, legacy.ResolvedSize);
            Assert.Equal(2f, legacy.ResolvedThickness);
            Assert.Equal(4f, legacy.ResolvedGap);
        }

        [Fact]
        public void TenthsWinOverTheLegacyValueOnceSet()
        {
            var config = new CrosshairConfig { Size = 8 };
            config.SetSizeTenths(34);

            Assert.Equal(3.4f, config.ResolvedSize);
        }

        /// <summary>
        /// The legacy whole-pixel field is still written, so a downgrade to a build that only
        /// understands whole pixels lands on the nearest shape rather than on whatever the
        /// field happened to hold. Rounded, not truncated, so it is not systematically thin.
        /// </summary>
        [Theory]
        [InlineData(34, 3)]
        [InlineData(35, 4)]
        [InlineData(29, 3)]
        [InlineData(5, 1)]
        public void TheLegacyFieldTracksTheNearestWholePixel(int tenths, int expectedLegacy)
        {
            var config = new CrosshairConfig();
            config.SetThicknessTenths(tenths);

            Assert.Equal(expectedLegacy, config.Thickness);
        }

        [Fact]
        public void CloneCarriesTheTenths()
        {
            var config = new CrosshairConfig();
            config.SetSizeTenths(77);
            config.SetGapTenths(13);

            var clone = config.Clone();

            Assert.Equal(7.7f, clone.ResolvedSize);
            Assert.Equal(1.3f, clone.ResolvedGap);
        }

        // ---- presets ---------------------------------------------------------------------

        [Fact]
        public void EveryPresetIsWhite()
        {
            // Colour is the user's own choice; a preset hands them a shape, not a look.
            Assert.All(CrosshairPresets.All,
                p => Assert.Equal(CrosshairPresets.White, CrosshairPresets.ToConfig(p).ColourArgb));
        }

        [Fact]
        public void ApplyingAPresetLeavesColourAndNameAlone()
        {
            var mine = new CrosshairConfig
            {
                Name = "my sniper dot",
                ColourArgb = unchecked((int)0xFFFF0000),
                Outline = false,
            };

            CrosshairPresets.Apply(mine, CrosshairPresets.All.First(p => p.Name == "Wide"));

            Assert.Equal("my sniper dot", mine.Name);
            Assert.Equal(unchecked((int)0xFFFF0000), mine.ColourArgb);
            Assert.False(mine.Outline);

            // But the shape did change.
            Assert.Equal(14f, mine.ResolvedSize);
        }

        [Fact]
        public void ApplyingAPresetSetsTheShapeAndTheDot()
        {
            var config = new CrosshairConfig();
            CrosshairPresets.Apply(config, CrosshairPresets.All.First(p => p.Name == "Cross+Dot"));

            Assert.Equal(CrosshairShape.Cross, config.Shape);
            Assert.True(config.CentreDot);

            CrosshairPresets.Apply(config, CrosshairPresets.All.First(p => p.Name == "Classic"));
            Assert.False(config.CentreDot);
        }

        [Fact]
        public void EveryPresetRecognisesItself()
        {
            foreach (var preset in CrosshairPresets.All)
            {
                var config = CrosshairPresets.ToConfig(preset);
                var matched = CrosshairPresets.Matching(config);

                Assert.NotNull(matched);
                Assert.Equal(preset.Name, matched!.Name);
            }
        }

        [Fact]
        public void MovingASliderOffAPresetClearsTheMatch()
        {
            var config = CrosshairPresets.ToConfig(CrosshairPresets.All[0]);
            Assert.NotNull(CrosshairPresets.Matching(config));

            config.SetSizeTenths(config.SizeTenths!.Value + 1);

            Assert.Null(CrosshairPresets.Matching(config));
        }

        /// <summary>Matching compares the shape, not the paint - a red Classic is still a
        /// Classic.</summary>
        [Fact]
        public void MatchingIgnoresColour()
        {
            var config = CrosshairPresets.ToConfig(CrosshairPresets.All[0]);
            config.ColourArgb = unchecked((int)0xFF00AAFF);

            Assert.NotNull(CrosshairPresets.Matching(config));
        }

        [Fact]
        public void PresetNamesAreUniqueAndPresentable()
        {
            var names = CrosshairPresets.All.Select(p => p.Name).ToList();

            Assert.Equal(names.Count, names.Distinct().Count());
            Assert.All(names, n => Assert.False(string.IsNullOrWhiteSpace(n)));
        }

        [Fact]
        public void EveryPresetHasUsableDimensions()
        {
            foreach (var preset in CrosshairPresets.All)
            {
                var config = CrosshairPresets.ToConfig(preset);

                Assert.InRange(config.ResolvedSize, 0.5f, 30f);
                Assert.InRange(config.ResolvedThickness, 0.5f, 10f);
                Assert.InRange(config.ResolvedGap, 0f, 30f);
            }
        }

        /// <summary>The geometry has to survive every preset without throwing or producing
        /// nothing to draw.</summary>
        [Fact]
        public void EveryPresetProducesSomethingToDraw()
        {
            foreach (var preset in CrosshairPresets.All)
            {
                var shapes = CrosshairGeometry.Build(CrosshairPresets.ToConfig(preset));
                Assert.True(shapes.Bars.Count > 0 || shapes.Circle != null,
                    $"preset '{preset.Name}' draws nothing at all");
            }
        }
    }
}
