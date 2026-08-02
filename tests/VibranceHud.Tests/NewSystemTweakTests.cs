using System;
// Covers the tweaks added on top of the original five.
//
// These write real system state, some of it HKLM, so the properties that matter are: every
// tweak reverts to exactly what Windows shipped with, no two tweaks fight over the same value,
// and IsApplied reads the registry rather than remembering what we did this session (otherwise
// the toggle lies after a restart).

using System.Collections.Generic;
using System.Linq;
using VibranceHud.SystemTweaks;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class NewSystemTweakTests
    {
        /// <summary>In-memory stand-in for the registry - no test touches HKLM.</summary>
        private sealed class FakeRegistry : IRegistryAccess
        {
            private readonly Dictionary<string, string> _values = new();
            private static string Key(RegistryRoot r, string sub, string name) => $"{r}|{sub}|{name}";

            public string? GetValue(RegistryRoot root, string subKey, string name)
                => _values.TryGetValue(Key(root, subKey, name), out var v) ? v : null;

            public void SetValue(RegistryRoot root, string subKey, string name, string value, RegistryKind kind)
                => _values[Key(root, subKey, name)] = value;

            public void DeleteValue(RegistryRoot root, string subKey, string name)
                => _values.Remove(Key(root, subKey, name));

            public int Count => _values.Count;
        }

        private static SystemTweakCatalog NewCatalog(out FakeRegistry reg)
        {
            reg = new FakeRegistry();
            return new SystemTweakCatalog(reg);
        }

        [Fact]
        public void CatalogGrew_BeyondTheOriginalFive()
        {
            var catalog = NewCatalog(out _);
            Assert.True(catalog.All.Count > 5, "expected the added FPS tweaks to be in the catalog");
        }

        [Fact]
        public void EveryTweak_HasAUniqueId()
        {
            var ids = NewCatalog(out _).All.Select(t => t.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        /// <summary>Nothing ships without an explanation - a toggle a user can't understand is
        /// exactly the padding this catalog is meant to avoid.</summary>
        [Fact]
        public void EveryTweak_ExplainsItself()
        {
            foreach (var t in NewCatalog(out _).All)
            {
                Assert.False(string.IsNullOrWhiteSpace(t.Label), $"{t.Id} has no label");
                Assert.False(string.IsNullOrWhiteSpace(t.Description), $"{t.Id} has no description");
                Assert.False(string.IsNullOrWhiteSpace(t.Category), $"{t.Id} has no category");
            }
        }

        /// <summary>
        /// The important one: every tweak must be fully reversible. Revert restores Windows'
        /// stock value (or deletes the value when the stock state is "absent"), so the registry
        /// legitimately still holds entries afterwards - what matters is that the tweak no
        /// longer reads as applied, and that a second round trip lands in the same place rather
        /// than drifting.
        /// </summary>
        [Fact]
        public void EveryTweak_IsFullyReversible()
        {
            foreach (var tweak in NewCatalog(out _).All)
            {
                var fresh = new FakeRegistry();
                var single = new SystemTweakCatalog(fresh).All.First(t => t.Id == tweak.Id);

                single.Apply();
                Assert.True(single.IsApplied(), $"{tweak.Id}: Apply() didn't register as applied");

                single.Revert();
                Assert.False(single.IsApplied(), $"{tweak.Id}: still reads as applied after Revert()");
                int afterFirstRevert = fresh.Count;

                // Round trip again - state must be stable, not accumulating.
                single.Apply();
                single.Revert();
                Assert.False(single.IsApplied(), $"{tweak.Id}: not reverted on the second cycle");
                Assert.Equal(afterFirstRevert, fresh.Count);
            }
        }

        /// <summary>A setting whose on and off values are identical does nothing except leave a
        /// trace on the user's machine - it's noise pretending to be a tweak.</summary>
        [Fact]
        public void NoTweak_WritesAValueItWouldRevertToAnyway()
        {
            foreach (var tweak in NewCatalog(out _).All)
            {
                var fresh = new FakeRegistry();
                var single = new SystemTweakCatalog(fresh).All.First(t => t.Id == tweak.Id);

                single.Apply();
                var applied = Snapshot(fresh);
                single.Revert();
                var reverted = Snapshot(fresh);

                Assert.False(applied.Count > 0 && applied.SequenceEqual(reverted),
                    $"{tweak.Id}: applying and reverting leave identical state - it does nothing");
            }
        }

        private static List<KeyValuePair<string, string>> Snapshot(FakeRegistry reg)
        {
            var field = typeof(FakeRegistry).GetField("_values",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var dict = (Dictionary<string, string>)field.GetValue(reg)!;
            return dict.OrderBy(k => k.Key).ToList();
        }

        /// <summary>State is read back from the registry, so the toggle is still right after the
        /// app restarts rather than reflecting only what this session did.</summary>
        [Fact]
        public void IsApplied_ReadsRealState_NotSessionMemory()
        {
            var catalog = NewCatalog(out var reg);
            var tweak = catalog.All.First(t => t.Id == "system-responsiveness");

            Assert.False(tweak.IsApplied());
            tweak.Apply();

            // A brand-new catalog over the same registry - like a relaunch.
            var afterRestart = new SystemTweakCatalog(reg).All.First(t => t.Id == "system-responsiveness");
            Assert.True(afterRestart.IsApplied());
        }

        /// <summary>Two tweaks writing the same value would silently undo each other when one is
        /// turned off.</summary>
        [Fact]
        public void NoTwoTweaks_WriteTheSameRegistryValue()
        {
            var seen = new Dictionary<string, string>();
            foreach (var tweak in NewCatalog(out var reg).All)
            {
                var fresh = new FakeRegistry();
                var single = new SystemTweakCatalog(fresh).All.First(t => t.Id == tweak.Id);
                single.Apply();

                foreach (var written in Written(fresh))
                {
                    Assert.False(seen.ContainsKey(written),
                        $"'{tweak.Id}' and '{seen.GetValueOrDefault(written)}' both write {written}");
                    seen[written] = tweak.Id;
                }
            }
        }

        private static IEnumerable<string> Written(FakeRegistry reg)
        {
            // Re-derive which keys got touched by diffing against an untouched instance.
            var field = typeof(FakeRegistry).GetField("_values",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var dict = (Dictionary<string, string>)field.GetValue(reg)!;
            return dict.Keys.ToList();
        }

        /// <summary>Anything writing HKLM needs the elevation prompt; anything that doesn't must
        /// not ask for admin it doesn't need.</summary>
        [Fact]
        public void AdminRequirement_MatchesWhereTheTweakWrites()
        {
            var catalog = NewCatalog(out _);

            Assert.True(catalog.All.First(t => t.Id == "system-responsiveness").RequiresAdmin,
                "writes HKLM, so it must request elevation");
            Assert.False(catalog.All.First(t => t.Id == "mouse-accel").RequiresAdmin,
                "only writes HKCU - must not prompt for admin");
        }

        /// <summary>
        /// Nothing may write Win32PrioritySeparation.
        ///
        /// A tweak used to set it to 38, described as "short quantums". It is not: 38 decodes to
        /// LONG quantums, which is the server configuration and costs a game latency. Windows
        /// already gives a desktop the 3:1 foreground bias by default, so there is no value worth
        /// writing here - only values worth not writing.
        /// </summary>
        [Fact]
        public void PriorityControlIsLeftAlone()
        {
            var catalog = NewCatalog(out var reg);
            foreach (var tweak in catalog.All) tweak.Apply();

            Assert.DoesNotContain(Written(reg), key =>
                key.Contains("Win32PrioritySeparation", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Situational tweaks stay opt-in; the ones that are simply correct don't get
        /// buried behind an Advanced section.</summary>
        [Fact]
        public void SituationalTweaks_AreMarkedAdvanced()
        {
            var catalog = NewCatalog(out _);

            Assert.Equal(TweakTier.Advanced, catalog.All.First(t => t.Id == "hags").Tier);
            Assert.Equal(TweakTier.Safe, catalog.All.First(t => t.Id == "mouse-accel").Tier);
        }

        /// <summary>
        /// Tweaks that were shipped and turned out not to earn their place. A toggle that costs
        /// frames, or that we can only describe as "might help, might not", is worse than no
        /// toggle - it spends the trust the whole tab runs on.
        /// </summary>
        [Theory]
        [InlineData("fullscreen-optimizations")]  // costs frames in borderless, which is how our users play
        [InlineData("game-mode")]                 // "helps some, hurts others" = we didn't know
        [InlineData("foreground-boost")]          // wrote the server-style long quantum
        public void RetiredTweaksStayGone(string id)
        {
            Assert.DoesNotContain(NewCatalog(out _).All, t => t.Id == id);
        }

        /// <summary>
        /// Game DVR needs both values. GameDVR_Enabled alone leaves the capture pipeline
        /// running, which is the whole thing the tweak claims to stop - it reported success
        /// and changed nothing measurable.
        /// </summary>
        [Fact]
        public void DisablingGameDvrAlsoStopsBackgroundCapture()
        {
            var catalog = NewCatalog(out var reg);
            catalog.All.First(t => t.Id == "game-dvr").Apply();

            var written = Written(reg).ToList();
            Assert.Contains(written, k => k.Contains("GameDVR_Enabled"));
            Assert.Contains(written, k => k.Contains("AppCaptureEnabled"));
        }
    }
}
