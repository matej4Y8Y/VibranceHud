using System.Collections.Generic;
using VibranceHud.SystemTweaks;
using Xunit;

namespace VibranceHud.Tests
{
    public class RegistryTweakTests
    {
        /// <summary>In-memory stand-in for the registry, keyed like a real path.</summary>
        private sealed class FakeRegistry : IRegistryAccess
        {
            public readonly Dictionary<string, string> Values = new();
            private static string Key(RegistryRoot r, string sub, string name) => $"{r}\\{sub}\\{name}";

            public string? GetValue(RegistryRoot root, string subKey, string name) =>
                Values.TryGetValue(Key(root, subKey, name), out var v) ? v : null;

            public void SetValue(RegistryRoot root, string subKey, string name, string value, RegistryKind kind) =>
                Values[Key(root, subKey, name)] = value;

            public void DeleteValue(RegistryRoot root, string subKey, string name) =>
                Values.Remove(Key(root, subKey, name));
        }

        private static RegistryTweak GameDvr(IRegistryAccess reg) => new(
            reg, "game-dvr", "Disable Game DVR", "Stops background game recording overhead.",
            "Background", TweakTier.Safe, "Game DVR off",
            new RegistrySetting(RegistryRoot.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", "0", "1"));

        [Fact]
        public void IsApplied_False_WhenValueIsAtStock()
        {
            var reg = new FakeRegistry();
            reg.Values[@"CurrentUser\System\GameConfigStore\GameDVR_Enabled"] = "1";

            Assert.False(GameDvr(reg).IsApplied());
        }

        [Fact]
        public void IsApplied_False_WhenValueMissing()
        {
            Assert.False(GameDvr(new FakeRegistry()).IsApplied());
        }

        [Fact]
        public void Apply_WritesTheOnValue_AndReportsStatus()
        {
            var reg = new FakeRegistry();
            var tweak = GameDvr(reg);

            var result = tweak.Apply();

            Assert.True(result.Ok);
            Assert.Equal("Game DVR off", result.StatusText);
            Assert.Equal("0", reg.Values[@"CurrentUser\System\GameConfigStore\GameDVR_Enabled"]);
            Assert.True(tweak.IsApplied());
        }

        [Fact]
        public void Revert_RestoresTheOffValue()
        {
            var reg = new FakeRegistry();
            var tweak = GameDvr(reg);
            tweak.Apply();

            tweak.Revert();

            Assert.Equal("1", reg.Values[@"CurrentUser\System\GameConfigStore\GameDVR_Enabled"]);
            Assert.False(tweak.IsApplied());
        }

        [Fact]
        public void Revert_DeletesTheValue_WhenStockStateIsAbsent()
        {
            var reg = new FakeRegistry();
            // Nagle's algorithm: the tweak value doesn't exist in a stock install.
            var nagle = new RegistryTweak(reg, "nagle", "Disable Nagle", "Lower input latency.",
                "Network", TweakTier.Safe, "Nagle disabled",
                new RegistrySetting(RegistryRoot.LocalMachine, @"SYSTEM\Test\Interfaces\x", "TcpAckFrequency", "1", null));

            nagle.Apply();
            Assert.True(nagle.IsApplied());

            nagle.Revert();

            Assert.False(reg.Values.ContainsKey(@"LocalMachine\SYSTEM\Test\Interfaces\x\TcpAckFrequency"));
            Assert.False(nagle.IsApplied());
        }

        [Fact]
        public void RequiresAdmin_True_WhenAnySettingWritesToLocalMachine()
        {
            var reg = new FakeRegistry();
            var hklm = new RegistryTweak(reg, "x", "X", "d", "Network", TweakTier.Safe, "s",
                new RegistrySetting(RegistryRoot.LocalMachine, "k", "n", "1", "0"));
            Assert.True(hklm.RequiresAdmin);
        }

        [Fact]
        public void RequiresAdmin_False_ForCurrentUserOnlyTweaks()
        {
            Assert.False(GameDvr(new FakeRegistry()).RequiresAdmin);
        }

        [Fact]
        public void MultiSetting_IsApplied_OnlyWhenEveryValueIsOptimized()
        {
            var reg = new FakeRegistry();
            var tweak = new RegistryTweak(reg, "multi", "Multi", "Two values.", "Network",
                TweakTier.Safe, "done",
                new RegistrySetting(RegistryRoot.LocalMachine, @"k", "a", "1", "0"),
                new RegistrySetting(RegistryRoot.LocalMachine, @"k", "b", "1", "0"));

            reg.Values[@"LocalMachine\k\a"] = "1"; // only one optimized
            Assert.False(tweak.IsApplied());

            reg.Values[@"LocalMachine\k\b"] = "1";
            Assert.True(tweak.IsApplied());
        }
    }
}
