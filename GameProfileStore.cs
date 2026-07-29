using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VibranceHud
{
    /// <summary>
    /// Reads and writes the saved <see cref="GameProfile"/> set to
    /// %LOCALAPPDATA%\PlexusX\profiles.json (the same Velopack-safe folder
    /// SettingsStore uses, so updates never wipe the file).
    /// Schema-versioned as { "version": 1, "profiles": { ... } }; future bumps
    /// migrate on load in the same place that today silently ignores an unknown
    /// schema and returns an empty set.
    /// </summary>
    public static class GameProfileStore
    {
        private const int CurrentSchemaVersion = 1;

        public static string StorePath
        {
            get
            {
                var appData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PlexusX");
                Directory.CreateDirectory(appData);
                return Path.Combine(appData, "profiles.json");
            }
        }

        /// <summary>Loads every profile from the user's store path. Returns an empty list
        /// when the file is missing or unparseable - the user keeps their current
        /// in-memory settings, and can just re-save.</summary>
        public static IReadOnlyList<GameProfile> Load() => Load(StorePath);

        /// <summary>Test-friendly overload that reads from an explicit path.</summary>
        public static IReadOnlyList<GameProfile> Load(string path)
        {
            if (!File.Exists(path)) return Array.Empty<GameProfile>();
            try
            {
                var json = File.ReadAllText(path);
                var doc = JsonSerializer.Deserialize<ProfilesDocument>(json);
                return doc?.Profiles ?? new List<GameProfile>();
            }
            catch (JsonException)
            {
                // Corrupted file - start fresh. Don't throw - user keeps current settings.
                return Array.Empty<GameProfile>();
            }
        }

        /// <summary>Serializes a profile set to JSON. Used by the editor card tests
        /// to seed the file; production code uses <see cref="Set"/>.</summary>
        public static string SerializeAll(IEnumerable<GameProfile> profiles)
        {
            var doc = new ProfilesDocument
            {
                Version = CurrentSchemaVersion,
                Profiles = new List<GameProfile>(profiles),
            };
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>Upserts a profile for its <see cref="GameProfile.GameId"/> and writes
        /// the whole file back atomically (write to .tmp, then File.Replace so a
        /// partial write never leaves the user with a corrupt profiles.json and
        /// a wiped profile set). The previous direct <c>File.WriteAllText</c>
        /// could lose every saved profile if PlexusX crashed mid-save - the
        /// single worst bug this class could have once users start saving
        /// per-game configurations.</summary>
        public static void Set(GameProfile profile) => Set(profile, StorePath);

        /// <summary>Test-friendly overload.</summary>
        public static void Set(GameProfile profile, string path)
        {
            var all = new List<GameProfile>(Load(path));
            var idx = all.FindIndex(p => p.GameId == profile.GameId);
            profile.LastUpdated = DateTime.UtcNow;
            if (idx >= 0) all[idx] = profile; else all.Add(profile);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            // Same atomic-write pattern as SettingsStore.Save: .tmp + File.Replace,
            // fallback to plain copy if Replace fails on a weird filesystem.
            var json = SerializeAll(all);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            try
            {
                if (File.Exists(path))
                    File.Replace(tmp, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                else
                    File.Move(tmp, path);
            }
            catch (Exception) when (File.Exists(tmp))
            {
                // Same fallback SettingsStore uses - over the network or on a
                // filesystem where Replace fails, a plain overwrite still
                // beats losing the save entirely.
                File.Copy(tmp, path, overwrite: true);
                try { File.Delete(tmp); } catch { /* best effort */ }
            }
        }

        public static void Remove(string gameId) => Remove(gameId, StorePath);

        public static void Remove(string gameId, string path)
        {
            var all = new List<GameProfile>(Load(path));
            all.RemoveAll(p => p.GameId == gameId);
            File.WriteAllText(path, SerializeAll(all));
        }

        /// <summary>Convenience: lookup a single profile by id, or null.</summary>
        public static GameProfile? Get(string gameId) => Get(gameId, StorePath);

        public static GameProfile? Get(string gameId, string path)
        {
            foreach (var p in Load(path))
                if (p.GameId == gameId) return p;
            return null;
        }

        private sealed class ProfilesDocument
        {
            public int Version { get; set; } = CurrentSchemaVersion;
            public List<GameProfile> Profiles { get; set; } = new();
        }
    }
}