using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using VibranceHud.Monitors;
using VibranceHud.Pages;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The Monitor tab, against fake hardware.
    ///
    /// Fake on purpose: what a real panel supports varies by machine, and a page whose tests
    /// only pass on the developer's monitor is a page nobody else can safely change. The one
    /// thing that must be true everywhere is that the page tells the truth about whatever it
    /// was given.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class MonitorHardwarePageTests
    {
        private static MonitorCapability Capable(string name = "Test Monitor") =>
            new(name, true, true, true, 0, 50, 100, "");

        private static MonitorCapability Refusing(string why) =>
            new("Laptop Screen", false, false, false, 0, 0, 0, why);

        private static MonitorHardwarePage Build(params MonitorCapability[] caps)
        {
            Theme.Apply("Violet");
            string dir = Path.Combine(Path.GetTempPath(), "PxMon_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            var page = new MonitorHardwarePage(new AppSettings(), new SettingsStore(dir), caps);
            page.Size = new Size(900, 760);
            page.CreateControl();
            return page;
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        /// <summary>Spaces stripped, because UiHelpers.Caption letter-spaces its headings -
        /// "CONTRAST" is on screen as "C O N T R A S T".</summary>
        private static IEnumerable<string> Texts(Control page) =>
            Descendants(page).Select(c => (c.Text ?? "").Replace(" ", "")).Where(t => t.Length > 0);

        [Fact]
        public void ACapablePanelGetsOneSliderPerThingItSupports()
        {
            using var page = Build(Capable());

            // Brightness, contrast, low blue light.
            Assert.Equal(3, Descendants(page).OfType<FlatSlider>().Count());
        }

        /// <summary>
        /// The honesty rule (S5). A panel that refuses must produce an explanation and no
        /// controls at all - a slider that silently does nothing is worse than a sentence
        /// saying the monitor will not talk.
        /// </summary>
        [Fact]
        public void ARefusingPanelGetsNoControlsAndAnExplanation()
        {
            const string why = "This monitor is connected but will not accept DDC/CI control.";
            using var page = Build(Refusing(why));

            Assert.Empty(Descendants(page).OfType<FlatSlider>());
            Assert.Contains(Texts(page), t => t.Contains("DDC/CI", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ThePanelIsNamedSoAMultiMonitorUserKnowsWhichIsWhich()
        {
            using var page = Build(Capable("Dell U2723QE"), Capable("LG 27GP850"));

            Assert.Contains(Texts(page), t => t.Contains("DELL", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(Texts(page), t => t.Contains("LG", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void EveryMonitorGetsItsOwnCard()
        {
            using var page = Build(Capable("One"), Refusing("nope"), Capable("Three"));

            Assert.Equal(3, Descendants(page).OfType<CardPanel>().Count());
        }

        /// <summary>A panel supporting only some things must not offer the rest.</summary>
        [Fact]
        public void OnlyTheSupportedControlsAppear()
        {
            var brightnessOnly = new MonitorCapability(
                "Half Monitor", SupportsBrightness: true, SupportsContrast: false,
                SupportsRgbGain: false, 0, 50, 100, "");

            using var page = Build(brightnessOnly);

            Assert.Single(Descendants(page).OfType<FlatSlider>());
            Assert.Contains(Texts(page), t => t.Contains("BRIGHTNESS", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(Texts(page), t => t.Contains("CONTRAST", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ItReportsWhetherItCanOfferAnythingAtAll()
        {
            using var capable = Build(Capable());
            using var refusing = Build(Refusing("no"));

            Assert.True(capable.OffersAnyHardwareControl);
            Assert.False(refusing.OffersAnyHardwareControl);
        }

        /// <summary>
        /// Nothing on this page may write to a monitor just by being constructed. Building a
        /// page is something tests, the shell and a theme rebuild all do; changing somebody's
        /// screen brightness as a side effect of that would be indefensible.
        /// </summary>
        [Fact]
        public void ConstructingThePageDoesNotTouchTheHardware()
        {
            var settings = new AppSettings { MonitorBrightness = 77 };
            string dir = Path.Combine(Path.GetTempPath(), "PxMon_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            Theme.Apply("Violet");
            using var page = new MonitorHardwarePage(settings, new SettingsStore(dir), new[] { Capable() });

            // The saved value is shown, not re-applied: the slider reads 77 and nothing was
            // pushed to the panel.
            Assert.Contains(Descendants(page).OfType<FlatSlider>(), s => s.Value == 77);
        }

        /// <summary>
        /// With nothing saved, brightness has to come from the capability the caller passed -
        /// not from a fresh read of whatever monitor happens to be plugged in.
        ///
        /// It did exactly that at first, which meant the page ignored its own parameter and
        /// hit the hardware during construction. The render showed 100% against a fake panel
        /// reporting 50.
        /// </summary>
        [Fact]
        public void BrightnessStartsFromTheProbedValueNotAFreshHardwareRead()
        {
            var probed = new MonitorCapability("Test Monitor",
                SupportsBrightness: true, SupportsContrast: false, SupportsRgbGain: false,
                BrightnessMin: 0, BrightnessCurrent: 42, BrightnessMax: 100, Refusal: "");

            using var page = Build(probed);

            Assert.Contains(Descendants(page).OfType<FlatSlider>(), s => s.Value == 42);
        }

        /// <summary>
        /// Contrast cannot be read back, so an unset slider must not sit at 0 - that would
        /// claim the panel's contrast is off, which is a statement the app cannot make.
        /// </summary>
        [Fact]
        public void UnreadableContrastStartsInTheMiddleRatherThanAtZero()
        {
            var contrastOnly = new MonitorCapability("Test Monitor",
                SupportsBrightness: false, SupportsContrast: true, SupportsRgbGain: false,
                0, 0, 0, "");

            using var page = Build(contrastOnly);

            var slider = Descendants(page).OfType<FlatSlider>().Single();
            Assert.Equal(50, slider.Value);
        }
    }
}
