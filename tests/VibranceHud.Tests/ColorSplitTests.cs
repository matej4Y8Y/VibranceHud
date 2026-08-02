using System.Collections.Generic;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Vibrance and Saturation are two genuinely different effects, so they are two
    /// separate controls:
    ///   Vibrance   0-100  -> the NVIDIA driver's Digital Vibrance. Non-linear: it lifts
    ///                        muted colours and largely leaves already-saturated ones
    ///                        (and skin tones) alone. Needs an NVIDIA GPU.
    ///   Saturation 0-200  -> the software colour matrix. Linear: every colour's chroma
    ///                        scaled by the same factor. Works on any GPU, and is what
    ///                        lets the app go past the driver's own ceiling.
    /// </summary>
    public class ColorSplitTests
    {
        private sealed class FakeController : IVibranceController
        {
            public int LastSet = -1;
            public int CurrentLevel { get; set; }
            public int DefaultLevel { get; set; } = 50;
            public bool IsAvailable { get; set; } = true;
            public void SetLevel(int level) { LastSet = level; CurrentLevel = level; }
        }

        private sealed class FakeOverlay : ISaturationOverlay
        {
            public readonly List<float[]> Applied = new();
            public int ClearCalls;
            public void Apply(float[] matrix) => Applied.Add(matrix);
            public void Clear() => ClearCalls++;
            public float[] Last => Applied[^1];
        }

        private sealed class FakeGamma : IGammaRamp
        {
            public void Apply(ushort[] ramp) { }
            public void Reset() { }
        }

        private static (VibranceEngine engine, FakeController ctrl, FakeOverlay ovl) NewEngine()
        {
            var ctrl = new FakeController();
            var ovl = new FakeOverlay();
            return (new VibranceEngine(ctrl, ovl, new FakeGamma()), ctrl, ovl);
        }

        private static void AssertMatrix(float[] expected, float[] actual)
        {
            for (int i = 0; i < 25; i++) Assert.Equal(expected[i], actual[i], 4);
        }

        // ---- Vibrance drives the driver, and only the driver ----

        [Fact]
        public void Vibrance_GoesToTheDriver_AndLeavesTheMatrixAlone()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.Saturation = 100;   // neutral
            engine.Vibrance = 70;

            Assert.Equal(70, ctrl.LastSet);
            Assert.Equal(70, engine.Vibrance);
            // Saturation neutral => nothing for the software pass to do.
            Assert.True(ovl.ClearCalls > 0);
        }

        [Fact]
        public void Vibrance_ClampsToRange()
        {
            var (engine, ctrl, _) = NewEngine();

            // Past the ceiling, whatever the ceiling currently is - a literal here goes stale
            // the moment the slider gets more headroom.
            engine.Vibrance = VibranceEngine.MaxVibrance + 50;
            Assert.Equal(VibranceEngine.MaxVibrance, engine.Vibrance);

            engine.Vibrance = -10;
            Assert.Equal(0, engine.Vibrance);
        }

        // ---- Past the driver's ceiling, vibrance continues in software ----

        [Fact]
        public void Vibrance_PastDriverCeiling_PinsDriver_AndBoostsInSoftware()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.Vibrance = 180;

            // No hardware left to ask for - driver sits at its ceiling.
            Assert.Equal(VibranceEngine.DriverVibranceCeiling, ctrl.LastSet);
            Assert.Equal(180, engine.Vibrance);
            AssertMatrix(ColorAdjust.Build(1f, 1.8f, 1f, 0f), ovl.Last);
        }

        [Fact]
        public void Vibrance_AtCeiling_IsStillPureDriver()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.Vibrance = 100;

            Assert.Equal(100, ctrl.LastSet);
            Assert.Empty(ovl.Applied); // nothing for software to do yet
        }

        [Fact]
        public void SoftwareVibrance_SparesRed_UnlikeFlatSaturation()
        {
            // Both lift the picture, but vibrance holds the red channel back - that is
            // the whole difference between the two controls above the driver ceiling.
            var vib = ColorAdjust.Build(1f, 2f, 1f, 0f);
            var sat = ColorAdjust.Build(2f, 1f, 1f, 0f);

            // [0] is red-in -> red-out: how hard red is being pushed.
            Assert.True(vib[0] < sat[0]);
            // ...while green is pushed exactly as hard ([6] is green-in -> green-out).
            Assert.Equal(sat[6], vib[6], 4);
        }

        [Fact]
        public void VibranceAndSaturation_Compose_IntoOneMatrix()
        {
            var (engine, _, ovl) = NewEngine();

            engine.Vibrance = 150;
            engine.Saturation = 140;

            AssertMatrix(ColorAdjust.Build(1.4f, 1.5f, 1f, 0f), ovl.Last);
        }

        // ---- Saturation drives the matrix, and only the matrix ----

        [Fact]
        public void Saturation_BuildsTheMatrix_WithoutTouchingTheDriver()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.Vibrance = 40;
            int driverAfterVibrance = ctrl.LastSet;
            engine.Saturation = 180;

            Assert.Equal(driverAfterVibrance, ctrl.LastSet); // driver untouched
            AssertMatrix(ColorAdjust.Build(1.8f, 1f, 0f), ovl.Last);
        }

        [Fact]
        public void Saturation_BelowNeutral_Desaturates()
        {
            var (engine, _, ovl) = NewEngine();

            engine.Saturation = 40;

            AssertMatrix(ColorAdjust.Build(0.4f, 1f, 0f), ovl.Last);
        }

        [Fact]
        public void Saturation_AtZero_IsGreyscale()
        {
            var (engine, _, ovl) = NewEngine();

            engine.Saturation = 0;

            // Every output channel becomes pure luma - the row values are the coefficients.
            AssertMatrix(ColorAdjust.Build(0f, 1f, 0f), ovl.Last);
        }

        [Fact]
        public void Saturation_ClampsToRange()
        {
            var (engine, _, _) = NewEngine();

            engine.Saturation = 500;
            Assert.Equal(VibranceEngine.MaxSaturation, engine.Saturation);

            engine.Saturation = -50;
            Assert.Equal(0, engine.Saturation);
        }

        // ---- The two are independent ----

        [Fact]
        public void VibranceAndSaturation_AreIndependent()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.Vibrance = 85;
            engine.Saturation = 150;

            Assert.Equal(85, ctrl.LastSet);              // driver got vibrance
            Assert.Equal(85, engine.Vibrance);
            Assert.Equal(150, engine.Saturation);
            AssertMatrix(ColorAdjust.Build(1.5f, 1f, 0f), ovl.Last);

            // Changing one must not disturb the other.
            engine.Saturation = 100;
            Assert.Equal(85, engine.Vibrance);
            Assert.Equal(85, ctrl.LastSet);
        }

        [Fact]
        public void Saturation_StillSharesOneMatrixWithBrightnessAndEyeCare()
        {
            var (engine, _, ovl) = NewEngine();

            engine.Saturation = 160;
            engine.Brightness = 90;
            engine.EyeCare = true;

            AssertMatrix(ColorAdjust.Build(1.6f, 0.9f, VibranceEngine.EyeCareWarmth), ovl.Last);
        }

        [Fact]
        public void Reset_ReturnsVibranceToDriverDefault_AndSaturationToNeutral()
        {
            var (engine, ctrl, _) = NewEngine();
            ctrl.DefaultLevel = 50;
            engine.Vibrance = 100;
            engine.Saturation = 200;

            engine.Reset();

            Assert.Equal(50, engine.Vibrance);
            Assert.Equal(100, engine.Saturation);
            Assert.Equal(100, engine.Brightness);
        }

        // ---- Settings migration: old installs stored one combined 0-200 "Level" ----

        [Theory]
        // legacy level -> (vibrance, saturation). Matches exactly what the old single
        // slider did internally, so upgrading changes nothing on screen.
        [InlineData(0, 0, 100)]
        [InlineData(50, 50, 100)]
        [InlineData(100, 100, 100)]
        [InlineData(150, 100, 150)]
        [InlineData(200, 100, 200)]
        public void LegacyLevel_SplitsIntoVibranceAndSaturation(int level, int vib, int sat)
        {
            var s = new AppSettings { Level = level };

            Assert.Equal(vib, s.ResolvedVibrance);
            Assert.Equal(sat, s.ResolvedSaturation);
        }

        [Fact]
        public void ExplicitValues_WinOverTheLegacyLevel()
        {
            var s = new AppSettings
            {
                Level = 200,               // what an old build wrote
                VibrancePercent = 30,      // what this build wrote
                SaturationPercent = 120
            };

            Assert.Equal(30, s.ResolvedVibrance);
            Assert.Equal(120, s.ResolvedSaturation);
        }

        [Fact]
        public void FreshInstall_IsNeutralOnBothControls()
        {
            var s = new AppSettings();

            Assert.Equal(100, s.ResolvedVibrance);
            Assert.Equal(100, s.ResolvedSaturation);
        }
    }
}
