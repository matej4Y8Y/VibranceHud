using System.Collections.Generic;
using System.Linq;
using VibranceHud.SystemTweaks;
using Xunit;

namespace VibranceHud.Tests
{
    public class SystemTweakCatalogTests
    {
        private sealed class FakeRegistry : IRegistryAccess
        {
            private readonly Dictionary<string, string> _v = new();
            private static string K(RegistryRoot r, string s, string n) => $"{r}\\{s}\\{n}";
            public string? GetValue(RegistryRoot r, string s, string n) => _v.TryGetValue(K(r, s, n), out var x) ? x : null;
            public void SetValue(RegistryRoot r, string s, string n, string v, RegistryKind k) => _v[K(r, s, n)] = v;
            public void DeleteValue(RegistryRoot r, string s, string n) => _v.Remove(K(r, s, n));
        }

        private static IReadOnlyList<ISystemTweak> Tweaks() => new SystemTweakCatalog(new FakeRegistry()).All;

        [Fact]
        public void Catalog_IsNotEmpty()
        {
            Assert.NotEmpty(Tweaks());
        }

        [Fact]
        public void EveryTweak_HasAUniqueId()
        {
            var ids = Tweaks().Select(t => t.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void EveryTweak_HasLabelDescriptionAndCategory()
        {
            foreach (var t in Tweaks())
            {
                Assert.False(string.IsNullOrWhiteSpace(t.Id));
                Assert.False(string.IsNullOrWhiteSpace(t.Label));
                Assert.False(string.IsNullOrWhiteSpace(t.Description));
                Assert.False(string.IsNullOrWhiteSpace(t.Category));
            }
        }

        [Fact]
        public void Catalog_HasBothSafeAndAdvancedTweaks()
        {
            var tweaks = Tweaks();
            Assert.Contains(tweaks, t => t.Tier == TweakTier.Safe);
            Assert.Contains(tweaks, t => t.Tier == TweakTier.Advanced);
        }

        [Fact]
        public void EveryTweak_StartsUnapplied_OnAStockMachine()
        {
            // Nothing set in the fake registry = a stock machine; no tweak should read as already on.
            foreach (var t in Tweaks())
                Assert.False(t.IsApplied());
        }

        [Fact]
        public void ApplyThenRevert_LeavesTweakOff()
        {
            foreach (var t in Tweaks())
            {
                t.Apply();
                Assert.True(t.IsApplied());
                t.Revert();
                Assert.False(t.IsApplied());
            }
        }
    }
}
