using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VibranceHud.Games
{
    /// <summary>
    /// Pure parser for Steam's libraryfolders.vdf - extracts every library path so the
    /// hub can look for games across all drives, not just the default Steam folder.
    /// Kept free of any filesystem/registry access so it is fully unit-testable.
    ///
    /// Handles both the modern nested format (`"path" "..."`) and the old flat format
    /// (`"1" "..."`). Parsing is line-by-line: matching the whole file at once lets a
    /// value on one line greedily pair with a key on the next, dropping entries.
    /// </summary>
    public static class SteamVdf
    {
        private static readonly Regex KeyValue =
            new("^\\s*\"([^\"]+)\"\\s+\"(.+?)\"\\s*$", RegexOptions.Compiled);

        public static IReadOnlyList<string> ParseLibraryPaths(string vdfContent)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(vdfContent)) return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in vdfContent.Split('\n'))
            {
                var m = KeyValue.Match(line);
                if (!m.Success) continue;

                var key = m.Groups[1].Value;
                var value = m.Groups[2].Value;

                // A path lives under either the "path" key (new format) or a numeric key
                // (old format). Numeric keys also appear as app-size entries, whose values
                // are bare numbers - excluded by requiring a path separator.
                bool keyIsPath = key.Equals("path", StringComparison.OrdinalIgnoreCase) || IsAllDigits(key);
                if (!keyIsPath) continue;
                if (!value.Contains('\\') && !value.Contains('/')) continue;

                var path = value.Replace("\\\\", "\\").Trim();
                if (seen.Add(path)) result.Add(path);
            }
            return result;
        }

        private static bool IsAllDigits(string s)
        {
            if (s.Length == 0) return false;
            foreach (var c in s)
                if (!char.IsDigit(c)) return false;
            return true;
        }
    }
}
