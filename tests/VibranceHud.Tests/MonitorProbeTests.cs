using System.Linq;
using VibranceHud.Monitors;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The DDC/CI probe, against whatever monitor is actually attached.
    ///
    /// These assert on shape rather than on values, because they have to pass on a machine
    /// whose panel refuses DDC/CI entirely - laptop internal displays almost always do, and
    /// plenty of desktop monitors either do not implement it or implement it badly. A probe
    /// that threw on unsupported hardware would take the whole app down at startup, which is
    /// exactly the failure this suite exists to prevent.
    /// </summary>
    public sealed class MonitorProbeTests
    {
        /// <summary>
        /// Not an assertion - a readout. Writes what this machine's monitors actually said to
        /// %TEMP%\plexusx-monitor.txt, the same way CapabilityProbeLiveTests reports the
        /// capture situation. Whether the Monitor tab can offer real controls or has to
        /// explain itself is a fact about the hardware, and it has to be read, not assumed.
        /// </summary>
        [Fact]
        public void ReportWhatThisMachinesMonitorsSupport()
        {
            var caps = MonitorProbe.Probe();

            var report = new System.Text.StringBuilder();
            report.AppendLine($"monitors found : {caps.Count}");
            foreach (var c in caps)
            {
                report.AppendLine();
                report.AppendLine($"  name       : {c.Description}");
                report.AppendLine($"  brightness : {c.SupportsBrightness}"
                    + (c.SupportsBrightness ? $"  ({c.BrightnessMin}..{c.BrightnessCurrent}..{c.BrightnessMax})" : ""));
                report.AppendLine($"  contrast   : {c.SupportsContrast}");
                report.AppendLine($"  rgb gain   : {c.SupportsRgbGain}");
                report.AppendLine($"  refusal    : {(c.Refusal.Length == 0 ? "(none - it answered)" : c.Refusal)}");
            }

            string text = report.ToString();
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plexusx-monitor.txt"), text);
            System.Console.WriteLine(text);
        }

        [Fact]
        public void ProbingNeverThrows()
        {
            var ex = Record.Exception(() => MonitorProbe.Probe());
            Assert.Null(ex);
        }

        [Fact]
        public void EveryMonitorIsDescribed()
        {
            var caps = MonitorProbe.Probe();

            Assert.All(caps, c => Assert.False(string.IsNullOrWhiteSpace(c.Description),
                "a monitor with no description cannot be shown to the user"));
        }

        /// <summary>
        /// The honesty rule (S5) as a test. A panel either does something, or it says why not.
        /// Silence is the one answer that is not allowed, because silence is what leads to a
        /// tab full of controls that quietly do nothing.
        /// </summary>
        [Fact]
        public void AMonitorThatSupportsNothingExplainsItself()
        {
            var caps = MonitorProbe.Probe();

            Assert.All(caps, c =>
            {
                bool supportsSomething = c.SupportsBrightness || c.SupportsContrast || c.SupportsRgbGain;
                Assert.True(supportsSomething || c.Refusal.Length > 0,
                    $"'{c.Description}' supports nothing and gives no reason");
            });
        }

        /// <summary>
        /// A panel that reports brightness must report a usable range. A min equal to max, or
        /// a current outside the range, means the driver answered with rubbish - and applying
        /// a slider built on that would move the user's brightness somewhere arbitrary.
        /// </summary>
        [Fact]
        public void ReportedBrightnessRangesAreUsable()
        {
            foreach (var c in MonitorProbe.Probe().Where(c => c.SupportsBrightness))
            {
                Assert.True(c.BrightnessMax > c.BrightnessMin,
                    $"'{c.Description}' reports min {c.BrightnessMin} max {c.BrightnessMax}");
                Assert.InRange(c.BrightnessCurrent, c.BrightnessMin, c.BrightnessMax);
            }
        }

        /// <summary>
        /// Probing twice must not change the picture. The probe reads; it never writes.
        /// </summary>
        [Fact]
        public void ProbingIsRepeatableAndReadOnly()
        {
            var first = MonitorProbe.Probe();
            var second = MonitorProbe.Probe();

            Assert.Equal(first.Count, second.Count);

            for (int i = 0; i < first.Count; i++)
            {
                Assert.Equal(first[i].Description, second[i].Description);
                Assert.Equal(first[i].SupportsBrightness, second[i].SupportsBrightness);
                Assert.Equal(first[i].BrightnessCurrent, second[i].BrightnessCurrent);
            }
        }
    }
}
