using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VibranceHud.Apex
{
    /// <summary>
    /// Reads and edits Apex Legends' videoconfig.txt (lines of <c>"setting.key"   "value"</c>,
    /// both key and value quoted). Same discipline as RustConfig: preserve every line it
    /// wasn't asked to touch byte-for-byte, and only rewrite the values we're asked to.
    /// </summary>
    public sealed class ApexConfig
    {
        private readonly List<string> _lines;

        private ApexConfig(List<string> lines) => _lines = lines;

        public static ApexConfig Parse(string text) =>
            new(text.Length == 0 ? new List<string>() : new List<string>(text.Split('\n')));

        public string? Get(string key)
        {
            var idx = FindLine(key);
            if (idx < 0) return null;

            var m = Regex.Match(_lines[idx], "\"([^\"]*)\"\\s*\\r?$");
            return m.Success ? m.Groups[1].Value : null;
        }

        public void Set(string key, string value)
        {
            var idx = FindLine(key);
            if (idx < 0)
            {
                var newLine = $"\"{key}\"\t\t\"{value}\"";
                // If the file ends in a newline, Split left a trailing empty element;
                // insert before it so the file keeps ending in a newline (no blank line).
                if (_lines.Count > 0 && _lines[^1].Length == 0)
                    _lines.Insert(_lines.Count - 1, newLine);
                else
                    _lines.Add(newLine);
                return;
            }

            // Swap only the quoted value; keep the quoted key and separating whitespace intact.
            var line = _lines[idx];
            bool cr = line.EndsWith("\r");
            var core = cr ? line[..^1] : line;
            core = Regex.Replace(core, "\"([^\"]*)\"\\s*$", "\"" + value.Replace("$", "$$") + "\"");
            _lines[idx] = cr ? core + "\r" : core;
        }

        public string Serialize() => string.Join("\n", _lines);

        private int FindLine(string key)
        {
            var pattern = "^\\s*\"" + Regex.Escape(key) + "\"\\s+\"";
            for (int i = 0; i < _lines.Count; i++)
            {
                if (Regex.IsMatch(_lines[i], pattern, RegexOptions.IgnoreCase))
                    return i;
            }
            return -1;
        }
    }
}
