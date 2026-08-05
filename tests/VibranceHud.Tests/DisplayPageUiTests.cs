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
    [Collection("Theme serial")]
    public sealed class DisplayPageUiTests
    {
        [Fact]
        public void Page_ExposesFourCompleteScenePresetsAndSixSliders()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);
            page.Size = new Size(780, 628);
            page.CreateControl();

            var chipLabels = Descendants(page).OfType<PresetChip>().Select(c => c.Caption).ToArray();

            Assert.Equal(new[] { "Balanced", "Forest", "Desert", "Snow" }, chipLabels);

            // By caption rather than by count. This used to assert exactly six sliders, which
            // broke the moment the advanced colour section added eight more - and a raw count
            // never said which six it wanted anyway. These are the controls the page exists
            // for; the advanced ones are checked separately.
            var captions = Descendants(page).OfType<Label>()
                .Select(l => l.Text?.Trim() ?? "")
                .ToHashSet();

            foreach (var expected in new[]
                { "Saturation", "Vibrance", "Brightness", "Gamma", "Contrast", "Temperature" })
                Assert.Contains(expected, captions);

            // The eye-care switch became a point on the Temperature slider. If a ToggleSwitch
            // ever reappears here, the two controls are fighting over the same setting.
            Assert.DoesNotContain(Descendants(page), c => c is ToggleSwitch);
        }

        [Fact]
        public void Every_scene_preset_chip_previews_its_own_look()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);
            page.CreateControl();

            foreach (var chip in Descendants(page).OfType<PresetChip>())
            {
                Assert.False(string.IsNullOrWhiteSpace(chip.Subtitle));
                Assert.False(string.IsNullOrWhiteSpace(chip.Kind));
                // The swatch is a grey ramp pushed through this matrix. Without one the chip
                // silently degrades to a flat neutral bar that says nothing about the preset.
                Assert.NotNull(chip.Matrix);
                Assert.Equal(25, chip.Matrix!.Length);
                // The photo is an embedded resource; a wrong name returns null silently and
                // the chip loses its backdrop without anything failing. Named in the message
                // because "value is null" alone does not say which of the four is missing.
                Assert.True(chip.Photo != null,
                    $"no embedded art for chip '{chip.Kind}' (expected resource preset-{chip.Kind}.png)");
            }
        }

        [Fact]
        public void The_biome_chips_do_not_all_preview_the_same_colour()
        {
            // Each swatch is derived from the preset's own numbers, so identical swatches
            // would mean the presets had stopped differing in tone.
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);
            page.CreateControl();

            var signatures = Descendants(page).OfType<PresetChip>()
                .Select(c => string.Join(",", c.Matrix!))
                .Distinct()
                .Count();

            Assert.Equal(4, signatures);
        }

        [Fact]
        public void Nothing_on_the_card_is_laid_out_past_the_edge_of_the_card()
        {
            // The real guarantee. The card draws a rounded surface and clips its children, so
            // anything positioned past its bounds is simply sliced off - which is exactly how
            // the second hotkey picker ended up cut in half.
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);
            page.Size = new Size(780, 628);
            page.CreateControl();
            InvokeLayout(page);

            var card = page.Controls.OfType<CardPanel>().Single();
            foreach (Control child in card.Controls)
            {
                Assert.True(child.Left >= 0, $"{Describe(child)} starts left of the card: {child.Bounds}");
                Assert.True(child.Top >= 0, $"{Describe(child)} starts above the card: {child.Bounds}");
                Assert.True(child.Right <= card.ClientSize.Width,
                    $"{Describe(child)} clips right: {child.Bounds} vs card {card.ClientSize}");
                Assert.True(child.Bottom <= card.ClientSize.Height,
                    $"{Describe(child)} clips at the bottom: {child.Bounds} vs card {card.ClientSize}");
            }
        }

        [Fact]
        public void The_page_can_scroll_to_everything_it_lays_out()
        {
            // Vertical overflow is allowed now - the page scrolls. What is not allowed is
            // overflowing without the scroll extent to reach it, which is invisible content.
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);
            page.Size = new Size(780, 500);   // deliberately too short
            page.CreateControl();
            InvokeLayout(page);

            int lowest = page.Controls.Cast<Control>().Max(c => c.Bottom);
            Assert.True(page.AutoScroll, "the Display page has to scroll or tall content is unreachable");
            Assert.True(page.AutoScrollMinSize.Height >= lowest,
                $"scroll extent {page.AutoScrollMinSize.Height} doesn't reach the lowest control at {lowest}");
        }

        [Fact]
        public void Two_column_rows_never_overlap_each_other()
        {
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path);
            page.Size = new Size(780, 628);
            page.CreateControl();
            InvokeLayout(page);

            var sliders = Descendants(page).OfType<TwoColorSlider>().ToList();
            for (int i = 0; i < sliders.Count; i++)
                for (int j = i + 1; j < sliders.Count; j++)
                    Assert.False(sliders[i].Bounds.IntersectsWith(sliders[j].Bounds),
                        $"two sliders overlap: {sliders[i].Bounds} and {sliders[j].Bounds}");
        }

        [Theory]
        [InlineData(0, "Neutral")]
        [InlineData(-40, "Cool 40")]
        [InlineData(25, "Warm 25")]
        public void Temperature_reads_as_a_direction_not_a_signed_number(int value, string expected) =>
            Assert.Equal(expected, VibrancePage.TemperatureText(value));

        private static string Describe(Control c) =>
            c is Label l ? $"Label(\"{l.Text}\")" : c.GetType().Name;

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        private static VibrancePage BuildPage(string directory)
        {
            Theme.Apply("Violet");
            var engine = new VibranceEngine(new Controller(), new Overlay(), new Gamma());
            return new VibrancePage(engine, new AppSettings(), new SettingsStore(directory));
        }

        private static void InvokeLayout(VibrancePage page) =>
            typeof(VibrancePage).GetMethod("LayoutContent", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(page, null);

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
                System.IO.Path.GetTempPath(), "PlexusXDisplayUI_" + Guid.NewGuid().ToString("N"));

            public void Dispose()
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
        }
    }
}
