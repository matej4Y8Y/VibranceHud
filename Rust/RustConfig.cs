using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VibranceHud.Rust
{
    /// <summary>
    /// Reads and edits Rust's client.cfg (lines of <c>convar "value"</c>). Critically it
    /// preserves every line verbatim - we only ever change the values we're asked to and
    /// leave the player's other ~360 settings byte-for-byte untouched.
    /// </summary>
    public sealed class RustConfig
    {
        private readonly List<string> _lines;

        private RustConfig(List<string> lines) => _lines = lines;

        public static RustConfig Parse(string text)
        {
            var lines = text.Length == 0
                ? new List<string>()
                : new List<string>(text.Split('\n'));
            return new RustConfig(lines);
        }

        public string? Get(string convar)
        {
            var idx = FindLine(convar);
            if (idx < 0) return null;

            var m = Regex.Match(_lines[idx], "\"(.*)\"\\s*\\r?$");
            return m.Success ? m.Groups[1].Value : null;
        }

        public void Set(string convar, string value)
        {
            var idx = FindLine(convar);
            if (idx < 0)
            {
                var newLine = $"{convar} \"{value}\"";
                // If the file ends in a newline, Split left a trailing empty element;
                // insert before it so the file keeps ending in a newline (no blank line).
                if (_lines.Count > 0 && _lines[^1].Length == 0)
                    _lines.Insert(_lines.Count - 1, newLine);
                else
                    _lines.Add(newLine);
                return;
            }

            // Swap only the quoted value; keep the key text and any trailing \r intact.
            var line = _lines[idx];
            bool cr = line.EndsWith("\r");
            var core = cr ? line[..^1] : line;
            core = Regex.Replace(core, "\"(.*)\"\\s*$", "\"" + value.Replace("$", "$$") + "\"");
            _lines[idx] = cr ? core + "\r" : core;
        }

        public string Serialize() => string.Join("\n", _lines);

        private int FindLine(string convar)
        {
            var pattern = "^\\s*" + Regex.Escape(convar) + "\\s+\"";
            for (int i = 0; i < _lines.Count; i++)
            {
                if (Regex.IsMatch(_lines[i], pattern, RegexOptions.IgnoreCase))
                    return i;
            }
            return -1;
        }
    }
}
