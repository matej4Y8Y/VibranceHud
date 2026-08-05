using System;
using System.Linq;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class DisplayPresetTests
    {
        [Fact]
        public void Catalog_CoversNeutralAndRustsThreeMajorBiomeFamilies()
        {
            Assert.Equal(
                new[] { "Balanced", "Forest", "Desert", "Snow" },
                DisplayPresets.All.Select(p => p.Name));
        }

        [Fact]
        public void EveryPreset_StaysInsideTheDisplayControls()
        {
            foreach (var preset in DisplayPresets.All)
            {
                Assert.InRange(preset.Brightness, VibranceEngine.MinBrightness, VibranceEngine.MaxBrightness);
                Assert.InRange(preset.Gamma, VibranceEngine.MinGamma, VibranceEngine.MaxGamma);
                Assert.InRange(preset.Contrast, VibranceEngine.MinContrast, VibranceEngine.MaxContrast);
                Assert.InRange(preset.Temperature, VibranceEngine.MinTemperature, VibranceEngine.MaxTemperature);
                Assert.False(string.IsNullOrWhiteSpace(preset.Subtitle));
            }
        }

        [Fact]
        public void A_preset_carries_tone_only_never_the_users_own_colour()
        {
            // Saturation and vibrance are the two controls the page puts at the top at full
            // size, because they are the user's taste. If a preset ever gains the ability to
            // set them, changing biome silently discards what someone just dialled in.
            var properties = typeof(DisplayPreset).GetProperties().Select(p => p.Name).ToArray();

            Assert.DoesNotContain("Saturation", properties);
            Assert.DoesNotContain("Vibrance", properties);
        }

        [Fact]
        public void Balanced_IsAnUntintedDailyDriver()
        {
            var preset = DisplayPresets.Balanced;

            Assert.Equal(100, preset.Brightness);
            Assert.Equal(100, preset.Gamma);
            Assert.Equal(100, preset.Contrast);
            Assert.Equal(0, preset.Temperature);
        }

        [Fact]
        public void Forest_OpensShadowsInsteadOfFightingItself()
        {
            var preset = DisplayPresets.Forest;

            // The point of this look is seeing into shade under the canopy.
            Assert.True(preset.Gamma > 100, "forest has to lift the shadows");
            // Contrast must NOT also climb - it would crush exactly what gamma just opened.
            Assert.True(preset.Contrast <= 100,
                "raising contrast here cancels the gamma lift, which is the classic preset mistake");
            Assert.True(preset.Temperature < 0, "cooling separates warm players from green foliage");
        }

        [Fact]
        public void Desert_CoolsWarmTerrainAndProtectsBrightDetail()
        {
            var preset = DisplayPresets.Desert;

            Assert.True(preset.Brightness < 100, "sand and sky are already near clipping");
            Assert.True(preset.Contrast > 100, "long sightlines need edge definition");
            Assert.True(preset.Temperature < 0, "the yellow cast is what flattens this biome");
        }

        [Fact]
        public void Snow_PullsBackTheGlareHardestOfAll()
        {
            var preset = DisplayPresets.Snow;

            Assert.True(preset.Temperature > 0, "snow needs a warm counter-cast");
            Assert.True(preset.Gamma <= 100, "lifting midtones here just washes the ground out");
            // Snow clips faster than any other scene, so this is the one that must come down
            // furthest and the one whose contrast must stay gentle.
            Assert.True(preset.Brightness < DisplayPresets.Desert.Brightness,
                "snow is brighter than desert and has to drop further");
            Assert.InRange(preset.Contrast, 100, 110);
        }

        [Fact]
        public void The_biome_looks_actually_differ_from_neutral_and_from_each_other()
        {
            var looks = DisplayPresets.All
                .Select(p => (p.Brightness, p.Gamma, p.Contrast, p.Temperature))
                .ToArray();

            Assert.Equal(looks.Length, looks.Distinct().Count());
        }

        [Fact]
        public void Matches_RequiresTheWholeTone_NotOneControl()
        {
            var preset = DisplayPresets.Forest;

            Assert.True(preset.Matches(
                preset.Brightness, preset.Gamma, preset.Contrast, preset.Temperature));
            Assert.False(preset.Matches(
                preset.Brightness, preset.Gamma, preset.Contrast + 1, preset.Temperature));
        }
    }
}
