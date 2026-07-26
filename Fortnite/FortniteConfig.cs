using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VibranceHud.Fortnite
{
    /// <summary>
    /// Reads and edits Fortnite's GameUserSettings.ini - a section-aware INI of
    /// <c>[Section]</c> headers followed by <c>key=value</c> lines. The same key name can
    /// exist in two different sections, so every lookup and edit is scoped to its section.
    /// Same discipline as the other config readers: preserve everything we weren't asked
    /// to touch byte-for-byte.
    /// </summary>
    public sealed class FortniteConfig
    {
        private readonly List<string> _lines;

        private FortniteConfig(List<string> lines) => _lines = lines;

        public static FortniteConfig Parse(string text) =>
            new(text.Length == 0 ? new List<string>() : new List<string>(text.Split('\n')));

        public string? Get(string section, string key)
        {
            var (headerIdx, endIdx) = FindSectionRange(section);
            if (headerIdx < 0) return null;

            var pattern = "^\\s*" + Regex.Escape(key) + "\\s*=(.*)$";
            for (int i = headerIdx + 1; i < endIdx; i++)
            {
                var m = Regex.Match(_lines[i], pattern, RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value.TrimEnd('\r');
            }
            return null;
        }

        public void Set(string section, string key, string value)
        {
            var (headerIdx, endIdx) = FindSectionRange(section);
            if (headerIdx < 0)
            {
                AppendNewSection(section, key, value);
                return;
            }

            var pattern = "^\\s*" + Regex.Escape(key) + "\\s*=";
            for (int i = headerIdx + 1; i < endIdx; i++)
            {
                if (!Regex.IsMatch(_lines[i], pattern, RegexOptions.IgnoreCase)) continue;

                bool cr = _lines[i].EndsWith("\r");
                _lines[i] = $"{key}={value}" + (cr ? "\r" : "");
                return;
            }

            // Key missing in this section: insert right after the section's last content
            // line, before any trailing blank lines that separate it from the next section.
            var insertAt = endIdx;
            while (insertAt > headerIdx + 1 && _lines[insertAt - 1].Trim().Length == 0)
                insertAt--;
            _lines.Insert(insertAt, $"{key}={value}");
        }

        public string Serialize() => string.Join("\n", _lines);

        private void AppendNewSection(string section, string key, string value)
        {
            // If the file ends in a newline, Split left a trailing empty element;
            // insert before it so the file keeps ending in a newline (no blank line).
            var insertAt = _lines.Count > 0 && _lines[^1].Length == 0 ? _lines.Count - 1 : _lines.Count;
            _lines.Insert(insertAt, $"[{section}]");
            _lines.Insert(insertAt + 1, $"{key}={value}");
        }

        /// <summary>The section's header index and the exclusive end of its lines (next header or EOF).</summary>
        private (int headerIdx, int endIdx) FindSectionRange(string section)
        {
            int headerIdx = -1;
            for (int i = 0; i < _lines.Count; i++)
            {
                if (IsSectionHeader(_lines[i], section)) { headerIdx = i; break; }
            }
            if (headerIdx < 0) return (-1, -1);

            int endIdx = _lines.Count;
            for (int i = headerIdx + 1; i < _lines.Count; i++)
            {
                if (IsAnySectionHeader(_lines[i])) { endIdx = i; break; }
            }
            return (headerIdx, endIdx);
        }

        private static bool IsSectionHeader(string line, string section)
        {
            var trimmed = line.TrimEnd('\r').Trim();
            return trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']'
                && string.Equals(trimmed[1..^1], section, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAnySectionHeader(string line)
        {
            var trimmed = line.TrimEnd('\r').Trim();
            return trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']';
        }
    }
}
