using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VibranceHud.Games;

namespace VibranceHud.Fortnite
{
    /// <summary>
    /// Applies settings to a Fortnite install by safely editing its GameUserSettings.ini -
    /// same discipline as the Rust/CS2/Apex services: always back up the pristine original
    /// before the first write, meant to be used while Fortnite is closed, and can restore
    /// the backup on demand.
    /// </summary>
    public sealed class FortniteSettingsService
    {
        private readonly string _iniPath;

        public FortniteSettingsService(string iniPath) => _iniPath = iniPath;

        /// <summary>Builds a service for the Fortnite install detected on this PC, or null.</summary>
        public static FortniteSettingsService? ForInstalledFortnite()
        {
            var fortnite = GameLibrary.DetectInstalled().FirstOrDefault(d => d.Game.Id == "fortnite");
            if (fortnite == null) return null;
            return new FortniteSettingsService(DefaultIniPath());
        }

        /// <summary>GameUserSettings.ini lives at %LOCALAPPDATA%\FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini (per-user, not per-install).</summary>
        public static string DefaultIniPath() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FortniteGame", "Saved", "Config", "WindowsClient", "GameUserSettings.ini");

        /// <summary>True if the Fortnite game client is currently running (FortniteClient-Win64-Shipping.exe).</summary>
        public static bool IsFortniteRunning() =>
            Process.GetProcessesByName("FortniteClient-Win64-Shipping").Length > 0;

        public string IniPath => _iniPath;
        public string BackupPath => _iniPath + ".vibrancebak";
        public bool ConfigExists => File.Exists(_iniPath);
        public bool HasBackup => File.Exists(BackupPath);

        public FortniteConfig ReadCurrent() =>
            FortniteConfig.Parse(File.Exists(_iniPath) ? File.ReadAllText(_iniPath) : "");

        /// <summary>Copies the current config aside once; later calls keep the first backup.</summary>
        public void Backup()
        {
            if (!HasBackup && File.Exists(_iniPath))
                File.Copy(_iniPath, BackupPath);
        }

        public void Restore()
        {
            if (HasBackup)
                File.Copy(BackupPath, _iniPath, overwrite: true);
        }

        public void Apply(IReadOnlyList<FortniteConfigEdit> edits)
        {
            Backup(); // preserve the pristine original before the first edit
            Directory.CreateDirectory(Path.GetDirectoryName(_iniPath)!); // WindowsClient folder may be absent
            var cfg = ReadCurrent();
            foreach (var edit in edits)
                cfg.Set(edit.Section, edit.Key, edit.Value);
            File.WriteAllText(_iniPath, cfg.Serialize());
        }
    }
}
