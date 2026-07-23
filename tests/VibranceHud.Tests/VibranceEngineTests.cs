using System.Collections.Generic;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class VibranceEngineTests
    {
        private sealed class FakeController : IVibranceController
        {
            public int LastSet = -1;
            public int CurrentLevel { get; set; }
            public int DefaultLevel { get; set; } = 50;
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

        private static (VibranceEngine engine, FakeController ctrl, FakeOverlay ovl) NewEngine()
        {
            var ctrl = new FakeController();
            var ovl = new FakeOverlay();
            return (new VibranceEngine(ctrl, ovl), ctrl, ovl);
        }

        private static void AssertMatrix(float[] expected, float[] actual)
        {
            for (int i = 0; i < 25; i++) Assert.Equal(expected[i], actual[i], 4);
        }

        [Fact]
        public void Max_Is200()
        {
            Assert.Equal(200, VibranceEngine.Max);
        }

        [Fact]
        public void BelowThreshold_DrivesGpuOnly_AndClearsOverlay()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.SetLevel(80);

            Assert.Equal(80, ctrl.LastSet);
            Assert.Empty(ovl.Applied);
            Assert.Equal(1, ovl.ClearCalls);
        }

        [Fact]
        public void AtThreshold100_IsStillGpuOnly()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.SetLevel(100);

            Assert.Equal(100, ctrl.LastSet);
            Assert.Empty(ovl.Applied);
        }

        [Fact]
        public void AboveThreshold_PinsGpuAt100_AndAppliesSaturationMatrix()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.SetLevel(150);

            Assert.Equal(100, ctrl.LastSet);
            AssertMatrix(ColorAdjust.Build(1.5f, 1f, 0f), ovl.Last);
        }

        [Fact]
        public void ClampsAboveMax()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.SetLevel(250);

            Assert.Equal(200, engine.CurrentLevel);
            Assert.Equal(100, ctrl.LastSet);
            AssertMatrix(ColorAdjust.Build(2f, 1f, 0f), ovl.Last);
        }

        [Fact]
        public void ClampsBelowZero()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.SetLevel(-10);

            Assert.Equal(0, engine.CurrentLevel);
            Assert.Equal(0, ctrl.LastSet);
            Assert.Equal(1, ovl.ClearCalls);
        }

        [Fact]
        public void CurrentLevel_ReflectsLastSet_IncludingOvershoot()
        {
            var (engine, _, _) = NewEngine();

            engine.SetLevel(175);

            Assert.Equal(175, engine.CurrentLevel);
        }

        [Fact]
        public void DefaultLevel_ComesFromController()
        {
            var (engine, ctrl, _) = NewEngine();
            ctrl.DefaultLevel = 42;

            Assert.Equal(42, engine.DefaultLevel);
        }

        [Fact]
        public void Brightness_AppliesMatrix_EvenBelowVibranceThreshold()
        {
            var (engine, _, ovl) = NewEngine();
            engine.SetLevel(80); // clears

            engine.Brightness = 70;

            Assert.Equal(70, engine.Brightness);
            AssertMatrix(ColorAdjust.Build(1f, 0.7f, 0f), ovl.Last);
        }

        [Fact]
        public void Brightness_IsClampedToSafeRange()
        {
            var (engine, _, _) = NewEngine();

            engine.Brightness = 500;
            Assert.Equal(VibranceEngine.MaxBrightness, engine.Brightness);

            engine.Brightness = 0;
            Assert.Equal(VibranceEngine.MinBrightness, engine.Brightness);
        }

        [Fact]
        public void EyeCare_AppliesWarmMatrix_AndClearsWhenTurnedOff()
        {
            var (engine, _, ovl) = NewEngine();
            engine.SetLevel(100);

            engine.EyeCare = true;
            AssertMatrix(ColorAdjust.Build(1f, 1f, VibranceEngine.EyeCareWarmth), ovl.Last);

            int clearsBefore = ovl.ClearCalls;
            engine.EyeCare = false;
            Assert.Equal(clearsBefore + 1, ovl.ClearCalls); // back to identity
        }

        [Fact]
        public void Combined_VibranceBrightnessAndEyeCare_ShareOneMatrix()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.SetLevel(160);
            engine.Brightness = 90;
            engine.EyeCare = true;

            Assert.Equal(100, ctrl.LastSet); // driver still pinned
            AssertMatrix(ColorAdjust.Build(1.6f, 0.9f, VibranceEngine.EyeCareWarmth), ovl.Last);
        }

        [Fact]
        public void Reset_RestoresDriverDefault_AndNeutralAdjustments()
        {
            var (engine, ctrl, ovl) = NewEngine();
            ctrl.DefaultLevel = 50;
            engine.SetLevel(200);
            engine.Brightness = 60;
            engine.EyeCare = true;

            engine.Reset();

            Assert.Equal(50, ctrl.LastSet);
            Assert.Equal(50, engine.CurrentLevel);
            Assert.Equal(100, engine.Brightness);
            Assert.False(engine.EyeCare);
            Assert.True(ovl.ClearCalls >= 1); // neutral again
        }
    }
}
