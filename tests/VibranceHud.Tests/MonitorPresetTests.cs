using System.Collections.Generic;
using VibranceHud.Monitors;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Picture presets and factory reset - the monitor's own buttons, not sliders.
    ///
    /// These are code lists, not scales: a monitor reports "current 2, max 18" and those are
    /// menu entries, not a range you slide along. Offering them as a slider is how you end up
    /// dragging someone's screen somewhere it can't come back from, so they're buttons.
    /// </summary>
    public sealed class MonitorPresetTests
    {
        private static MonitorSnapshot With(int presetCount) => new(
            "\\\\.\\DISPLAY1", "Dell S2721DGF",
            new Dictionary<MonitorSetting, MonitorRange>
            {
                [MonitorSetting.Brightness] = new MonitorRange(0, 50, 100),
            },
            presetCount);

        [Fact]
        public void A_monitor_that_reports_presets_offers_them()
        {
            Assert.True(With(18).HasPresets);
            Assert.Equal(18, With(18).PresetCount);
        }

        [Fact]
        public void A_monitor_with_nothing_to_choose_between_offers_nothing()
        {
            // One preset is not a choice, and zero certainly isn't. Buttons that can't change
            // anything are the padding this app exists to not have.
            Assert.False(With(1).HasPresets);
            Assert.False(With(0).HasPresets);
        }

        [Fact]
        public void The_preset_list_is_capped_at_something_a_person_can_look_at()
        {
            // Monitors have reported absurd counts. Twenty buttons is already a lot; a hundred
            // is a wall of numbers nobody will ever press.
            Assert.True(With(200).Presets.Count <= 12);
        }

        [Fact]
        public void Presets_are_numbered_from_one_because_nobody_counts_from_zero()
        {
            var presets = With(4).Presets;
            Assert.Equal(new[] { 1, 2, 3, 4 }, presets);
        }

        [Fact]
        public void Sliders_still_work_the_same_alongside_them()
        {
            // The preset count is extra information, not a replacement for what was there.
            Assert.True(With(18).Supports(MonitorSetting.Brightness));
            Assert.Equal(50, With(18).Range(MonitorSetting.Brightness)!.Current);
        }
    }
}
