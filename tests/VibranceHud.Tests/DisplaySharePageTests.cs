using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using VibranceHud;
using VibranceHud.Pages;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Share codes on the Display page.
    ///
    /// They used to live three cards down the Settings page, which is nowhere near the
    /// sliders they describe. That matters commercially rather than cosmetically: a code
    /// passed between friends is how this app spreads, so a feature nobody finds is a growth
    /// loop that never starts.
    ///
    /// The thing these tests actually guard is the move itself. Applying a code has to drive
    /// the page's sliders, not just the engine - on Settings there were no sliders to keep in
    /// step, and lifting the old code across unchanged would have left the screen changing
    /// while every control on the page still showed the old numbers.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class DisplaySharePageTests
    {
        [Fact]
        public void TheDisplayPageCarriesTheShareControls()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out _);

            Assert.Single(Descendants(page).OfType<TextBox>());
            Assert.Contains(Descendants(page).OfType<GlassButton>(),
                b => b.Text.Contains("Copy", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(Descendants(page).OfType<GlassButton>(),
                b => b.Text.Equals("Apply", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void CopyingProducesADecodableCodeForTheCurrentLook()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out var engine);

            engine.Saturation = 150;
            engine.Vibrance = 80;
            engine.Contrast = 110;

            Invoke(page, "CopyMyCode");

            var box = Descendants(page).OfType<TextBox>().Single();
            Assert.True(ProfileCode.TryDecode(box.Text, out var decoded),
                $"the box should hold a valid code, got '{box.Text}'");

            Assert.Equal(150, decoded.Saturation);
            Assert.Equal(80, decoded.Vibrance);
            Assert.Equal(110, decoded.Contrast);
        }

        /// <summary>The whole point of moving it here: the sliders have to follow.</summary>
        [Fact]
        public void ApplyingACodeMovesTheSlidersNotJustTheEngine()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out var engine);

            var code = ProfileCode.Encode(new ProfileCode(
                Vibrance: 70, Saturation: 180, Brightness: 96, Gamma: 108,
                Contrast: 115, Temperature: -30));

            Descendants(page).OfType<TextBox>().Single().Text = code;
            Invoke(page, "ApplyCode");

            Assert.Equal(180, engine.Saturation);
            Assert.Equal(70, engine.Vibrance);
            Assert.Equal(-30, engine.Temperature);

            // Every slider on the page must read back the applied value.
            var sliders = Descendants(page).OfType<TwoColorSlider>().Select(s => s.Value).ToList();
            Assert.Contains(180, sliders);
            Assert.Contains(70, sliders);
            Assert.Contains(-30, sliders);
        }

        [Fact]
        public void ARoundTripThroughTheBoxIsLossless()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out var engine);

            engine.Saturation = 210;
            engine.Vibrance = 45;
            engine.Brightness = 92;
            engine.Contrast = 121;
            engine.Temperature = 40;

            Invoke(page, "CopyMyCode");
            Invoke(page, "ApplyCode");

            Assert.Equal(210, engine.Saturation);
            Assert.Equal(45, engine.Vibrance);
            Assert.Equal(92, engine.Brightness);
            Assert.Equal(121, engine.Contrast);
            Assert.Equal(40, engine.Temperature);
        }

        /// <summary>
        /// A mistyped code must change nothing at all. Half-applying lands somebody on a
        /// stranger's screen with no way of knowing which half took.
        /// </summary>
        [Fact]
        public void ABadCodeChangesNothing()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out var engine);

            engine.Saturation = 130;
            engine.Vibrance = 60;

            Descendants(page).OfType<TextBox>().Single().Text = "PX-NOTAREALCODE";
            Invoke(page, "ApplyCode");

            Assert.Equal(130, engine.Saturation);
            Assert.Equal(60, engine.Vibrance);
        }

        [Fact]
        public void AnEmptyBoxIsRejectedQuietlyRatherThanThrowing()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out var engine);
            engine.Saturation = 125;

            Descendants(page).OfType<TextBox>().Single().Text = "";
            Invoke(page, "ApplyCode");

            Assert.Equal(125, engine.Saturation);
        }

        // ---- helpers ----------------------------------------------------------------

        private static VibrancePage BuildPage(string directory, out VibranceEngine engine)
        {
            Theme.Apply("Violet");
            engine = new VibranceEngine(new Controller(), new Overlay(), new Gamma());
            var page = new VibrancePage(engine, new AppSettings(), new SettingsStore(directory));
            page.Size = new Size(830, 628);
            page.CreateControl();
            return page;
        }

        private static void Invoke(VibrancePage page, string method) =>
            typeof(VibrancePage)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(page, null);

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        private sealed class Controller : IVibranceController
        {
            public int CurrentLevel { get; set; } = 50;
            public int DefaultLevel => 50;
            public bool IsAvailable => true;
            public void SetLevel(int level) => CurrentLevel = level;
        }

        private sealed class Overlay : ISaturationOverlay
        {
            public void Apply(float[] matrix) { }
            public void Clear() { }
        }

        private sealed class Gamma : IGammaRamp
        {
            public void Apply(ushort[] ramp) { }
            public void Reset() { }
        }

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "PlexusXShare_" + Guid.NewGuid().ToString("N"));

            public void Dispose()
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
        }
    }
}
