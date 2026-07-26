using VibranceHud.Fortnite;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Fortnite GameUserSettings.ini handling: INI sections with key=value lines.
    /// Section awareness matters - the same key name can exist in two sections and only
    /// the one in the asked-for section may change. Everything else stays byte-for-byte.
    /// </summary>
    public class FortniteConfigTests
    {
        private const string Sample =
            "[/Script/FortniteGame.FortGameUserSettings]\n" +
            "FrameRateLimit=60.000000\n" +
            "bUseVsync=True\n" +
            "\n" +
            "[ScalabilityGroups]\n" +
            "sg.ShadowsQuality=2\n" +
            "sg.EffectsQuality=2\n";

        [Fact]
        public void Get_reads_key_in_section()
        {
            var cfg = FortniteConfig.Parse(Sample);
            Assert.Equal("60.000000", cfg.Get("/Script/FortniteGame.FortGameUserSettings", "FrameRateLimit"));
        }

        [Fact]
        public void Get_same_key_in_other_section_returns_null()
        {
            var cfg = FortniteConfig.Parse(Sample);
            Assert.Null(cfg.Get("ScalabilityGroups", "FrameRateLimit"));
        }

        [Fact]
        public void Set_existing_key_changes_value_in_that_section_only()
        {
            var cfg = FortniteConfig.Parse(Sample);
            cfg.Set("ScalabilityGroups", "sg.ShadowsQuality", "0");
            Assert.Equal("0", cfg.Get("ScalabilityGroups", "sg.ShadowsQuality"));
            Assert.Equal("60.000000", cfg.Get("/Script/FortniteGame.FortGameUserSettings", "FrameRateLimit"));
            Assert.Contains("bUseVsync=True", cfg.Serialize());
        }

        [Fact]
        public void Set_missing_key_inserts_into_existing_section()
        {
            var cfg = FortniteConfig.Parse(Sample);
            cfg.Set("ScalabilityGroups", "sg.TextureQuality", "0");
            Assert.Equal("0", cfg.Get("ScalabilityGroups", "sg.TextureQuality"));
            // inserted inside the section, not at the end of the file
            var text = cfg.Serialize();
            Assert.True(text.IndexOf("sg.TextureQuality=0") > text.IndexOf("[ScalabilityGroups]"));
        }

        [Fact]
        public void Set_missing_section_appends_section_and_key()
        {
            var cfg = FortniteConfig.Parse(Sample);
            cfg.Set("/Script/Engine.Engine", "bSmoothFrameRate", "false");
            Assert.Equal("false", cfg.Get("/Script/Engine.Engine", "bSmoothFrameRate"));
            Assert.Contains("[/Script/Engine.Engine]", cfg.Serialize());
        }

        [Fact]
        public void Get_missing_section_returns_null()
        {
            var cfg = FortniteConfig.Parse(Sample);
            Assert.Null(cfg.Get("[NoSuch]", "x"));
        }

        [Fact]
        public void Empty_config_accepts_section_and_key()
        {
            var cfg = FortniteConfig.Parse("");
            cfg.Set("ScalabilityGroups", "sg.ShadowsQuality", "0");
            Assert.Equal("0", cfg.Get("ScalabilityGroups", "sg.ShadowsQuality"));
        }
    }
}
