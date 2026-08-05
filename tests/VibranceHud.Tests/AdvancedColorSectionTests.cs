using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using VibranceHud;
using VibranceHud.Capabilities;
using VibranceHud.Design;
using VibranceHud.Pages;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The advanced colour section on the Display page.
    ///
    /// Every control in it resolves to the display gamma ramp, so the section is only honest
    /// if it knows whether that ramp works on this machine. Without that, on a PC where
    /// Windows refuses or clamps the ramp, all eight sliders move, update their readouts, and
    /// change nothing at all - and the app says nothing. That is the failure mode these tests
    /// exist to prevent.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class AdvancedColorSectionTests : IDisposable
    {
        private readonly MachineCapabilities _original = Machine.Current;

        public void Dispose() => Machine.OverrideForTests(_original);

        [Fact]
        public void StartsCollapsedSoTheDefaultPageIsUnchanged()
        {
            Working();
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out _);

            Assert.False(Section(page).Expanded);
        }

        [Fact]
        public void OpeningItMakesTheSectionTaller()
        {
            Working();
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out _);
            var section = Section(page);

            int closed = section.PreferredHeight;
            section.Expanded = true;

            Assert.True(section.PreferredHeight > closed * 2,
                $"expanded height {section.PreferredHeight} is barely more than collapsed {closed}");
        }

        [Fact]
        public void ValuesRoundTrip()
        {
            Working();
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out _);
            var section = Section(page);

            var grade = new ToneSettings(
                Highlights: 40, Shadows: -30, Whites: 20, Blacks: -15, Fade: 25,
                ShadowTint: -60, MidtoneTint: 10, HighlightTint: 55);

            section.Tone = grade;

            var back = section.Tone;
            Assert.Equal(40, back.Highlights);
            Assert.Equal(-30, back.Shadows);
            Assert.Equal(20, back.Whites);
            Assert.Equal(-15, back.Blacks);
            Assert.Equal(25, back.Fade);
            Assert.Equal(-60, back.ShadowTint);
            Assert.Equal(10, back.MidtoneTint);
            Assert.Equal(55, back.HighlightTint);
        }

        /// <summary>Loading a saved grade must fire once, not once per slider - otherwise it
        /// writes the settings file eight times on every page load.</summary>
        [Fact]
        public void LoadingAGradeRaisesChangedExactlyOnce()
        {
            Working();
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out _);
            var section = Section(page);

            int fired = 0;
            section.ToneChanged += (_, _) => fired++;

            section.Tone = new ToneSettings(Highlights: 30, Shadows: -20, Fade: 15);

            Assert.Equal(1, fired);
        }

        [Fact]
        public void ResettingOneGroupLeavesTheOtherAlone()
        {
            Working();
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out _);
            var section = Section(page);

            section.Tone = new ToneSettings(Highlights: 80, Shadows: 60, ShadowTint: 40);
            section.ResetTone();

            Assert.Equal(0, section.Tone.Highlights);
            Assert.Equal(0, section.Tone.Shadows);
            Assert.Equal(40, section.Tone.ShadowTint);   // a different group
        }

        // ---- the part that matters ------------------------------------------------------

        [Fact]
        public void OnAMachineThatRefusesTheRampTheControlsAreDisabledAndExplained()
        {
            Machine.OverrideForTests(new MachineCapabilities(GammaRamp: GammaSupport.Refused));

            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out _);
            var section = Section(page);
            section.Expanded = true;

            Assert.False(section.Usable);

            // Every slider in the section must be dead rather than pretending.
            var advancedSliders = Descendants(page).OfType<TwoColorSlider>()
                .Where(s => !s.Enabled).ToList();
            Assert.True(advancedSliders.Count >= 8,
                $"expected the 8 advanced sliders disabled, found {advancedSliders.Count}");

            // And the page has to say why, on screen.
            var shown = Descendants(page).OfType<Label>().Select(l => l.Text ?? "");
            Assert.Contains(shown, t => t.Contains("won't do anything", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void HdrIsNamedWhenHdrIsTheReason()
        {
            Machine.OverrideForTests(
                new MachineCapabilities(GammaRamp: GammaSupport.Refused, HdrActive: true));

            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out _);

            var shown = Descendants(page).OfType<Label>().Select(l => l.Text ?? "");
            Assert.Contains(shown, t => t.Contains("HDR", StringComparison.Ordinal));
        }

        /// <summary>Clamped still works, so the controls stay live - they are just weaker than
        /// their numbers suggest. Disabling them would remove a feature that does function.</summary>
        [Fact]
        public void OnAClampedMachineTheControlsStayLive()
        {
            Machine.OverrideForTests(new MachineCapabilities(GammaRamp: GammaSupport.Clamped));

            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out _);
            var section = Section(page);
            section.Expanded = true;

            Assert.True(section.Usable);
            Assert.All(Descendants(page).OfType<TwoColorSlider>(), s => Assert.True(s.Enabled));

            var shown = Descendants(page).OfType<Label>().Select(l => l.Text ?? "");
            Assert.Contains(shown, t => t.Contains("weaker", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void AWorkingMachineIsToldNothing()
        {
            Working();
            using var temp = new TempDirectory();
            using var page = BuildPage(temp.Path, out _);

            var shown = Descendants(page).OfType<Label>().Select(l => l.Text ?? "").ToList();
            Assert.DoesNotContain(shown, t => t.Contains("won't do anything", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(shown, t => t.Contains("weaker", StringComparison.OrdinalIgnoreCase));
        }

        // ---- layout ---------------------------------------------------------------------

        /// <summary>
        /// Opened, at every scale factor, nothing may land on top of anything else. This is
        /// the check that would have caught the Reset button painting under its own label.
        /// </summary>
        [Theory]
        [InlineData(96)]
        [InlineData(144)]
        [InlineData(192)]
        public void OpenedTheCardHasNoOverlappingControls(int dpi)
        {
            Working();
            int originalDpi = Tokens.Dpi;
            try
            {
                Tokens.Dpi = dpi;
                Fonts.Rebuild();

                using var temp = new TempDirectory();
                using var page = BuildPage(temp.Path, out _, dpi);
                Section(page).Expanded = true;
                InvokeLayout(page);

                var card = Descendants(page).OfType<CardPanel>().First();
                var kids = card.Controls.Cast<Control>()
                    .Where(c => c.Visible && c.Width > 0 && c.Height > 0).ToList();

                for (int i = 0; i < kids.Count; i++)
                    for (int j = i + 1; j < kids.Count; j++)
                    {
                        var a = kids[i].Bounds;
                        a.Intersect(kids[j].Bounds);
                        Assert.False(a.Width > 2 && a.Height > 2,
                            $"at {dpi} DPI '{Describe(kids[i])}' overlaps '{Describe(kids[j])}'");
                    }
            }
            finally
            {
                Tokens.Dpi = originalDpi;
                Fonts.Rebuild();
            }
        }

        // ---- helpers ---------------------------------------------------------------------

        private static void Working() =>
            Machine.OverrideForTests(new MachineCapabilities(GammaRamp: GammaSupport.Working));

        private static AdvancedColorSection Section(VibrancePage page) =>
            (AdvancedColorSection)typeof(VibrancePage)
                .GetField("_advanced", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(page)!;

        private static VibrancePage BuildPage(string directory, out VibranceEngine engine, int dpi = 96)
        {
            Theme.Apply("Violet");
            engine = new VibranceEngine(new Controller(), new Overlay(), new Gamma());
            var page = new VibrancePage(engine, new AppSettings(), new SettingsStore(directory));
            page.Size = new Size(Tokens.ScaleAt(dpi, 830), Tokens.ScaleAt(dpi, 628));
            page.CreateControl();
            InvokeLayout(page);
            return page;
        }

        private static void InvokeLayout(VibrancePage page) =>
            typeof(VibrancePage).GetMethod("LayoutContent", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(page, null);

        private static string Describe(Control c) =>
            (string.IsNullOrWhiteSpace(c.Text) ? c.GetType().Name : c.Text.Trim()) + " " + c.Bounds;

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
                System.IO.Path.GetTempPath(), "PlexusXAdv_" + Guid.NewGuid().ToString("N"));

            public void Dispose()
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
        }
    }
}
