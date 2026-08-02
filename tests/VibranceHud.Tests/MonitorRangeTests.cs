using System.Collections.Generic;
using VibranceHud.Monitors;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Monitors don't agree on what a number means. One reports brightness 0-100, the next
    /// 0-255, and a few report odd ranges like 20-80 because the panel refuses to go darker.
    /// The slider is always 0-100% on screen, so every value crossing that boundary has to be
    /// converted - and a conversion that drifts by one on the way back is a setting that
    /// creeps every time the app opens.
    /// </summary>
    public sealed class MonitorRangeTests
    {
        [Fact]
        public void A_plain_zero_to_hundred_monitor_maps_one_to_one()
        {
            var range = new MonitorRange(0, 40, 100);
            Assert.Equal(40, range.Percent);
            Assert.Equal(40, range.RawFromPercent(40));
        }

        [Fact]
        public void A_zero_to_255_monitor_still_shows_a_sane_percentage()
        {
            var range = new MonitorRange(0, 255, 255);
            Assert.Equal(100, range.Percent);

            Assert.Equal(0, range.RawFromPercent(0));
            Assert.Equal(255, range.RawFromPercent(100));
            Assert.Equal(128, range.RawFromPercent(50));
        }

        [Fact]
        public void A_monitor_that_refuses_to_go_dark_maps_its_own_floor_to_zero_percent()
        {
            // Some panels report a minimum well above zero. Showing the user "20%" when the
            // monitor is as dark as it goes would be a slider that appears stuck.
            var range = new MonitorRange(20, 20, 80);
            Assert.Equal(0, range.Percent);
            Assert.Equal(20, range.RawFromPercent(0));
            Assert.Equal(80, range.RawFromPercent(100));
            Assert.Equal(50, range.RawFromPercent(50));
        }

        [Fact]
        public void Converting_out_and_back_never_drifts()
        {
            // The bug this guards against: open the app, it reads 51 and shows 20%, writes
            // back 50, next launch shows 19%. Repeat until the setting has walked away.
            var range = new MonitorRange(0, 0, 255);
            for (int percent = 0; percent <= 100; percent++)
            {
                var raw = range.RawFromPercent(percent);
                var back = new MonitorRange(0, raw, 255).Percent;
                Assert.True(back == percent,
                    $"{percent}% -> raw {raw} -> {back}%");
            }
        }

        [Fact]
        public void Values_outside_the_slider_are_clamped_not_wrapped()
        {
            var range = new MonitorRange(0, 50, 100);
            Assert.Equal(0, range.RawFromPercent(-30));
            Assert.Equal(100, range.RawFromPercent(400));
        }

        [Fact]
        public void A_range_with_no_room_in_it_is_not_a_real_control()
        {
            // A monitor answering "min 50, max 50" is answering to be polite. Offering a
            // slider that cannot move is worse than not offering one.
            Assert.False(new MonitorRange(50, 50, 50).IsUsable);
            Assert.False(new MonitorRange(0, 0, 0).IsUsable);
            Assert.True(new MonitorRange(0, 0, 100).IsUsable);
        }

        [Fact]
        public void A_current_value_outside_the_reported_range_is_pulled_back_in()
        {
            // Seen on real hardware: a monitor reports max 100 and current 110.
            Assert.Equal(100, new MonitorRange(0, 110, 100).Current);
            Assert.Equal(10, new MonitorRange(10, 5, 100).Current);
        }
    }

    /// <summary>
    /// What the tab is allowed to show. The rule is the whole point of the scan: only controls
    /// the monitor actually answered for, so nobody is handed a greyed-out list of things
    /// their screen can't do.
    /// </summary>
    public sealed class MonitorSnapshotTests
    {
        private static MonitorSnapshot Snapshot(params (MonitorSetting, MonitorRange)[] settings)
        {
            var map = new Dictionary<MonitorSetting, MonitorRange>();
            foreach (var (setting, range) in settings) map[setting] = range;
            return new MonitorSnapshot("\\\\.\\DISPLAY1", "Dell S2721DGF", map);
        }

        [Fact]
        public void Only_settings_the_monitor_answered_for_are_offered()
        {
            var snap = Snapshot(
                (MonitorSetting.Brightness, new MonitorRange(0, 50, 100)),
                (MonitorSetting.Contrast, new MonitorRange(0, 75, 100)));

            Assert.True(snap.Supports(MonitorSetting.Brightness));
            Assert.True(snap.Supports(MonitorSetting.Contrast));
            Assert.False(snap.Supports(MonitorSetting.Sharpness));
            Assert.False(snap.Supports(MonitorSetting.Volume));
        }

        [Fact]
        public void A_setting_that_answered_with_no_range_is_not_offered()
        {
            var snap = Snapshot((MonitorSetting.Volume, new MonitorRange(0, 0, 0)));
            Assert.False(snap.Supports(MonitorSetting.Volume));
        }

        [Fact]
        public void A_monitor_that_answered_nothing_is_reported_as_silent()
        {
            // This is the DDC/CI-switched-off case, and it needs its own state: the user has
            // to be told to check their monitor's menu, not told their monitor is unsupported.
            Assert.False(Snapshot().RespondedAtAll);
            Assert.True(Snapshot((MonitorSetting.Brightness, new MonitorRange(0, 50, 100)))
                .RespondedAtAll);
        }

        [Fact]
        public void The_model_name_is_used_when_there_is_one()
        {
            Assert.Equal("Dell S2721DGF", Snapshot().Label);

            var unnamed = new MonitorSnapshot("\\\\.\\DISPLAY2", "",
                new Dictionary<MonitorSetting, MonitorRange>());
            Assert.Equal("\\\\.\\DISPLAY2", unnamed.Label);
        }
    }
}
