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

        /// <summary>
        /// A preview must be fully opaque.
        ///
        /// The R/G/B checks that used to be here were tautologies - those are bytes, so
        /// InRange(0, 255) cannot fail for any implementation at all. Only the alpha carried
        /// information, so only the alpha is left.
        /// </summary>
        [Fact]
        public void EveryPreviewIsOpaque()
        {
            foreach (var p in GameColourPresets.All.SelectMany(g => g.Presets))
                foreach (var s in GameColourPresets.SampleColours)
                    Assert.Equal(255, GameColourPresets.Preview(p, s).A);
        }

        /// <summary>
        /// The preview must use the SAME vibrance function the engine applies.
        ///
        /// This is the check whose absence let a real bug ship: the preview used
        /// 1 + vibrance/100 while the engine uses SoftwareVibranceFactor. On NVIDIA the driver
        /// owns 0-100 and the matrix gets 1.0, so a tile built from 1.55 showed a lift that
        /// never happens; on AMD the same preset is about 1.03, roughly fifty times less than
        /// the tile claimed. The class doc says it does not approximate, so this holds it to it.
        /// </summary>
        [Theory]
        [InlineData(true)]    // NVIDIA: the driver owns the low range
        [InlineData(false)]   // AMD/Intel: software carries all of it
        public void ThePreviewUsesTheEnginesOwnVibranceFunction(bool driverAvailable)
        {
            foreach (var p in GameColourPresets.All.SelectMany(g => g.Presets))
            {
                float engineFactor = VibranceEngine.SoftwareVibranceFactor(p.Vibrance, driverAvailable);

                var expected = ColorAdjust.Build(
                    saturation: p.Saturation / 100f,
                    vibrance: engineFactor,
                    contrast: p.Contrast / 100f,
                    brightness: p.Brightness / 100f,
                    warmth: p.Temperature / 100f);

                // Reproduce what Preview does with that matrix, on one sample.
                var source = GameColourPresets.SampleColours[0];
                float r = source.R / 255f, g = source.G / 255f, b = source.B / 255f;

                float nr = r * expected[0] + g * expected[5] + b * expected[10] + expected[20];

                var actual = GameColourPresets.Preview(p, source, driverAvailable);

                // Through the tone ramp both sides would diverge, so compare the presets whose
                // grade is neutral - there the matrix is the whole pipeline.
                if (!(p.Tone with { Gamma = p.Tone.ResolvedGamma }).IsNeutral) continue;

                int want = System.Math.Clamp((int)System.MathF.Round(nr * 255f), 0, 255);
                Assert.InRange(actual.R, want - 1, want + 1);
            }
        }

        /// <summary>
        /// On NVIDIA, a preset's vibrance below the driver ceiling must not move the preview's
        /// matrix at all - the driver does that part, and the matrix genuinely gets 1.0.
        /// </summary>
        [Fact]
        public void OnNvidiaLowVibranceIsLeftToTheDriver()
        {
            // 80 is above neutral but below the driver ceiling: NVIDIA's driver does all of
            // it, so the matrix stays at 1.0; on the software path the matrix has to carry it.
            var preset = new ColourPreset("t", "vibrance only, nothing else",
                Vibrance: 80, Saturation: 100, Brightness: 100, Contrast: 100, Temperature: 0,
                Tone: ToneSettings.Neutral);

            var source = Color.FromArgb(255, 200, 120, 80);

            Assert.Equal(source, GameColourPresets.Preview(preset, source, driverAvailable: true));
            Assert.NotEqual(source, GameColourPresets.Preview(preset, source, driverAvailable: false));
        }

        /// <summary>
        /// Neutral has to be neutral on a machine with no NVIDIA driver too.
        ///
        /// The vibrance scale is 0 = greyscale, 50 = untouched. Neutral shipped at 0, which on
        /// NVIDIA looked fine - the driver owns everything below the ceiling and the matrix is
        /// 1.0 either way - and on AMD or Intel drained every colour off the screen. A preset
        /// called Neutral that greys out the display is the worst possible first click.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NeutralIsNeutralOnEveryGpu(bool driverAvailable)
        {
            foreach (var source in GameColourPresets.SampleColours)
            {
                var previewed = GameColourPresets.Preview(
                    GameColourPresets.Neutral, source, driverAvailable);

                Assert.InRange(previewed.R, source.R - 2, source.R + 2);
                Assert.InRange(previewed.G, source.G - 2, source.G + 2);
                Assert.InRange(previewed.B, source.B - 2, source.B + 2);
            }
        }

        /// <summary>
        /// No preset except the deliberately flat ones may desaturate on the software path.
        ///
        /// Below 50 the scale removes colour. A preset whose whole selling point is "more
        /// colour" sitting at 35 would do the opposite of its own description on every
        /// non-NVIDIA machine, and the tile would not have shown it either.
        /// </summary>
        [Fact]
        public void OnlyThePresetsThatSayTheyAreFlatSitBelowNeutral()
        {
            foreach (var group in GameColourPresets.All)
                foreach (var p in group.Presets)
                {
                    if (p.Vibrance >= 50) continue;

                    bool saysSo = p.Name.Contains("Flat", StringComparison.OrdinalIgnoreCase)
                               || p.Why.Contains("low contrast", StringComparison.OrdinalIgnoreCase)
                               || p.Why.Contains("softer", StringComparison.OrdinalIgnoreCase)
                               || p.Why.Contains("look rather than", StringComparison.OrdinalIgnoreCase);

                    Assert.True(saysSo,
                        $"{group.Game}/{p.Name} has vibrance {p.Vibrance}, which removes colour, "
                        + "but does not describe itself as flat or soft");
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
