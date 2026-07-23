using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VibranceHud.Games;

namespace VibranceHud.Rust
{
    /// <summary>
    /// Applies settings to a Rust install by safely editing its client.cfg. Always backs
    /// up the pristine original before the first write, refuses nothing but is meant to be
    /// used while Rust is closed (Rust rewrites its config on exit), and can restore the
    /// backup on demand.
    /// </summary>
    public sealed class RustSettingsService
    {
        private readonly string _clientCfgPath;

        public RustSettingsService(string clientCfgPath) => _clientCfgPath = clientCfgPath;

        /// <summary>Builds a service for the Rust install detected on this PC, or null.</summary>
        public static RustSettingsService? ForInstalledRust()
        {
            var rust = GameLibrary.DetectInstalled().FirstOrDefault(d => d.Game.Id == "rust");
            if (rust == null) return null;
            return new RustSettingsService(Path.Combine(rust.InstallDir, "cfg", "client.cfg"));
        }

        /// <summary>True if the Rust game client is currently running (RustClient.exe).</summary>
        public static bool IsRustRunning() =>
            Process.GetProcessesByName("RustClient").Length > 0;

        public string ClientCfgPath => _clientCfgPath;
        public string BackupPath => _clientCfgPath + ".vibrancebak";
        public bool ConfigExists => File.Exists(_clientCfgPath);
        public bool HasBackup => File.Exists(BackupPath);

        public RustConfig ReadCurrent() =>
            RustConfig.Parse(File.Exists(_clientCfgPath) ? File.ReadAllText(_clientCfgPath) : "");

        /// <summary>Copies the current config aside once; later calls keep the first backup.</summary>
        public void Backup()
        {
            if (!HasBackup && File.Exists(_clientCfgPath))
                File.Copy(_clientCfgPath, BackupPath);
        }

        public void Restore()
        {
            if (HasBackup)
                File.Copy(BackupPath, _clientCfgPath, overwrite: true);
        }

        public void Apply(IReadOnlyDictionary<string, string> changes)
        {
            Backup(); // preserve the pristine original before the first edit
            var cfg = ReadCurrent();
            foreach (var kv in changes)
                cfg.Set(kv.Key, kv.Value);
            File.WriteAllText(_clientCfgPath, cfg.Serialize());
        }
    }
}
