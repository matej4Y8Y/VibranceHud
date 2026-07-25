using System;
using System.IO;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Settings must survive updates and crashes. Updates are already safe because the
    /// installer only wipes the install folder while settings live in %AppData% - these
    /// cover the other half: a corrupt or half-written file must not silently reset
    /// everything the user has configured.
    /// </summary>
    public class SettingsDurabilityTests : IDisposable
    {
        private readonly string _dir;

        public SettingsDurabilityTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "plexusx_settings_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        [Fact]
        public void SavedSettings_RoundTrip()
        {
            var store = new SettingsStore(_dir);
            store.Save(new AppSettings { SaturationPercent = 175, CustomBackgroundDim = 33 });

            var loaded = new SettingsStore(_dir).Load();

            Assert.Equal(175, loaded.SaturationPercent);
            Assert.Equal(33, loaded.CustomBackgroundDim);
        }

        [Fact]
        public void ASecondSave_KeepsTheEarlierOneAsABackup()
        {
            var store = new SettingsStore(_dir);
            store.Save(new AppSettings { SaturationPercent = 150 });
            store.Save(new AppSettings { SaturationPercent = 160 });

            Assert.True(File.Exists(Path.Combine(_dir, "settings.bak")),
                "a backup is what makes recovery from a corrupt write possible");
        }

        [Fact]
        public void CorruptSettings_FallBackToTheBackup_InsteadOfResettingEverything()
        {
            var store = new SettingsStore(_dir);
            store.Save(new AppSettings { SaturationPercent = 150 });
            store.Save(new AppSettings { SaturationPercent = 165 });

            // Simulate a crash mid-write leaving a truncated file.
            File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ \"Satura");

            var loaded = new SettingsStore(_dir).Load();

            Assert.Equal(150, loaded.SaturationPercent); // the backup, not defaults
        }

        [Fact]
        public void EmptyFile_IsTreatedAsCorrupt_NotAsValidDefaults()
        {
            var store = new SettingsStore(_dir);
            store.Save(new AppSettings { SaturationPercent = 155 });
            store.Save(new AppSettings { SaturationPercent = 158 });
            File.WriteAllText(Path.Combine(_dir, "settings.json"), "");

            Assert.Equal(155, new SettingsStore(_dir).Load().SaturationPercent);
        }

        [Fact]
        public void NoSettingsAtAll_GivesDefaults_WithoutThrowing()
        {
            var loaded = new SettingsStore(Path.Combine(_dir, "nope")).Load();

            Assert.Equal(100, loaded.SaturationPercent ?? 100);
        }

        [Fact]
        public void UnknownFieldsFromANewerBuild_DoNotWipeTheRest()
        {
            // A user rolling back a version must not lose everything else.
            File.WriteAllText(Path.Combine(_dir, "settings.json"),
                "{ \"SaturationPercent\": 142, \"SomeFutureSetting\": 9 }");

            Assert.Equal(142, new SettingsStore(_dir).Load().SaturationPercent);
        }
    }
}
