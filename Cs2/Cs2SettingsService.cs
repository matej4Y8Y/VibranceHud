using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VibranceHud.Games;

namespace VibranceHud.Cs2
{
    /// <summary>
    /// Applies settings to a CS2 install by safely editing its autoexec.cfg - same discipline
    /// as the Rust service: back up the original before the first write, meant to be used
    /// while CS2 is closed, restore on demand. Unlike Rust, CS2 has no autoexec by default, so
    /// we create one if needed (and the launch-options helper tells the user to +exec it).
    /// </summary>
    public sealed class Cs2SettingsService
    {
        private readonly string _autoexecPath;

        public Cs2SettingsService(string autoexecPath) => _autoexecPath = autoexecPath;

        /// <summary>Builds a service for the CS2 install detected on this PC, or null.</summary>
        public static Cs2SettingsService? ForInstalledCs2()
        {
            var cs2 = GameLibrary.DetectInstalled().FirstOrDefault(d => d.Game.Id == "cs2");
            if (cs2 == null) return null;
            return new Cs2SettingsService(AutoexecPathFor(cs2.InstallDir));
        }

        /// <summary>autoexec lives at &lt;install&gt;\game\csgo\cfg\autoexec.cfg.</summary>
        public static string AutoexecPathFor(string installDir) =>
            Path.Combine(installDir, "game", "csgo", "cfg", "autoexec.cfg");

        public static bool IsCs2Running() => Process.GetProcessesByName("cs2").Length > 0;

        public string AutoexecPath => _autoexecPath;
        public string BackupPath => _autoexecPath + ".vibrancebak";
        public bool ConfigExists => File.Exists(_autoexecPath);
        public bool HasBackup => File.Exists(BackupPath);

        public Cs2Config ReadCurrent() =>
            Cs2Config.Parse(File.Exists(_autoexecPath) ? File.ReadAllText(_autoexecPath) : "");

        public void Backup()
        {
            if (!HasBackup && File.Exists(_autoexecPath))
                File.Copy(_autoexecPath, BackupPath);
        }

        public void Restore()
        {
            if (HasBackup)
                File.Copy(BackupPath, _autoexecPath, overwrite: true);
        }

        public void Apply(IReadOnlyDictionary<string, string> changes)
        {
            Backup();
            Directory.CreateDirectory(Path.GetDirectoryName(_autoexecPath)!); // cfg folder may be absent
            var cfg = ReadCurrent();
            foreach (var kv in changes)
                cfg.Set(kv.Key, kv.Value);
            File.WriteAllText(_autoexecPath, cfg.Serialize());
        }
    }
}
