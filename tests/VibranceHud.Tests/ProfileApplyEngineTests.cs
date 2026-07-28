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

    internal sealed class FakeGameHubApplier : IGameHubApplier
    {
        public List<string> Applied { get; } = new();
        public void Apply(string gameId, GameHubOptions opts) => Applied.Add(gameId);
    }

    public class ProfileApplyEngineTests
    {
        [Fact]
        public void Apply_ThenRestore_RoundTripsValues()
        {
            var v = new FakeVibranceEngine();
            var hub = new FakeGameHubApplier();
            var engine = new ProfileApplyEngine(v, hub);
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
            Assert.Single(hub.Applied);

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
            var hub = new FakeGameHubApplier();
            var engine = new ProfileApplyEngine(v, hub);
            engine.ApplyAsync("rust").Wait();
            Assert.Equal(100, v.Vibrance); // untouched
            Assert.Empty(hub.Applied);
        }

        [Fact]
        public void RestoreAsync_WithoutApply_IsNoOp()
        {
            var v = new FakeVibranceEngine { Vibrance = 77 };
            var engine = new ProfileApplyEngine(v, new FakeGameHubApplier());
            engine.RestoreAsync().Wait();
            Assert.Equal(77, v.Vibrance);
        }
    }
}