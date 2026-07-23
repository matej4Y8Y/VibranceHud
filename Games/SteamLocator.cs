using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace VibranceHud.Games
{
    /// <summary>
    /// Finds Steam and its game libraries on any PC by reading the registry and
    /// libraryfolders.vdf - no hardcoded paths. Tries the per-user key first, then the
    /// machine-wide keys (including the 32-bit WOW6432Node view), and degrades to null /
    /// empty if Steam isn't installed.
    /// </summary>
    public static class SteamLocator
    {
        public static string? FindSteamPath()
        {
            string?[] candidates =
            {
                Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string,
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string,
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string,
            };

            foreach (var c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c) && Directory.Exists(c))
                    return Path.GetFullPath(c);
            }
            return null;
        }

        public static IReadOnlyList<string> GetLibraries(string steamPath)
        {
            var raw = new List<string> { steamPath };

            foreach (var rel in new[] { @"steamapps\libraryfolders.vdf", @"config\libraryfolders.vdf" })
            {
                var vdf = Path.Combine(steamPath, rel);
                if (!File.Exists(vdf)) continue;
                raw.AddRange(SteamVdf.ParseLibraryPaths(File.ReadAllText(vdf)));
                break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            foreach (var lib in raw)
            {
                if (!Directory.Exists(lib)) continue;
                var full = Path.GetFullPath(lib);
                if (seen.Add(full)) result.Add(full);
            }
            return result;
        }
    }
}
