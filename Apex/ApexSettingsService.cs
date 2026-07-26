using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VibranceHud.Games;

namespace VibranceHud.Apex
{
    /// <summary>
    /// Applies settings to an Apex Legends install by safely editing its videoconfig.txt -
    /// same discipline as the Rust/CS2 services: always back up the pristine original before
    /// the first write, meant to be used while Apex is closed (it rewrites this file on exit),
    /// and can restore the backup on demand.
    /// </summary>
    public sealed class ApexSettingsService
    {
        private readonly string _videoConfigPath;

        public ApexSettingsService(string videoConfigPath) => _videoConfigPath = videoConfigPath;

        /// <summary>Builds a service for the Apex install detected on this PC, or null.</summary>
        public static ApexSettingsService? ForInstalledApex()
        {
            var apex = GameLibrary.DetectInstalled().FirstOrDefault(d => d.Game.Id == "apex");
            if (apex == null) return null;
            return new ApexSettingsService(DefaultVideoConfigPath());
        }

        /// <summary>videoconfig.txt lives at %USERPROFILE%\Saved Games\Respawn\Apex\local\videoconfig.txt (per-user, not per-install).</summary>
        public static string DefaultVideoConfigPath() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Saved Games", "Respawn", "Apex", "local", "videoconfig.txt");

        /// <summary>True if the Apex Legends game client is currently running (r5apex.exe).</summary>
        public static bool IsApexRunning() =>
            Process.GetProcessesByName("r5apex").Length > 0;

        public string VideoConfigPath => _videoConfigPath;
        public string BackupPath => _videoConfigPath + ".vibrancebak";
        public bool ConfigExists => File.Exists(_videoConfigPath);
        public bool HasBackup => File.Exists(BackupPath);

        public ApexConfig ReadCurrent() =>
            ApexConfig.Parse(File.Exists(_videoConfigPath) ? File.ReadAllText(_videoConfigPath) : "");

        /// <summary>Copies the current config aside once; later calls keep the first backup.</summary>
        public void Backup()
        {
            if (!HasBackup && File.Exists(_videoConfigPath))
                File.Copy(_videoConfigPath, BackupPath);
        }

        public void Restore()
        {
            if (HasBackup)
                File.Copy(BackupPath, _videoConfigPath, overwrite: true);
        }

        public void Apply(IReadOnlyDictionary<string, string> changes)
        {
            Backup(); // preserve the pristine original before the first edit
            Directory.CreateDirectory(Path.GetDirectoryName(_videoConfigPath)!); // Saved Games folder may be absent
            var cfg = ReadCurrent();
            foreach (var kv in changes)
                cfg.Set(kv.Key, kv.Value);
            File.WriteAllText(_videoConfigPath, cfg.Serialize());
        }
    }
}
