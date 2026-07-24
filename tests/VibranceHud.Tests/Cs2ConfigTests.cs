using VibranceHud.Cs2;
using Xunit;

namespace VibranceHud.Tests
{
    public class Cs2ConfigTests
    {
        [Fact]
        public void Get_ReadsAnUnquotedValue()
        {
            var cfg = Cs2Config.Parse("fps_max 0\nr_dynamic 1\n");
            Assert.Equal("0", cfg.Get("fps_max"));
            Assert.Equal("1", cfg.Get("r_dynamic"));
        }

        [Fact]
        public void Get_ReadsAQuotedValue()
        {
            var cfg = Cs2Config.Parse("fps_max \"400\"\n");
            Assert.Equal("400", cfg.Get("fps_max"));
        }

        [Fact]
        public void Get_ReturnsNull_WhenAbsent()
        {
            Assert.Null(Cs2Config.Parse("fps_max 0\n").Get("r_dynamic"));
        }

        [Fact]
        public void Set_UpdatesAnExistingConvar_InPlace()
        {
            var cfg = Cs2Config.Parse("cl_showfps 1\nr_dynamic 1\nrate 786432\n");
            cfg.Set("r_dynamic", "0");

            Assert.Equal("0", cfg.Get("r_dynamic"));
            // The other lines are untouched.
            Assert.Equal("1", cfg.Get("cl_showfps"));
            Assert.Equal("786432", cfg.Get("rate"));
            Assert.Equal(3 + 1, cfg.Serialize().Split('\n').Length); // no new line added (trailing empty)
        }

        [Fact]
        public void Set_AddsANewConvar_WhenMissing()
        {
            var cfg = Cs2Config.Parse("cl_showfps 1\n");
            cfg.Set("r_drawparticles", "0");

            Assert.Equal("0", cfg.Get("r_drawparticles"));
        }

        [Fact]
        public void Set_WritesQuotedValues()
        {
            var cfg = Cs2Config.Parse("");
            cfg.Set("fps_max", "0");
            Assert.Contains("fps_max \"0\"", cfg.Serialize());
        }

        [Fact]
        public void RoundTrip_PreservesUnrelatedLines()
        {
            var original = "// my autoexec\ncl_showfps 1\nsensitivity 1.8\n";
            var cfg = Cs2Config.Parse(original);
            cfg.Set("r_dynamic", "0");

            var result = cfg.Serialize();
            Assert.Contains("// my autoexec", result);
            Assert.Contains("sensitivity 1.8", result);
        }
    }
}
