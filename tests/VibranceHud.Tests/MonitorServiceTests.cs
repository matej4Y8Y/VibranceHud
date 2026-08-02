using System.Collections.Generic;
using System.Linq;
using VibranceHud.Monitors;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>A monitor we can make behave however the test needs.</summary>
    internal sealed class FakeMonitors : IMonitorControl
    {
        private readonly List<MonitorSnapshot> _monitors = new();
        public readonly List<(string Device, MonitorSetting Setting, int Raw)> Writes = new();
        public bool Throws;

        public FakeMonitors Add(string device, string model,
            params (MonitorSetting Setting, int Min, int Current, int Max)[] settings)
        {
            var map = new Dictionary<MonitorSetting, MonitorRange>();
            foreach (var s in settings)
                map[s.Setting] = new MonitorRange(s.Min, s.Current, s.Max);
            _monitors.Add(new MonitorSnapshot(device, model, map));
            return this;
        }

        public IReadOnlyList<MonitorSnapshot> Scan()
        {
            if (Throws) throw new System.InvalidOperationException("no dxva2 here");
            return _monitors;
        }

        public bool Set(string deviceName, MonitorSetting setting, int rawValue)
        {
            Writes.Add((deviceName, setting, rawValue));
            return true;
        }
    }

    public sealed class MonitorServiceTests
    {
        private static FakeMonitors TwoMonitors() => new FakeMonitors()
            .Add("\\\\.\\DISPLAY1", "Dell S2721DGF",
                (MonitorSetting.Brightness, 0, 50, 100),
                (MonitorSetting.Contrast, 0, 75, 100))
            .Add("\\\\.\\DISPLAY2", "AOC 24G2",
                (MonitorSetting.Brightness, 0, 90, 255));

        [Fact]
        public void A_scan_finds_every_monitor_and_what_each_one_can_do()
        {
            var service = new MonitorService(TwoMonitors());
            service.Scan();

            Assert.Equal(2, service.Monitors.Count);
            Assert.Equal("Dell S2721DGF", service.Monitors[0].Label);
            Assert.True(service.Monitors[0].Supports(MonitorSetting.Contrast));
            Assert.False(service.Monitors[1].Supports(MonitorSetting.Contrast));
        }

        [Fact]
        public void A_machine_with_no_ddc_support_reports_nothing_rather_than_crashing()
        {
            var service = new MonitorService(new FakeMonitors { Throws = true });
            service.Scan();

            Assert.Empty(service.Monitors);
            Assert.True(service.HasScanned);
            Assert.False(service.AnyMonitorResponded);
        }

        [Fact]
        public void A_monitor_that_answers_nothing_is_told_apart_from_no_monitors()
        {
            // The tab shows a different message for each: one says check your monitor's menu,
            // the other says your hardware can't do this.
            var silent = new FakeMonitors().Add("\\\\.\\DISPLAY1", "Some Panel");
            var service = new MonitorService(silent);
            service.Scan();

            Assert.Single(service.Monitors);
            Assert.False(service.AnyMonitorResponded);
        }

        [Fact]
        public void A_slider_position_is_converted_into_that_monitors_own_units()
        {
            var fake = TwoMonitors();
            var service = new MonitorService(fake);
            service.Scan();

            service.SetPercent("\\\\.\\DISPLAY1", MonitorSetting.Brightness, 40);
            service.SetPercent("\\\\.\\DISPLAY2", MonitorSetting.Brightness, 40);
            service.Flush();

            // Same 40% on screen, different numbers down the cable.
            Assert.Contains(("\\\\.\\DISPLAY1", MonitorSetting.Brightness, 40), fake.Writes);
            Assert.Contains(("\\\\.\\DISPLAY2", MonitorSetting.Brightness, 102), fake.Writes);
        }

        [Fact]
        public void Dragging_a_slider_sends_one_write_not_sixty()
        {
            // The whole reason writes are queued. A drag produces a value every frame and the
            // cable manages roughly ten a second; without this the picture keeps moving well
            // after the user has let go.
            var fake = TwoMonitors();
            var service = new MonitorService(fake);
            service.Scan();

            for (int percent = 0; percent <= 60; percent++)
                service.SetPercent("\\\\.\\DISPLAY1", MonitorSetting.Brightness, percent);
            service.Flush();

            Assert.Single(fake.Writes);
            Assert.Equal(60, fake.Writes[0].Raw);
        }

        [Fact]
        public void Two_different_controls_both_get_through()
        {
            var fake = TwoMonitors();
            var service = new MonitorService(fake);
            service.Scan();

            service.SetPercent("\\\\.\\DISPLAY1", MonitorSetting.Brightness, 10);
            service.SetPercent("\\\\.\\DISPLAY1", MonitorSetting.Contrast, 90);
            service.Flush();

            Assert.Equal(2, fake.Writes.Count);
        }

        [Fact]
        public void Setting_something_the_monitor_cannot_do_is_refused_quietly()
        {
            var fake = TwoMonitors();
            var service = new MonitorService(fake);
            service.Scan();

            Assert.False(service.SetPercent("\\\\.\\DISPLAY2", MonitorSetting.Contrast, 50));
            Assert.False(service.SetPercent("\\\\.\\NOPE", MonitorSetting.Brightness, 50));

            service.Flush();
            Assert.Empty(fake.Writes);
        }

        [Fact]
        public void Closing_the_app_puts_every_monitor_back()
        {
            var fake = TwoMonitors();
            var service = new MonitorService(fake);
            service.Scan();

            service.SetPercent("\\\\.\\DISPLAY1", MonitorSetting.Brightness, 100);
            service.Flush();
            fake.Writes.Clear();

            service.RestoreAll();

            // 50 and 75 were what the Dell was set to when we found it; 90 for the AOC.
            Assert.Contains(("\\\\.\\DISPLAY1", MonitorSetting.Brightness, 50), fake.Writes);
            Assert.Contains(("\\\\.\\DISPLAY1", MonitorSetting.Contrast, 75), fake.Writes);
            Assert.Contains(("\\\\.\\DISPLAY2", MonitorSetting.Brightness, 90), fake.Writes);
        }

        [Fact]
        public void Restoring_drops_anything_still_queued()
        {
            // Otherwise a half-finished slider drag lands after the restore and the monitor
            // keeps the wrong value - the exact bug that leaves someone's screen dimmed.
            var fake = TwoMonitors();
            var service = new MonitorService(fake);
            service.Scan();

            service.SetPercent("\\\\.\\DISPLAY1", MonitorSetting.Brightness, 100);
            service.RestoreAll();
            service.Flush();

            var lastBrightness = fake.Writes
                .Where(w => w.Device == "\\\\.\\DISPLAY1" && w.Setting == MonitorSetting.Brightness)
                .Last();
            Assert.Equal(50, lastBrightness.Raw);
        }

        [Fact]
        public void Rescanning_does_not_forget_what_the_settings_were_originally()
        {
            // Plugging in a second screen triggers a rescan. If that overwrote the originals
            // with the values the user has since dialled in, exit would restore the wrong ones.
            var fake = TwoMonitors();
            var service = new MonitorService(fake);
            service.Scan();

            service.SetPercent("\\\\.\\DISPLAY1", MonitorSetting.Brightness, 100);
            service.Flush();
            service.Scan();

            Assert.Equal(50, service.OriginalRaw("\\\\.\\DISPLAY1", MonitorSetting.Brightness));
        }

        [Fact]
        public void Disposing_restores_too()
        {
            var fake = TwoMonitors();
            var service = new MonitorService(fake);
            service.Scan();
            service.SetPercent("\\\\.\\DISPLAY1", MonitorSetting.Brightness, 100);
            service.Flush();
            fake.Writes.Clear();

            service.Dispose();

            Assert.Contains(("\\\\.\\DISPLAY1", MonitorSetting.Brightness, 50), fake.Writes);
        }
    }
}
