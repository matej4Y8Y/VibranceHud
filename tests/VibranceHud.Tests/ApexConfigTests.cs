using VibranceHud.Apex;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Apex videoconfig.txt handling: lines of "setting.key"  "value". The parser must
    /// preserve every line it wasn't asked to touch byte-for-byte (same contract as RustConfig).
    /// </summary>
    public class ApexConfigTests
    {
        private const string Sample =
            "\"setting.csm_enabled\"\t\t\"1\"\n" +
            "\"setting.fps_max\"\t\t\"144\"\n" +
            "\"setting.mat_antialias_mode\"\t\t\"12\"\n";

        [Fact]
        public void Get_reads_existing_value()
        {
            var cfg = ApexConfig.Parse(Sample);
            Assert.Equal("144", cfg.Get("setting.fps_max"));
        }

        [Fact]
        public void Get_missing_key_returns_null()
        {
            var cfg = ApexConfig.Parse(Sample);
            Assert.Null(cfg.Get("setting.dvs_enable"));
        }

        [Fact]
        public void Set_existing_key_changes_only_the_value()
        {
            var cfg = ApexConfig.Parse(Sample);
            cfg.Set("setting.fps_max", "0");
            Assert.Equal("0", cfg.Get("setting.fps_max"));
            // untouched lines survive byte-for-byte
            Assert.Contains("\"setting.csm_enabled\"\t\t\"1\"", cfg.Serialize());
            Assert.Contains("\"setting.mat_antialias_mode\"\t\t\"12\"", cfg.Serialize());
        }

        [Fact]
        public void Set_missing_key_appends_a_new_line()
        {
            var cfg = ApexConfig.Parse(Sample);
            cfg.Set("setting.dvs_enable", "0");
            Assert.Equal("0", cfg.Get("setting.dvs_enable"));
        }

        [Fact]
        public void Empty_config_round_trips_and_accepts_keys()
        {
            var cfg = ApexConfig.Parse("");
            Assert.Equal("", cfg.Serialize());
            cfg.Set("setting.fps_max", "0");
            Assert.Equal("0", cfg.Get("setting.fps_max"));
        }

        [Fact]
        public void File_ending_in_newline_stays_ending_in_newline()
        {
            var cfg = ApexConfig.Parse("\"setting.fps_max\"\t\t\"144\"\n");
            cfg.Set("setting.csm_enabled", "0");
            Assert.EndsWith("\n", cfg.Serialize());
            Assert.DoesNotContain("\n\n", cfg.Serialize());
        }

        [Fact]
        public void Key_match_is_case_insensitive()
        {
            var cfg = ApexConfig.Parse(Sample);
            Assert.Equal("144", cfg.Get("SETTING.FPS_MAX"));
        }
    }
}
