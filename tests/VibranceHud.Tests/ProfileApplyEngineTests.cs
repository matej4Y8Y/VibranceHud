using System.Collections.Generic;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    internal sealed class FakeVibranceEngine : IVibranceEngine
    {
            // Drag begin/end are UI-lifecycle hooks; a fake has no state to guard.
            public void BeginDrag() { }
            public void EndDrag() { }

            // Focus-overlay hooks: no state to guard in a fake.
            public void SuspendOverlay() { }
            public void ResumeOverlay() { }

        public int Vibrance { get; set; } = 100;
        public int Saturation { get; set; } = 100;
        public int Brightness { get; set; } = 100;
        public int Gamma { get; set; } = 100;
    }

    /// <summary>
    /// The per-game profile engine, now colour-only. It used to also push settings into the
    /// game's own config file through an IGameHubApplier; that went with the Games Hub,
    /// because writing to a game's files is under the hood and PlexusX only changes what the
    /// monitor shows.
    /// </summary>
    public class ProfileApplyEngineTests
    {
        [Fact]
        public void Apply_ThenRestore_RoundTripsValues()
        {
            var v = new FakeVibranceEngine();
            var engine = new ProfileApplyEngine(v);
            engine.SetCurrent(new GameProfile
            {
                GameId = "rust",
                Vibrance = 50,
                Saturation = 200,
                Brightness = 75,
                Gamma = 125,
            });

            // Desktop defaults at (100, 100, 100, 100)
            engine.ApplyAsync("rust").Wait();

            Assert.Equal(50, v.Vibrance);
            Assert.Equal(200, v.Saturation);
            Assert.Equal(75, v.Brightness);
            Assert.Equal(125, v.Gamma);

            engine.RestoreAsync().Wait();

            // Full restoration - spec says we put every slider back, not just the ones we changed.
            Assert.Equal(100, v.Vibrance);
            Assert.Equal(100, v.Saturation);
            Assert.Equal(100, v.Brightness);
            Assert.Equal(100, v.Gamma);
        }

        [Fact]
        public void ApplyAsync_WithoutSetCurrent_IsNoOp()
        {
            var v = new FakeVibranceEngine();
            var engine = new ProfileApplyEngine(v);
            engine.ApplyAsync("rust").Wait();
            Assert.Equal(100, v.Vibrance); // untouched
        }

        [Fact]
        public void RestoreAsync_WithoutApply_IsNoOp()
        {
            var v = new FakeVibranceEngine { Vibrance = 77 };
            var engine = new ProfileApplyEngine(v);
            engine.RestoreAsync().Wait();
            Assert.Equal(77, v.Vibrance);
        }
    }
}