using VibranceHud.Rust;
using Xunit;

namespace VibranceHud.Tests
{
    public class RustConfigTests
    {
        // Explicit \n so the tests don't depend on the source file's line endings.
        private const string Sample =
            "effects.bloom \"False\"\n" +
            "effects.motionblur \"True\"\n" +
            "fps.limit \"144\"\n" +
            "graphics.quality \"5\"\n";

        [Fact]
        public void RoundTrip_PreservesExactText()
        {
            Assert.Equal(Sample, RustConfig.Parse(Sample).Serialize());
        }

        [Fact]
        public void Get_ReturnsConvarValue()
        {
            var cfg = RustConfig.Parse(Sample);

            Assert.Equal("144", cfg.Get("fps.limit"));
            Assert.Equal("True", cfg.Get("effects.motionblur"));
        }

        [Fact]
        public void Get_MissingConvar_ReturnsNull()
        {
            Assert.Null(RustConfig.Parse(Sample).Get("does.notexist"));
        }

        [Fact]
        public void Set_ExistingConvar_ChangesOnlyThatValue()
        {
            var cfg = RustConfig.Parse(Sample);

            cfg.Set("fps.limit", "240");

            Assert.Equal("240", cfg.Get("fps.limit"));
            // Everything else is untouched.
            Assert.Equal("False", cfg.Get("effects.bloom"));
            Assert.Equal("True", cfg.Get("effects.motionblur"));
            Assert.Equal(
                "effects.bloom \"False\"\n" +
                "effects.motionblur \"True\"\n" +
                "fps.limit \"240\"\n" +
                "graphics.quality \"5\"\n",
                cfg.Serialize());
        }

        [Fact]
        public void Set_MissingConvar_AppendsLine()
        {
            var cfg = RustConfig.Parse("fps.limit \"144\"\n");

            cfg.Set("effects.gibs", "False");

            Assert.Equal("False", cfg.Get("effects.gibs"));
            // Appended on its own line; the file's trailing newline is preserved
            // (no stray blank line introduced).
            Assert.Equal("fps.limit \"144\"\neffects.gibs \"False\"\n", cfg.Serialize());
        }

        [Fact]
        public void Set_PreservesCarriageReturns()
        {
            var cfg = RustConfig.Parse("fps.limit \"144\"\r\ngraphics.quality \"5\"\r\n");

            cfg.Set("fps.limit", "240");

            Assert.Equal("fps.limit \"240\"\r\ngraphics.quality \"5\"\r\n", cfg.Serialize());
        }
    }
}
