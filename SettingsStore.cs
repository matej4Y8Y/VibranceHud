using System;
using System.IO;
using System.Text.Json;

namespace VibranceHud
{
    /// <summary>
    /// Loads and saves <see cref="AppSettings"/> as JSON in a given directory.
    /// The directory is injected so tests can point it at a temp folder; the app
    /// uses %AppData%\PlexusX - deliberately NOT the install folder, so updates
    /// (which wipe the install folder) never touch the user's settings.
    ///
    /// Writes are atomic and keep one backup. Without that, a crash or power cut
    /// mid-write leaves a truncated file, and the next launch silently resets every
    /// setting the user has - the single worst bug this class could have.
    /// </summary>
    public sealed class SettingsStore
    {
        private readonly string _directory;

        public SettingsStore(string directory)
        {
            _directory = directory;
        }

        private string FilePath => Path.Combine(_directory, "settings.json");
        private string BackupPath => Path.Combine(_directory, "settings.bak");

        /// <summary>
        /// Never throws. Falls back to the backup before giving up, so one bad write
        /// costs the user their last change rather than everything they've configured.
        /// </summary>
        public AppSettings Load()
        {
            return TryRead(FilePath) ?? TryRead(BackupPath) ?? new AppSettings();
        }

        private static AppSettings? TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text)) return null;
                return JsonSerializer.Deserialize<AppSettings>(text);
            }
            catch
            {
                return null;
            }
        }

        public void Save(AppSettings settings)
        {
            Directory.CreateDirectory(_directory);

            var json = JsonSerializer.Serialize(settings,
                new JsonSerializerOptions { WriteIndented = true });

            // Write to a temp file first, then swap it in. A half-written settings.json
            // is what turns "the app crashed" into "the app forgot everything".
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);

            try
            {
                if (File.Exists(FilePath))
                    File.Replace(temp, FilePath, BackupPath, ignoreMetadataErrors: true);
                else
                    File.Move(temp, FilePath);
            }
            catch (Exception) when (File.Exists(temp))
            {
                // File.Replace can fail on some filesystems and over network paths;
                // a plain overwrite still beats losing the save entirely.
                File.Copy(temp, FilePath, overwrite: true);
                try { File.Delete(temp); } catch { /* best effort */ }
            }
        }
    }
}
