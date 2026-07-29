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
            public readonly List<ushort[]> Applied = new();
            public int ResetCalls;
            public void Apply(ushort[] ramp) => Applied.Add(ramp);
            public void Reset() => ResetCalls++;
            public ushort[] Last => Applied[^1];
        }

        private static (VibranceEngine engine, FakeController ctrl, FakeOverlay ovl) NewEngine()
        {
            var (e, c, o, _) = NewEngineFull();
            return (e, c, o);
        }

        private static (VibranceEngine engine, FakeController ctrl, FakeOverlay ovl, FakeGamma gamma) NewEngineFull()
        {
            var ctrl = new FakeController();
            var ovl = new FakeOverlay();
            var gamma = new FakeGamma();
            return (new VibranceEngine(ctrl, ovl, gamma), ctrl, ovl, gamma);
        }

        private static void AssertMatrix(float[] expected, float[] actual)
        {
            for (int i = 0; i < 25; i++) Assert.Equal(expected[i], actual[i], 4);
        }

        [Fact]
        public void DriverAvailable_ReflectsController()
        {
            var (available, _, _) = NewEngine();
            Assert.True(available.DriverAvailable);

            var ctrl = new FakeController { IsAvailable = false };
            var unavailable = new VibranceEngine(ctrl, new FakeOverlay(), new FakeGamma());
            Assert.False(unavailable.DriverAvailable);
        }

        // Vibrance/Saturation split behaviour lives in ColorSplitTests; these cover the
        // driver-availability edge and how the other adjustments interact with it.

        [Fact]
        public void NoDriver_SoftwareSaturation_StillApplies()
        {
            var ctrl = new FakeController { IsAvailable = false };
            var ovl = new FakeOverlay();
            var engine = new VibranceEngine(ctrl, ovl, new FakeGamma());

            engine.Saturation = 150;

            Assert.Single(ovl.Applied); // software boost works without any driver
        }

        [Fact]
        public void Vibrance_DrivesGpuOnly_AndClearsOverlay()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.Vibrance = 80;

            Assert.Equal(80, ctrl.LastSet);
            Assert.Empty(ovl.Applied);
            Assert.Equal(1, ovl.ClearCalls);
        }

        [Fact]
        public void DefaultLevel_ComesFromController()
        {
            var (engine, ctrl, _) = NewEngine();
            ctrl.DefaultLevel = 42;

            Assert.Equal(42, engine.DefaultLevel);
        }

        [Fact]
        public void Brightness_AppliesMatrix_EvenAtNeutralSaturation()
        {
            var (engine, _, ovl) = NewEngine();
            engine.Vibrance = 80; // clears

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
            engine.Vibrance = 100;

            engine.EyeCare = true;
            AssertMatrix(ColorAdjust.Build(1f, 1f, VibranceEngine.EyeCareWarmth), ovl.Last);

            int clearsBefore = ovl.ClearCalls;
            engine.EyeCare = false;
            Assert.Equal(clearsBefore + 1, ovl.ClearCalls); // back to identity
        }

        [Fact]
        public void Combined_SaturationBrightnessAndEyeCare_ShareOneMatrix()
        {
            var (engine, ctrl, ovl) = NewEngine();

            engine.Saturation = 160;
            engine.Brightness = 90;
            engine.EyeCare = true;
            AssertMatrix(ColorAdjust.Build(1.6f, 0.9f, VibranceEngine.EyeCareWarmth), ovl.Last);
        }

        [Fact]
        public void Gamma_AppliesRamp_AndResetsAt100()
        {
            var (engine, _, _, gamma) = NewEngineFull();

            engine.Gamma = 130;

            Assert.Equal(130, engine.Gamma);
            Assert.Equal(GammaCurve.Build(1.3f), gamma.Last);

            int resetsBefore = gamma.ResetCalls;
            engine.Gamma = 100;
            Assert.Equal(resetsBefore + 1, gamma.ResetCalls); // neutral uses the linear ramp
        }

        [Fact]
        public void Gamma_IsClampedToSafeRange()
        {
            var (engine, _, _, _) = NewEngineFull();

            engine.Gamma = 900;
            Assert.Equal(VibranceEngine.MaxGamma, engine.Gamma);

            engine.Gamma = 1;
            Assert.Equal(VibranceEngine.MinGamma, engine.Gamma);
        }

        [Fact]
        public void Gamma_DoesNotTouchTheColorMatrix()
        {
            var (engine, _, ovl, _) = NewEngineFull();

            engine.Gamma = 120; // gamma lives in the ramp, not the matrix

            Assert.Empty(ovl.Applied);
        }

        [Fact]
        public void Reset_AlsoRestoresGamma()
        {
            var (engine, _, _, gamma) = NewEngineFull();
            engine.Gamma = 60;

            engine.Reset();

            Assert.Equal(100, engine.Gamma);
            Assert.True(gamma.ResetCalls >= 1);
        }

        [Fact]
                public void SuspendOverlay_StopsSubsequentValueWrites_UntilResume()
                {
                    // Post alt-tab fix: ScheduleOverlayApply must respect _overlaySuspended so
                    // the moment PlexusX loses focus and someone calls SuspendOverlay (Clear()),
                    // a value change while focus is elsewhere (popup open over a game, etc.)
                    // cannot silently re-enable the overlay. ResumeOverlay flushes the value
                    // back once focus returns.
                    var (engine, _, ovl) = NewEngine();
                    engine.Saturation = 160;
                    int appliedBeforeSuspend = ovl.Applied.Count;

                    engine.SuspendOverlay();
                    // A value change while suspended must NOT push a new matrix to the overlay.
                    engine.Saturation = 180;
                    Assert.Equal(appliedBeforeSuspend, ovl.Applied.Count);

                    // Resuming flushes the current value once.
                    engine.ResumeOverlay();
                    Assert.True(ovl.Applied.Count > appliedBeforeSuspend);
                }

                [Fact]
                public void VibranceSlider_Drag_EngineUpdates_ValueDuringDrag_FlushOnEndDrag()
                {
                    // Belt-and-braces for the "user drags saturation in popup over a game" path:
                    // BeginDrag suppresses overlay writes during the drag (no per-tick flood),
                    // EndDrag flushes the final value. This is the same flag dance the popup
                    // relies on - locked in here so a future refactor can't break the
                    // chip-tracks-cursor-1:1 contract.
                    var (engine, _, ovl) = NewEngine();
                    engine.BeginDrag();
                    int appliedBefore = ovl.Applied.Count;

                    for (int s = 110; s <= 170; s += 10)
                    {
                        engine.Saturation = s;
                        Assert.Equal(appliedBefore, ovl.Applied.Count); // drag suppresses
                    }

                    engine.EndDrag();
                    Assert.True(ovl.Applied.Count > appliedBefore); // single flush at the end
                }

                [Fact]
                public void Reset_RestoresDriverDefault_AndNeutralAdjustments()
        {
            var (engine, ctrl, ovl) = NewEngine();
            ctrl.DefaultLevel = 50;
            engine.Saturation = 200;
            engine.Brightness = 60;
            engine.EyeCare = true;

            engine.Reset();

            Assert.Equal(50, ctrl.LastSet);
            Assert.Equal(50, engine.Vibrance);
            Assert.Equal(100, engine.Brightness);
            Assert.False(engine.EyeCare);
            Assert.True(ovl.ClearCalls >= 1); // neutral again
        }
    }
}
