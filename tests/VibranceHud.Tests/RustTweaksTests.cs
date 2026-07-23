using System.Collections.Generic;
using System.Linq;
using VibranceHud.Rust;
using Xunit;

namespace VibranceHud.Tests
{
    public class RustTweaksTests
    {
        private const string Cfg =
            "effects.maxgibs \"-1\"\n" +
            "global.showblood \"True\"\n" +
            "grass.quality \"2\"\n" +
            "grass.displacement \"True\"\n" +
            "legs.enablelegs \"True\"\n";

        [Fact]
        public void Tweak_IsOff_WhenConvarsSitAtStockValues()
        {
            var cfg = RustConfig.Parse(Cfg);
            var gibs = RustTweaks.All.Single(t => t.Label == "Disable Gibs");

            Assert.False(gibs.IsOn(cfg));
        }

        [Fact]
        public void Tweak_IsOn_WhenConvarsSitAtOptimizedValues()
        {
            var cfg = RustConfig.Parse("effects.maxgibs \"0\"\n");
            var gibs = RustTweaks.All.Single(t => t.Label == "Disable Gibs");

            Assert.True(gibs.IsOn(cfg));
        }

        [Fact]
        public void MultiConvarTweak_NeedsEveryConvarOptimized()
        {
            var grass = RustTweaks.All.Single(t => t.Label == "Low Grass Quality");

            // Only one of the two convars optimized -> still off.
            Assert.False(grass.IsOn(RustConfig.Parse("grass.quality \"0\"\ngrass.displacement \"True\"\n")));
            Assert.True(grass.IsOn(RustConfig.Parse("grass.quality \"0\"\ngrass.displacement \"False\"\n")));
        }

        [Fact]
        public void Write_On_EmitsOptimizedValues_ForEveryConvar()
        {
            var grass = RustTweaks.All.Single(t => t.Label == "Low Grass Quality");
            var changes = new Dictionary<string, string>();

            grass.Write(changes, on: true);

            Assert.Equal("0", changes["grass.quality"]);
            Assert.Equal("False", changes["grass.displacement"]);
        }

        [Fact]
        public void Write_Off_RestoresStockValues()
        {
            var blood = RustTweaks.All.Single(t => t.Label == "Disable Blood");
            var changes = new Dictionary<string, string>();

            blood.Write(changes, on: false);

            Assert.Equal("True", changes["global.showblood"]);
        }

        [Fact]
        public void EveryTweak_HasDistinctOnAndOffValues()
        {
            foreach (var tweak in RustTweaks.All)
                foreach (var v in tweak.Values)
                    Assert.NotEqual(v.On, v.Off);
        }
    }
}
