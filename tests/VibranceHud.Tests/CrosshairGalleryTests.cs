using System.Linq;
using VibranceHud.Crosshair;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The crosshair gallery, and the composable shape model underneath it.
    ///
    /// The model is the point. Crosshairs used to be one of four mutually-exclusive shapes -
    /// Cross, Dot, Circle, T - which cannot express a cross WITH a dot, or a ring WITH a dot,
    /// so no catalogue built on it could be more than a handful of entries.
    /// </summary>
    public sealed class CrosshairGalleryTests
    {
        // ---- the model ------------------------------------------------------------------

        [Fact]
        public void ArmsAreIndependentSoCombinationsExist()
        {
            var sideOnly = new CrosshairConfig
            {
                ArmTop = false, ArmBottom = false, ArmLeft = true, ArmRight = true,
            };

            var shapes = CrosshairGeometry.Build(sideOnly);

            // Two arms, no more. The old model had no way to ask for this at all.
            Assert.Equal(2, shapes.Bars.Count);
        }

        [Fact]
        public void ACrossCanCarryACentreDot()
        {
            var config = new CrosshairConfig
            {
                ArmTop = true, ArmBottom = true, ArmLeft = true, ArmRight = true,
                CentreDot = true,
            };

            Assert.Equal(5, CrosshairGeometry.Build(config).Bars.Count);   // 4 arms + dot
        }

        [Fact]
        public void ARingCanCarryACentreDot()
        {
            var config = new CrosshairConfig
            {
                ArmTop = false, ArmBottom = false, ArmLeft = false, ArmRight = false,
                ShowCircle = true, CentreDot = true,
            };

            var shapes = CrosshairGeometry.Build(config);

            Assert.NotNull(shapes.Circle);
            Assert.Single(shapes.Bars);
        }

        [Fact]
        public void TheDotHasItsOwnSizeIndependentOfArmThickness()
        {
            var config = new CrosshairConfig { CentreDot = true, DotSizeTenths = 60 };
            config.SetThicknessTenths(10);

            Assert.Equal(6f, config.ResolvedDotSize);
            Assert.Equal(1f, config.ResolvedThickness);
        }

        // ---- migration: the thing that must not break -----------------------------------

        [Theory]
        [InlineData(CrosshairShape.Cross, true, true, true, true, false)]
        [InlineData(CrosshairShape.T, false, true, true, true, false)]
        [InlineData(CrosshairShape.Dot, false, false, false, false, false)]
        [InlineData(CrosshairShape.Circle, false, false, false, false, true)]
        public void ALegacyShapeMigratesToTheSameCrosshair(
            CrosshairShape shape, bool top, bool bottom, bool left, bool right, bool circle)
        {
            var legacy = new CrosshairConfig { Shape = shape };

            Assert.Equal(top, legacy.ResolvedArmTop);
            Assert.Equal(bottom, legacy.ResolvedArmBottom);
            Assert.Equal(left, legacy.ResolvedArmLeft);
            Assert.Equal(right, legacy.ResolvedArmRight);
            Assert.Equal(circle, legacy.ResolvedShowCircle);
        }

        /// <summary>The old Dot shape WAS a dot, so it has to come back as one - migrating it
        /// to "no arms and no dot" would leave somebody with an invisible crosshair.</summary>
        [Fact]
        public void TheLegacyDotShapeStillDrawsADot()
        {
            var legacy = new CrosshairConfig { Shape = CrosshairShape.Dot };

            Assert.True(legacy.ResolvedCentreDot);
            Assert.Single(CrosshairGeometry.Build(legacy).Bars);
        }

        [Fact]
        public void ExplicitPartsWinOverTheLegacyShape()
        {
            var config = new CrosshairConfig { Shape = CrosshairShape.Cross, ArmTop = false };
            Assert.False(config.ResolvedArmTop);
        }

        // ---- opacity ---------------------------------------------------------------------

        [Theory]
        [InlineData(100, 255)]
        [InlineData(50, 127)]
        [InlineData(0, 0)]
        public void OpacityScalesTheColoursAlpha(int opacity, int expectedAlpha)
        {
            var config = new CrosshairConfig
            {
                ColourArgb = unchecked((int)0xFFFF0000),
                Opacity = opacity,
            };

            Assert.Equal(expectedAlpha, (config.ResolvedColourArgb >> 24) & 0xFF);
        }

        [Fact]
        public void ChangingColourDoesNotResetOpacity()
        {
            var config = new CrosshairConfig { Opacity = 40 };
            config.ColourArgb = unchecked((int)0xFF00FF00);

            Assert.Equal(40, config.Opacity);
            Assert.Equal(102, (config.ResolvedColourArgb >> 24) & 0xFF);
        }

        // ---- the gallery -----------------------------------------------------------------

        [Fact]
        public void ThereAreThirtyCrosshairs()
        {
            Assert.Equal(30, CrosshairGallery.All.Count);
        }

        [Fact]
        public void EveryEntryHasAUniqueIdAndName()
        {
            var ids = CrosshairGallery.All.Select(i => i.Id).ToList();
            var names = CrosshairGallery.All.Select(i => i.Name).ToList();

            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.Equal(names.Count, names.Distinct().Count());
        }

        /// <summary>An entry that draws nothing is a blank cell in the grid the user cannot
        /// tell from a rendering fault.</summary>
        [Fact]
        public void EveryEntryDrawsSomething()
        {
            foreach (var item in CrosshairGallery.All)
            {
                var shapes = CrosshairGeometry.Build(item.Config);
                Assert.True(shapes.Bars.Count > 0 || shapes.Circle != null,
                    $"'{item.Name}' draws nothing at all");
            }
        }

        [Fact]
        public void EveryEntryIsWhiteSoTheUsersColourSurvives()
        {
            Assert.All(CrosshairGallery.All,
                i => Assert.Equal(CrosshairPresets.White, i.Config.ColourArgb));
        }

        [Fact]
        public void EveryEntryStaysInsideTheSliderRanges()
        {
            foreach (var item in CrosshairGallery.All)
            {
                Assert.InRange(item.Config.ResolvedSize, 0.5f, 30f);
                Assert.InRange(item.Config.ResolvedThickness, 0.5f, 10f);
                Assert.InRange(item.Config.ResolvedGap, 0f, 30f);
            }
        }

        [Fact]
        public void EveryFamilyHasEntries()
        {
            foreach (CrosshairGallery.Family family in
                     System.Enum.GetValues<CrosshairGallery.Family>())
                Assert.Contains(CrosshairGallery.All, i => i.Group == family);
        }

        [Fact]
        public void ApplyingAnEntryKeepsTheUsersColourAndOpacity()
        {
            var mine = new CrosshairConfig
            {
                Name = "mine",
                ColourArgb = unchecked((int)0xFFFF0000),
                Opacity = 55,
                Outline = false,
            };

            CrosshairGallery.Apply(mine, CrosshairGallery.All.First(i => i.Name == "Classic"));

            Assert.Equal(unchecked((int)0xFFFF0000), mine.ColourArgb);
            Assert.Equal(55, mine.Opacity);
            Assert.False(mine.Outline);
            Assert.Equal("mine", mine.Name);
        }

        [Fact]
        public void EveryEntryRecognisesItself()
        {
            foreach (var item in CrosshairGallery.All)
            {
                var applied = new CrosshairConfig();
                CrosshairGallery.Apply(applied, item);

                var matched = CrosshairGallery.Matching(applied);

                Assert.True(matched != null, $"'{item.Name}' does not recognise itself");
                Assert.Equal(item.Id, matched!.Id);
            }
        }

        /// <summary>
        /// No two entries may be the same crosshair. Thirty is only worth having if all thirty
        /// are distinct - near-duplicates are what the sliders are for.
        /// </summary>
        [Fact]
        public void NoTwoEntriesAreTheSameCrosshair()
        {
            var seen = new System.Collections.Generic.HashSet<string>();

            foreach (var item in CrosshairGallery.All)
            {
                var c = item.Config;
                var key = string.Join("|",
                    c.ResolvedArmTop, c.ResolvedArmBottom, c.ResolvedArmLeft, c.ResolvedArmRight,
                    c.ResolvedCentreDot, c.ResolvedShowCircle,
                    c.ResolvedSize, c.ResolvedThickness, c.ResolvedGap,
                    c.ResolvedDotSize, c.ResolvedCircleRadius);

                Assert.True(seen.Add(key), $"'{item.Name}' is a duplicate of another entry");
            }
        }
    }
}
