using System.Drawing;
using System.Linq;
using VibranceHud;
using VibranceHud.Display;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The per-game colour presets, and the preview that claims to show them.
    ///
    /// The preview is the part worth testing hardest: a tile that showed roughly the right
    /// thing would be a promise the app then breaks when the preset is applied for real.
    /// </summary>
    public sealed class GameColourPresetTests
    {
        [Fact]
        public void EveryGameOffersARealChoice()
        {
            Assert.NotEmpty(GameColourPresets.All);

            foreach (var group in GameColourPresets.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(group.Game));
                Assert.True(group.Presets.Count >= 4,
                    $"{group.Game} has only {group.Presets.Count} presets - that is a list, not a choice");
            }
        }

        /// <summary>Neutral has to be first in every group, so "put it back" is one click and
        /// never requires remembering what neutral was.</summary>
        [Fact]
        public void NeutralIsAlwaysTheFirstOption()
        {
            foreach (var group in GameColourPresets.All)
                Assert.Equal("Neutral", group.Presets[0].Name);
        }

        [Fact]
        public void NoTwoPresetsInAGroupAreTheSameLook()
        {
            foreach (var group in GameColourPresets.All)
            {
                var looks = group.Presets
                    .Select(p => (p.Vibrance, p.Saturation, p.Brightness, p.Contrast, p.Temperature, p.Tone))
                    .ToList();

                Assert.Equal(looks.Count, looks.Distinct().Count());
            }
        }

        [Fact]
        public void EveryPresetSaysWhatItIsFor()
        {
            foreach (var p in GameColourPresets.All.SelectMany(g => g.Presets))
            {
                Assert.False(string.IsNullOrWhiteSpace(p.Name));
                Assert.True(p.Why.Length > 15, $"'{p.Name}' does not explain itself");
            }
        }

        /// <summary>
        /// Every value has to be inside the engine's own bounds. A preset that clamps on
        /// apply is not the look the tile showed.
        /// </summary>
        [Fact]
        public void EveryPresetIsInsideTheEnginesRange()
        {
            foreach (var p in GameColourPresets.All.SelectMany(g => g.Presets))
            {
                Assert.InRange(p.Vibrance, 0, VibranceEngine.MaxVibrance);
                Assert.InRange(p.Saturation, 0, VibranceEngine.MaxSaturation);
                Assert.InRange(p.Brightness, VibranceEngine.MinBrightness, VibranceEngine.MaxBrightness);
                Assert.InRange(p.Contrast, VibranceEngine.MinContrast, VibranceEngine.MaxContrast);
                Assert.InRange(p.Temperature, VibranceEngine.MinTemperature, VibranceEngine.MaxTemperature);
                Assert.InRange(p.Tone.ResolvedGamma, VibranceEngine.MinGamma, VibranceEngine.MaxGamma);
            }
        }

        [Fact]
        public void AnUnknownGameStillGetsPresets()
        {
            var group = GameColourPresets.ForGame("Some Game Nobody Has Heard Of");

            Assert.NotNull(group);
            Assert.True(group.Presets.Count >= 4);
        }

        [Fact]
        public void AKnownGameGetsItsOwn()
        {
            Assert.Equal("Rust", GameColourPresets.ForGame("rust").Game);
            Assert.Equal("CS2", GameColourPresets.ForGame("CS2").Game);
        }

        // ---- preview ----------------------------------------------------------------------

        /// <summary>The neutral preset must leave a colour alone, or every tile is lying about
        /// its baseline and the whole strip is meaningless.</summary>
        [Fact]
        public void NeutralPreviewsAsNoChange()
        {
            foreach (var source in GameColourPresets.SampleColours)
            {
                var previewed = GameColourPresets.Preview(GameColourPresets.Neutral, source);

                Assert.InRange(previewed.R, source.R - 2, source.R + 2);
                Assert.InRange(previewed.G, source.G - 2, source.G + 2);
                Assert.InRange(previewed.B, source.B - 2, source.B + 2);
            }
        }

        [Fact]
        public void APresetThatIsNotNeutralActuallyChangesSomething()
        {
            foreach (var group in GameColourPresets.All)
            {
                foreach (var p in group.Presets.Skip(1))
                {
                    bool moved = GameColourPresets.SampleColours.Any(s =>
                        GameColourPresets.Preview(p, s) != s);

                    Assert.True(moved, $"{group.Game}/{p.Name} previews as no change at all");
                }
            }
        }

        /// <summary>A preview must never produce a colour outside the range a screen can show
        /// - clamping is the pipeline's job, not the caller's.</summary>
        [Fact]
        public void EveryPreviewIsAValidColour()
        {
            foreach (var p in GameColourPresets.All.SelectMany(g => g.Presets))
                foreach (var s in GameColourPresets.SampleColours)
                {
                    var c = GameColourPresets.Preview(p, s);
                    Assert.InRange(c.R, 0, 255);
                    Assert.InRange(c.G, 0, 255);
                    Assert.InRange(c.B, 0, 255);
                    Assert.Equal(255, c.A);
                }
        }

        /// <summary>
        /// A preset that lifts shadows has to visibly lift the darkest sample. This is the
        /// check that the tone ramp is genuinely being applied rather than skipped.
        /// </summary>
        [Fact]
        public void LiftedShadowsShowUpOnTheDarkestSample()
        {
            var lifted = new ColourPreset("test", "lifts shadows a lot",
                Vibrance: 0, Saturation: 100, Brightness: 100, Contrast: 100, Temperature: 0,
                Tone: new ToneSettings(Gamma: 100, Shadows: 80, Blacks: 60));

            var black = Color.FromArgb(255, 24, 24, 28);

            var previewed = GameColourPresets.Preview(lifted, black);

            Assert.True(previewed.R > black.R,
                $"shadow lift did nothing: {black.R} -> {previewed.R}");
        }

        [Fact]
        public void ThePreviewStripUsesColoursAPlayerActuallyLooksAt()
        {
            // Not a rainbow. A rainbow makes every preset look dramatic and says nothing.
            Assert.True(GameColourPresets.SampleColours.Length >= 5);
            Assert.Contains(GameColourPresets.SampleColours, c => c.R < 40 && c.G < 40 && c.B < 40);
        }
    }
}
