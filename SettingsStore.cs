using System;
using System.IO;
using System.Text.Json;

namespace VibranceHud
{
    /// <summary>
    /// Loads and saves <see cref="AppSettings"/> as JSON in a given directory.
    /// The directory is injected so tests can point it at a temp folder; the app
    /// uses %AppData%\VibranceHud.
    /// </summary>
    public sealed class SettingsStore
    {
        private readonly string _directory;

        public SettingsStore(string directory)
        {
            _directory = directory;
        }

        private string FilePath => Path.Combine(_directory, "settings.json");

        /// <summary>Missing or unreadable file just means defaults - never throws.</summary>
        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new AppSettings();
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath))
                       ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
