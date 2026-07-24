using System.Linq;
using VibranceHud.Cs2;
using VibranceHud.Rust;
using Xunit;

namespace VibranceHud.Tests
{
    public class PresetsTests
    {
        [Fact]
        public void Rust_Competitive_IsLowerQualityThanCinematic()
        {
            Assert.True(RustPresets.Competitive.Quality < RustPresets.Cinematic.Quality);
        }

        [Fact]
        public void Rust_Competitive_TurnsOnThePerformanceTweaks()
        {
            Assert.Contains("Disable Gibs", RustPresets.Competitive.TweaksOn);
            Assert.Contains("VSync Off", RustPresets.Competitive.TweaksOn);
        }

        [Fact]
        public void Rust_Cinematic_LeavesVisualsOn()
        {
            Assert.Empty(RustPresets.Cinematic.TweaksOn);
        }

        [Fact]
        public void Rust_PresetTweakNames_AllExistInTheTweakCatalog()
        {
            var known = RustTweaks.All.Select(t => t.Label).ToHashSet();
            foreach (var preset in RustPresets.All)
                foreach (var name in preset.TweaksOn)
                    Assert.Contains(name, known); // no preset references an invented tweak
        }

        [Fact]
        public void Cs2_Competitive_TurnsTweaksOn_Cinematic_Off()
        {
            Assert.True(Cs2Presets.Competitive.AllTweaksOn);
            Assert.False(Cs2Presets.Cinematic.AllTweaksOn);
        }

        [Fact]
        public void EveryPreset_HasNameAndDescription()
        {
            foreach (var p in RustPresets.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(p.Name));
                Assert.False(string.IsNullOrWhiteSpace(p.Description));
            }
            foreach (var p in Cs2Presets.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(p.Name));
                Assert.False(string.IsNullOrWhiteSpace(p.Description));
            }
        }
    }
}
