using System.Collections.Generic;
using System.Linq;
using VibranceHud.Cs2;
using VibranceHud.Games;
using Xunit;

namespace VibranceHud.Tests
{
    public class Cs2Tests
    {
        [Fact]
        public void Catalog_IncludesCs2_WithSteamAppId730()
        {
            var cs2 = SupportedGames.All.Single(g => g.Id == "cs2");
            Assert.Equal(730, cs2.SteamAppId);
            Assert.Equal("Counter-Strike Global Offensive", cs2.InstallFolder);
        }

        [Fact]
        public void AutoexecPath_IsUnderGameCsgoCfg()
        {
            var path = Cs2SettingsService.AutoexecPathFor(@"C:\Games\Counter-Strike Global Offensive");
            Assert.Equal(
                @"C:\Games\Counter-Strike Global Offensive\game\csgo\cfg\autoexec.cfg", path);
        }

        [Fact]
        public void EveryTweak_HasDistinctOnAndOffValues()
        {
            foreach (var tweak in Cs2Tweaks.All)
                foreach (var v in tweak.Values)
                    Assert.NotEqual(v.On, v.Off);
        }

        [Fact]
        public void EveryTweak_HasALabelAndDescription()
        {
            foreach (var t in Cs2Tweaks.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(t.Label));
                Assert.False(string.IsNullOrWhiteSpace(t.Description));
            }
        }

        [Fact]
        public void Tweak_IsOff_AtStockValues_AndOn_AtOptimized()
        {
            var dyn = Cs2Tweaks.All.Single(t => t.Label == "Disable Dynamic Lighting");
            Assert.False(dyn.IsOn(Cs2Config.Parse("r_dynamic 1\n")));
            Assert.True(dyn.IsOn(Cs2Config.Parse("r_dynamic 0\n")));
        }

        [Fact]
        public void Write_On_EmitsOptimizedValue()
        {
            var particles = Cs2Tweaks.All.Single(t => t.Label == "Reduce Particles");
            var changes = new Dictionary<string, string>();
            particles.Write(changes, on: true);
            Assert.Equal("0", changes["r_drawparticles"]);
        }

        [Fact]
        public void LaunchOptions_IncludeExecAutoexec_SoOurEditsActuallyRun()
        {
            Assert.Contains("+exec autoexec.cfg", Cs2LaunchOptions.Recommended);
        }
    }
}
