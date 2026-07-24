using System.Linq;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class ThemeCatalogTests
    {
        [Fact]
        public void All_ContainsTheExpectedThemes()
        {
            var names = ThemeCatalog.All.Select(t => t.Name).ToList();
            Assert.Contains("Violet", names);
            Assert.Contains("Emerald", names);
            Assert.Contains("Crimson", names);
            Assert.Contains("Light", names);
        }

        [Fact]
        public void EveryTheme_HasAUniqueName()
        {
            var names = ThemeCatalog.All.Select(t => t.Name).ToList();
            Assert.Equal(names.Count, names.Distinct().Count());
        }

        [Fact]
        public void OnlyLight_IsMarkedLight()
        {
            Assert.True(ThemeCatalog.ByName("Light").IsLight);
            Assert.False(ThemeCatalog.ByName("Violet").IsLight);
            Assert.False(ThemeCatalog.ByName("Emerald").IsLight);
        }

        [Fact]
        public void ByName_FallsBackToDefault_ForUnknownNames()
        {
            Assert.Equal(ThemeCatalog.DefaultName, ThemeCatalog.ByName("does-not-exist").Name);
            Assert.Equal(ThemeCatalog.DefaultName, ThemeCatalog.ByName(null).Name);
        }

        [Fact]
        public void Resolve_PrefersASavedName()
        {
            Assert.Equal("Emerald", ThemeCatalog.Resolve("Emerald", legacyLight: false).Name);
            Assert.Equal("Emerald", ThemeCatalog.Resolve("Emerald", legacyLight: true).Name);
        }

        [Fact]
        public void Resolve_MigratesLegacyLightBoolean_WhenNoNameSaved()
        {
            Assert.Equal("Light", ThemeCatalog.Resolve(null, legacyLight: true).Name);
            Assert.Equal("Light", ThemeCatalog.Resolve("", legacyLight: true).Name);
        }

        [Fact]
        public void Resolve_DefaultsToViolet_ForAFreshInstall()
        {
            Assert.Equal("Violet", ThemeCatalog.Resolve(null, legacyLight: false).Name);
        }

        [Fact]
        public void EveryColoredTheme_HasADistinctAccent()
        {
            var darkAccents = ThemeCatalog.All.Where(t => !t.IsLight).Select(t => t.Accent).ToList();
            Assert.Equal(darkAccents.Count, darkAccents.Distinct().Count());
        }
    }
}
