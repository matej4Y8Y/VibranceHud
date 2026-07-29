// Guards the "slider feels laggy while dragging" fixes.
//
// A drag generates a ValueChanged per mouse-move - easily 100+ per second. Anything
// expensive on that path (a driver call, a gamma-ramp syscall, a full-scene repaint)
// turns into visible stutter. These tests count the expensive calls across a simulated
// drag so a future change can't quietly reintroduce the cost.
//
// The counts asserted here are the *point* of the tests, not incidental: during a drag
// the engine should issue ZERO driver/gamma writes, then exactly one flush on EndDrag.

using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class SliderDragPerformanceTests
    {
        private sealed class CountingController : IVibranceController
        {
            public int SetLevelCalls { get; private set; }
            public int LastLevel { get; private set; } = -1;
            public bool IsAvailable => true;
            public int DefaultLevel => 50;
            public int CurrentLevel => LastLevel < 0 ? DefaultLevel : LastLevel;
            public void SetLevel(int level)
            {
                SetLevelCalls++;
                LastLevel = level;
            }
        }

        private sealed class CountingGammaRamp : IGammaRamp
        {
            public int ApplyCalls { get; private set; }
            public int ResetCalls { get; private set; }
            public void Apply(ushort[] ramp) => ApplyCalls++;
            public void Reset() => ResetCalls++;
        }

        private sealed class CountingOverlay : ISaturationOverlay
        {
            public int ApplyCalls { get; private set; }
            public int ClearCalls { get; private set; }
            public void Apply(float[] matrix) => ApplyCalls++;
            public void Clear() => ClearCalls++;
        }

        private static (VibranceEngine engine, CountingController ctrl,
                        CountingGammaRamp gamma, CountingOverlay overlay) NewEngine()
        {
            var ctrl = new CountingController();
            var gamma = new CountingGammaRamp();
            var overlay = new CountingOverlay();
            return (new VibranceEngine(ctrl, overlay, gamma), ctrl, gamma, overlay);
        }

        /// <summary>Dragging SATURATION must not touch the NVIDIA driver at all - the
        /// driver's Digital Vibrance level is unchanged by a saturation move, so every
        /// SetLevel call during that drag is pure wasted latency.</summary>
        [Fact]
        public void DraggingSaturation_MakesNoDriverCalls()
        {
            var (engine, ctrl, _, _) = NewEngine();
            engine.Vibrance = 50;
            int before = ctrl.SetLevelCalls;

            engine.BeginDrag();
            for (int v = 100; v <= 160; v++) engine.Saturation = v;
            engine.EndDrag();

            Assert.Equal(before, ctrl.SetLevelCalls);
        }

        /// <summary>Dragging BRIGHTNESS must not touch the driver either.</summary>
        [Fact]
        public void DraggingBrightness_MakesNoDriverCalls()
        {
            var (engine, ctrl, _, _) = NewEngine();
            engine.Vibrance = 50;
            int before = ctrl.SetLevelCalls;

            engine.BeginDrag();
            for (int v = 100; v <= 140; v++) engine.Brightness = v;
            engine.EndDrag();

            Assert.Equal(before, ctrl.SetLevelCalls);
        }

        /// <summary>Dragging GAMMA must not hit SetDeviceGammaRamp on every move - that
        /// syscall is slow enough to be felt. One flush at the end is correct.</summary>
        [Fact]
        public void DraggingGamma_DefersTheRampWriteToEndDrag()
        {
            var (engine, _, gamma, _) = NewEngine();

            engine.BeginDrag();
            for (int v = 100; v <= 140; v++) engine.Gamma = v;
            int duringDrag = gamma.ApplyCalls;
            engine.EndDrag();

            Assert.Equal(0, duringDrag);
            Assert.Equal(1, gamma.ApplyCalls);
        }

        /// <summary>The gamma value the user released on is the one that gets applied.</summary>
        [Fact]
        public void EndDrag_AppliesTheFinalGammaValue()
        {
            var (engine, _, gamma, _) = NewEngine();

            engine.BeginDrag();
            for (int v = 100; v <= 133; v++) engine.Gamma = v;
            engine.EndDrag();

            Assert.Equal(133, engine.Gamma);
            Assert.Equal(1, gamma.ApplyCalls);
        }

        /// <summary>Gamma back at exactly 100 must Reset the ramp, not Apply a curve -
        /// otherwise "return to neutral" leaves a near-identity ramp installed.</summary>
        [Fact]
        public void EndDrag_AtNeutralGamma_ResetsInsteadOfApplying()
        {
            var (engine, _, gamma, _) = NewEngine();
            engine.Gamma = 130; // start off-neutral, applied immediately (no drag)
            int applyBefore = gamma.ApplyCalls;
            int resetBefore = gamma.ResetCalls;

            engine.BeginDrag();
            for (int v = 129; v >= 100; v--) engine.Gamma = v;
            engine.EndDrag();

            Assert.Equal(applyBefore, gamma.ApplyCalls);
            Assert.Equal(resetBefore + 1, gamma.ResetCalls);
        }

        /// <summary>Dragging VIBRANCE does still need the driver, but only once at the
        /// end - not once per mouse-move.</summary>
        [Fact]
        public void DraggingVibrance_FlushesTheDriverOnceOnEndDrag()
        {
            var (engine, ctrl, _, _) = NewEngine();
            engine.Vibrance = 0;
            int before = ctrl.SetLevelCalls;

            engine.BeginDrag();
            for (int v = 1; v <= 80; v++) engine.Vibrance = v;
            int duringDrag = ctrl.SetLevelCalls - before;
            engine.EndDrag();

            Assert.Equal(0, duringDrag);
            Assert.Equal(before + 1, ctrl.SetLevelCalls);
            // Driver caps at its ceiling; 80 is below it so it lands exactly.
            Assert.Equal(80, ctrl.LastLevel);
        }

        /// <summary>Outside a drag, a single change still applies immediately - the
        /// deferral must not make normal clicks feel unresponsive.</summary>
        [Fact]
        public void WithoutADrag_ChangesApplyImmediately()
        {
            var (engine, ctrl, gamma, overlay) = NewEngine();

            engine.Vibrance = 70;
            Assert.True(ctrl.SetLevelCalls >= 1);

            engine.Gamma = 120;
            Assert.Equal(1, gamma.ApplyCalls);

            int overlayBefore = overlay.ApplyCalls + overlay.ClearCalls;
            engine.Saturation = 150;
            Assert.True(overlay.ApplyCalls + overlay.ClearCalls > overlayBefore);
        }

        /// <summary>EndDrag with nothing pending must not fire spurious writes.</summary>
        [Fact]
        public void EndDrag_WithNoChanges_DoesNotWriteAnything()
        {
            var (engine, ctrl, gamma, _) = NewEngine();
            engine.Vibrance = 50;
            engine.Gamma = 100;
            int ctrlBefore = ctrl.SetLevelCalls;
            int gammaBefore = gamma.ApplyCalls + gamma.ResetCalls;

            engine.BeginDrag();
            engine.EndDrag();

            Assert.Equal(ctrlBefore, ctrl.SetLevelCalls);
            Assert.Equal(gammaBefore, gamma.ApplyCalls + gamma.ResetCalls);
        }
    }
}
