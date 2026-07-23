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
            public readonly List<float> SaturationCalls = new();
            public int ClearCalls;
            public void SetSaturation(float factor) => SaturationCalls.Add(factor);
            public void Clear() => ClearCalls++;
        }

        private static (VibranceEngine engine, FakeController ctrl, FakeOverlay ovl) NewEngine()
        {
            var ctrl = new FakeController();
            var ovl = new FakeOverlay();
            return (new VibranceEngine(ctrl, ovl), ctrl, ovl);
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
            Assert.Empty(ovl.SaturationCalls);
            Assert.Equal(1, ovl.ClearCalls);
        }

        [Fact]
        public void AtThreshold100_IsStillGpuOnly()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.SetLevel(100);

            Assert.Equal(100, ctrl.LastSet);
            Assert.Empty(ovl.SaturationCalls);
            Assert.Equal(1, ovl.ClearCalls);
        }

        [Fact]
        public void AboveThreshold_PinsGpuAt100_AndSetsSaturation()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.SetLevel(150);

            Assert.Equal(100, ctrl.LastSet);
            Assert.Equal(new[] { 1.5f }, ovl.SaturationCalls.ToArray());
        }

        [Fact]
        public void ClampsAboveMax()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.SetLevel(250);

            Assert.Equal(200, engine.CurrentLevel);
            Assert.Equal(100, ctrl.LastSet);
            Assert.Equal(new[] { 2.0f }, ovl.SaturationCalls.ToArray());
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
        public void Reset_AppliesDriverDefault_AndClearsOverlay()
        {
            var (engine, ctrl, ovl) = NewEngine();
            ctrl.DefaultLevel = 50;

            engine.Reset();

            Assert.Equal(50, ctrl.LastSet);
            Assert.Equal(50, engine.CurrentLevel);
            Assert.Empty(ovl.SaturationCalls);
            Assert.True(ovl.ClearCalls >= 1);
        }
    }
}
